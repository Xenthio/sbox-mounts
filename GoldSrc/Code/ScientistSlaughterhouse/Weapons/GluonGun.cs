using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Gluon Gun" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "egon", "uranium", 999, Bucket = 5, Weight = 20, Mount = "ssh" )]
public sealed partial class GluonGun : ClassicGluonGun
{
	static readonly TagSet AntimatterTags = ["energybeam"];

	enum EgonMode { Gluon, Antimatter, Ion }

	[Sync( SyncFlags.FromHost )] EgonMode Mode { get; set; }
	bool Antimatter => Mode == EgonMode.Antimatter;
	TimeUntil _nextModeChange;

	internal override float BeamDamage => Antimatter ? 6 : 14;
	internal override float CellInterval => Mode switch
	{
		EgonMode.Antimatter => 0.1f,
		EgonMode.Ion => 1f / (3 * (IonLevel + 1)),
		_ => 0.2f
	};
	internal override string BeamSprite => Antimatter ? "amp" : base.BeamSprite;
	internal override string ImpactSprite => Antimatter ? null : base.ImpactSprite;
	internal override bool HasBeamNoise => !Antimatter;
	protected override TagSet BeamDamageTags => Antimatter ? AntimatterTags : base.BeamDamageTags;
	protected override bool HasSplashDamage => true;

	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 100;
		SecondaryDelay = 1;
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
		Mode = Mode == EgonMode.Ion ? EgonMode.Gluon : Mode + 1;
		ShowMode( Mode );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowMode( EgonMode mode )
	{
		if ( IsProxy || Application.IsDedicatedServer ) return;
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = mode switch
			{
				EgonMode.Antimatter => "Antimatter Pulverizer mode selected",
				EgonMode.Ion => "Ion Discharge Cannon mode selected",
				_ => "Gluon Gun mode selected"
			},
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
