using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Gluon Gun" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "egon", "uranium", 999, Bucket = 5, Weight = 20, Mount = "ssh" )]
public sealed class GluonGun : ClassicGluonGun
{
	static readonly TagSet AntimatterTags = ["energybeam"];

	[Sync( SyncFlags.FromHost )] bool Antimatter { get; set; }
	TimeUntil _nextModeChange;

	internal override float BeamDamage => Antimatter ? 6 : 14;
	internal override float CellInterval => Antimatter ? 0.1f : 0.2f;
	internal override string BeamSprite => Antimatter ? "amp" : base.BeamSprite;
	internal override string ImpactSprite => Antimatter ? null : base.ImpactSprite;
	internal override bool HasBeamNoise => !Antimatter;
	protected override TagSet BeamDamageTags => Antimatter ? AntimatterTags : base.BeamDamageTags;
	protected override bool HasSplashDamage => true;

	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 100;
	}

	protected override void Think()
	{
		base.Think();
		if ( Input.Down( "use" ) && Input.Pressed( "reload" ) && !Input.Down( "attack1" ) && !BeamActive )
			CycleMode();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void CycleMode()
	{
		if ( !IsActive || !Owner.IsValid() || BeamActive || _nextModeChange > 0 ) return;
		_nextModeChange = 1;
		Antimatter = !Antimatter;
		ShowMode( Antimatter );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowMode( bool antimatter )
	{
		if ( IsProxy || Application.IsDedicatedServer ) return;
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = antimatter ? "Antimatter Pulverizer mode selected" : "Gluon Gun mode selected",
			Position = new Vector2( -1, 0.5f ),
			Color = Color.White,
			HoldTime = 1,
			FadeOut = 0.25f
		} );
	}

	protected override void ApplyBeamImpact( Vector3 point, Vector3 normal, bool firstImpact )
	{
		if ( !Antimatter )
		{
			base.ApplyBeamImpact( point, normal, firstImpact );
			return;
		}

		// The native weapon puts a zero-fuse charge at the hit point on even uranium counts.
		if ( (Ammo1 & 1) != 0 ) return;

		var floor = Scene.Trace.Ray( point + Vector3.Up * 8, point + Vector3.Down * 32 )
			.IgnoreGameObjectHierarchy( Owner.GameObject ).WithoutTags( "npc", "player", "playercontroller", "trigger" ).Run();
		var grounded = floor.Hit && !floor.StartedSolid;
		// GoldSrc traces stop just short of the hit surface. Keep an ungrounded blast outside it.
		var origin = grounded ? floor.EndPosition : point + normal * 0.5f;
		Blast.Detonate( GameObject, Owner.GameObject, origin, floor.Normal, 75, grounded, ContentMount );
	}
}
