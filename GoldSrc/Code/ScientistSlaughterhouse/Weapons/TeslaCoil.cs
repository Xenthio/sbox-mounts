using GoldSrc.HalfLife;
using GoldSrc.Maps;
using Sandbox.Npcs;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Tesla Coil" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "gauss", "uranium", 999, ClassName = "teslacoil", Bucket = 5, Weight = 20, Mount = "ssh" )]
public sealed class TeslaCoil : StudioWeapon
{
	const float PulseInterval = 0.02f;
	const float InputTimeout = 0.16f;
	static readonly TagSet ProjectionTags = ["shock", "nevergib"];
	static readonly (Color Tint, float Width)[] ProjectionArcs =
	[
		(new Color( 0, 128f / 255, 1 ), 8),
		(new Color( 0, 128f / 255, 1 ), 4),
		(Color.White, 2),
		(Color.Blue, 4)
	];
	TimeUntil _ready, _windup, _nextFuel, _nextVisual, _inputDeadline, _nextModeChange;
	bool _projecting;
	[Sync( SyncFlags.FromHost )] bool Attraction { get; set; }

	internal override string ShotSequence => "fire";
	internal override bool CanIdle => !_projecting;
	internal override bool SwitchWhenSpent => !_projecting;
	internal override (string Name, float? Delay) IdleAnimation => Game.Random.Float() switch
	{
		<= 0.5f => ("idle", Game.Random.Float( 10, 15 )),
		<= 0.75f => ("idle2", Game.Random.Float( 10, 15 )),
		_ => ("fidget", 3)
	};
	internal override Vector3 WorldMuzzleOffset( bool hd ) => new( 28.4f, 2.8f, hd ? 4.6f : 4.1f );

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		UsesClips = false;
		StartingAmmo = 100;
		PrimaryAutomatic = SecondaryAutomatic = true;
		PrimaryDelay = SecondaryDelay = PulseInterval;
		Ballistics = new() { Range = 8192, Radius = 0 };
	}

	public override bool HasPrimaryAmmo() => !IsHeld || Ammo1 >= (Attraction ? 5 : 1);
	public override bool HasSecondaryAmmo() => !IsHeld || Ammo1 >= 20;

	public override bool CanReload() => IsActive && Owner.IsValid() && _nextModeChange <= 0;
	public override void Reload() => ToggleMode();

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ToggleMode()
	{
		if ( !CanReload() ) return;
		StopProjection();
		_nextModeChange = 1;
		Attraction = !Attraction;
		ShowMode( Attraction );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowMode( bool attraction )
	{
		if ( Application.IsDedicatedServer || IsProxy ) return;
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = attraction ? "Attraction mode selected" : "Projection mode selected",
			Position = new Vector2( -1, 0.5f ),
			Color = Color.White,
			HoldTime = 1,
			FadeOut = 0.25f
		} );
	}

	public override void PrimaryAttack() => Fire( false );
	public override void SecondaryAttack() => Fire( true );

	[Rpc.Host( NetFlags.OwnerOnly )]
	void Fire( bool chain )
	{
		if ( !IsActive || !Owner.IsValid() || _ready > 0 ) return;
		if ( WaterBody.Find( Scene, Owner.EyePosition ).IsValid() )
		{
			StopProjection();
			_ready = 0.15f;
			ShowClick( 0.15f );
			return;
		}

		if ( chain || Attraction )
		{
			StopProjection();
			if ( !TakePrimaryAmmo( chain ? 20 : 5 ) ) return;
			_ready = 1;
			var ray = chain ? OffsetLaunchRay( new Vector3( 0, 8, 8 ) ) : Owner.EyeTransform.ForwardRay;
			var hit = TraceLineOfFire( ray, 4096, 0 ).WithoutTags( "trigger" ).UseHitboxes().Run();
			if ( chain )
			{
				TeslaChain.Spawn( this, ray.Position, hit );
			}
			else
			{
				ShootBullet( hit, 50, 0, new TagSet { "shock" } );
				Scene.GetSystem<NpcSystem>()?.EmitStimulus( "combat", hit.EndPosition, Owner.GameObject, 512, 3 );
			}
			ShowDischarge( hit.EndPosition, chain );
			return;
		}

		if ( !HasPrimaryAmmo() ) return;
		_inputDeadline = InputTimeout;
		_ready = PulseInterval;
		if ( !_projecting )
		{
			_projecting = true;
			_windup = 0.1f;
			_nextFuel = _nextVisual = 0;
			BeginPresentation();
		}
		if ( _windup > 0 ) return;

		var beam = TraceLineOfFire( Owner.EyeTransform.ForwardRay, Ballistics.Range, 0 ).WithoutTags( "trigger" ).UseHitboxes().Run();
		if ( !beam.Hit || beam.StartedSolid ) return;
		ShootBullet( beam, 2, 0, ProjectionTags );
		if ( _nextVisual <= 0 )
		{
			_nextVisual = 0.08f;
			ShowProjection( beam.EndPosition );
		}
		if ( _nextFuel > 0 ) return;
		TakePrimaryAmmo();
		_nextFuel = 0.666f;
		if ( !HasPrimaryAmmo() ) StopProjection( 1 );
	}

	protected override void Think()
	{
		base.Think();
		if ( Input.Released( "attack1" ) ) ReleaseTrigger();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ReleaseTrigger() => StopProjection();

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Networking.IsHost && _projecting && _inputDeadline <= 0 ) StopProjection();
	}

	protected override void OnHolstered()
	{
		if ( Networking.IsHost ) StopProjection();
		ClearProjection();
		base.OnHolstered();
	}

	protected override void OnDisabled()
	{
		_projecting = false;
		ClearProjection();
		base.OnDisabled();
	}

	void StopProjection( float delay = 0.5f )
	{
		if ( !_projecting ) return;
		_projecting = false;
		_ready = delay;
		EndPresentation( delay );
	}

	public override void DryFire() => PlayClick( 0.25f );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowClick( float delay ) => PlayClick( delay );

	void PlayClick( float delay )
	{
		SetNextFire( delay );
		PlaySample( "weapons/357_cock1.wav", attenuation: 0.8f );
	}

	SoundHandle _sparkSound;
	readonly List<GameObject> _beamObjects = [];

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void BeginPresentation()
	{
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( "spin", holdLastFrame: true );
		PostWeaponEvent( WeaponEvents.Attack );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowProjection( Vector3 end )
	{
		if ( Application.IsDedicatedServer ) return;
		ClearProjection();
		var start = GetMuzzleTransform().Position;
		_sparkSound = PlaySample( $"buttons/spark{Game.Random.Int( 1, 6 )}.wav", Game.Random.Float( 0.5f, 1 ), attenuation: 0.8f );
		foreach ( var (tint, width) in ProjectionArcs )
			_beamObjects.Add( EnergyBeam.Show( start, end, tint.WithAlpha( 100f / 255 ), "lgtning", "Tesla projection", width, 0.12f, ContentMount ) );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowDischarge( Vector3 impact, bool chain )
	{
		SetNextFire( 1 );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( ShotSequence, idleAfter: 1 );
		PostWeaponEvent( WeaponEvents.Attack );
		if ( chain )
		{
			PlaySample( $"weapons/electro{Game.Random.Int( 4, 5 )}.wav", attenuation: 0.8f );
			PlaySample( "weapons/electro6.wav", attenuation: 0.8f );
			return;
		}
		if ( Application.IsDedicatedServer ) return;

		var start = GetMuzzleTransform().Position;
		foreach ( var (tint, width) in ProjectionArcs )
			EnergyBeam.Show( start, impact, tint.WithAlpha( 100f / 255 ), "lgtning", "Tesla attraction", width, 0.4f, ContentMount );
		Blast.SpriteEffect( "Tesla attraction flash", "zerogxplode", impact, 1, 10, mount: ContentMount );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void EndPresentation( float delay )
	{
		ClearProjection();
		SetNextFire( delay );
		var view = ViewModel?.GetComponent<StudioWeaponView>();
		if ( view.IsValid() ) view.RemainingIdleTime = 2;
	}

	void ClearProjection()
	{
		_sparkSound?.Stop();
		_sparkSound = null;
		foreach ( var beam in _beamObjects )
			if ( beam.IsValid() ) beam.Destroy();
		_beamObjects.Clear();
	}
}
