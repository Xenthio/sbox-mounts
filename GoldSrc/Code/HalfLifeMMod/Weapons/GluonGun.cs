using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.HalfLifeMMod;

[Title( "Gluon Gun" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "egon", "uranium", 100, Bucket = 3, Weight = 20, Mount = "hl1mmod" )]
public sealed class GluonGun : ClassicGluonGun
{
	static readonly TagSet NarrowDamageTags = ["energybeam"];

	bool _hasDeployed;

	[Sync( SyncFlags.FromHost )] public bool WideBeam { get; private set; }

	internal override string DeploySequence => !_hasDeployed ? "draw_first" : WideBeam ? "draw" : "draw_narrow";
	internal override string ShotSequence => WideBeam ? base.ShotSequence : "altfirecycle";
	internal override float? IdleAfterShot => float.PositiveInfinity;
	internal override (string Name, float? Delay) IdleAnimation => (RestingSequence, Game.Random.Float( 10, 15 ));
	internal override string ImpactSprite => "egon_flare_intro";
	internal override float BeamDamage => WideBeam ? 14 : 6;
	internal override float CellInterval => (WideBeam, Networking.MaxPlayers > 1) switch
	{
		(true, true) => 0.2f,
		(false, false) => 0.166f,
		_ => 0.1f
	};
	internal override (float Volume, float Pitch) BeamSoundTuning => WideBeam ? base.BeamSoundTuning : (0.9f, 1);
	protected override TagSet BeamDamageTags => WideBeam ? base.BeamDamageTags : NarrowDamageTags;
	protected override bool HasSplashDamage => WideBeam && base.HasSplashDamage;

	string RestingSequence => WideBeam ? "idle1" : "altfirecycle_idle";

	internal override void Configure()
	{
		base.Configure();
		DeployTime = 0.5f;
		SecondaryDelay = 1;
	}

	protected override void OnEquipped()
	{
		base.OnEquipped();
		_hasDeployed = true;
	}

	public override void SecondaryAttack()
	{
		EndAttack( SecondaryDelay );
		ToggleMode();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ToggleMode()
	{
		if ( !IsActive || !Owner.IsValid() ) return;
		WideBeam = !WideBeam;
		ShowMode( WideBeam );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowMode( bool wide )
	{
		if ( IsProxy || Application.IsDedicatedServer ) return;
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( wide ? "altfireoff" : "altfireon", idleAfter: 3 );
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = wide ? "Wide fire mode" : "Narrow fire mode",
			Position = new Vector2( -1, 0.5f ),
			Color = Color.White,
			HoldTime = 1,
			FadeOut = 0.25f
		} );
	}

	internal override void FinishBeamAnimation() =>
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( RestingSequence, idleAfter: 2 );

	protected override void ApplyBeamImpact( Vector3 point, Vector3 normal, bool firstImpact )
	{
		if ( WideBeam ) base.ApplyBeamImpact( point, normal, firstImpact );
	}
}
