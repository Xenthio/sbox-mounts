using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Fireball Launcher" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "egon", "ffuel", 250, ClassName = "fireball", Bucket = 3, Weight = 20, Mount = "ssh" )]
public sealed class FireballLauncher : StudioWeapon
{
	const int ShotCost = 5;
	const string FireballSprite = "mount://ssh/ssh/sprites/fireball.spr";
	TimeUntil _readyToFire;
	bool _ballistic;

	internal override string ShotSequence => "altfirecycle";
	internal override (string Name, float? Delay) IdleAnimation =>
		Game.Random.Int( 0, 1 ) == 0 ? ("idle1", Game.Random.Float( 10, 15 )) : ("fidget1", 3);

	internal override void Configure()
	{
		HoldType = WeaponHolds.Rifle;
		UsesClips = false;
		StartingAmmo = 20;
		PrimaryDelay = 1.5f;
		SecondaryDelay = 0.2f;
	}

	public override bool HasPrimaryAmmo() => !IsHeld || Ammo1 >= ShotCost;
	public override bool HasSecondaryAmmo() => true;
	public override void PrimaryAttack() => Launch( Owner.IsValid() ? Owner.EyeTransform.ForwardRay : AimRay );
	public override void SecondaryAttack() => ToggleMode();

	[Rpc.Host( NetFlags.OwnerOnly )]
	void Launch( Ray ray )
	{
		if ( !IsActive || !Owner.IsValid() || _readyToFire > 0 || !HasPrimaryAmmo() ||
			!ray.Position.IsFinite || !ray.Forward.IsFinite || ray.Position.Distance( Owner.EyePosition ) > 96 ) return;
		if ( WaterBody.Find( Scene, Owner.EyePosition ).IsValid() )
		{
			_readyToFire = 0.15f;
			Click( 0.15f );
			return;
		}
		if ( !TakePrimaryAmmo( ShotCost ) ) return;

		if ( _ballistic )
			NpcProjectile.Fire<BallisticFireball>( Owner.GameObject, FireballSprite, 100, null, ray.Position, ray.Forward, 1000 );
		else
			NpcProjectile.Fire<NapalmFireball>( Owner.GameObject, FireballSprite, 100, null, ray.Position, ray.Forward, 1000 );
		_readyToFire = PrimaryDelay;
		ShowShot();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ToggleMode()
	{
		if ( !IsActive || !Owner.IsValid() || _readyToFire > 0 ) return;
		_ballistic = !_ballistic;
		ShowMode( _ballistic );
	}

	public override void DryFire() => PlayClick( 0.5f );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void Click( float delay ) => PlayClick( delay );

	void PlayClick( float delay )
	{
		SetNextFire( delay );
		PlaySample( "weapons/357_cock1.wav", 0.8f );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowShot()
	{
		SetNextFire( PrimaryDelay );
		PlaySample( "x/x_shoot1.wav", attenuation: 0.8f );
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( ShotSequence, idleAfter: 1 );
		PostWeaponEvent( WeaponEvents.Attack );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowMode( bool ballistic )
	{
		if ( IsProxy || !Owner.IsValid() || Owner.IsProxy ) return;
		ViewModel?.GetComponent<StudioWeaponView>()?.Play( ballistic ? "altfireoff" : "altfireon" );
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = ballistic ? "Ballistic mode selected" : "Direct mode selected",
			Position = new Vector2( -1, -1 ),
			Color = Color.White,
			HoldTime = 2
		} );
	}
}
