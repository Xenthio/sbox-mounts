using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Title( "Hand Grenade" ), Category( "Half-Life: MMod" )]
[WeaponPickup( "grenade", "grenades", 10, ClassName = "handgrenade", Bucket = 4, Weight = 5, Mount = "hl1mmod" )]
public sealed class HandGrenade : ClassicHandGrenade
{
	const string WorldModelPath = "mount://hl1mmod/hl1mmod/models/w_grenade.mdl.vmdl";
	const float PrimeCooldown = 1.5f;
	bool _hasDeployed;

	internal override string DeploySequence => _hasDeployed ? "draw" : "draw_first";
	internal override string PinSequence => Underhand ? "lob_pinpull" : "pinpull";

	internal override void Configure()
	{
		base.Configure();
		DeployTime = 0.5f;
		SecondaryAutomatic = false;
	}

	protected override void OnEquipped()
	{
		base.OnEquipped();
		_hasDeployed = true;
	}

	public override bool HasSecondaryAmmo() => HasPrimaryAmmo();
	public override bool CanSecondaryAttack() => CanPrime && base.CanSecondaryAttack();

	public override void PrimaryAttack()
	{
		SetNextFire( PrimeCooldown );
		base.PrimaryAttack();
	}

	public override void SecondaryAttack()
	{
		SetNextFire( PrimeCooldown );
		Prime( true );
	}

	internal override Vector3 LaunchOrigin( Vector3 direction )
	{
		if ( !Underhand ) return base.LaunchOrigin( direction );

		var basis = Rotation.LookAt( direction );
		var offset = direction * 16 + basis.Left * 23 + basis.Up * 8;
		return BulletTrace( new Ray( Owner.EyePosition, offset.Normal ), offset.Length, 2 ).Run().EndPosition;
	}

	internal override float ThrowSpeed( float pitch ) =>
		System.Math.Clamp( (90 - pitch) * 4, 0, Underhand ? 250 : 500 );

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge )
	{
		var overhand = base.LaunchGrenade( origin, direction, pitch, pinAge );
		return Underhand ? "lob" : overhand;
	}

	internal override void SpawnGrenade( Vector3 origin, Vector3 velocity, float fuse ) =>
		TimedGrenade.Spawn<HandGrenadeProjectile>( Owner.GameObject, origin, velocity, fuse, "Hand grenade", WorldModelPath,
			grenade => grenade.FlightAnimation = Game.Random.Int( 0, 1 ) == 0 ? "toss" : "toss2", ContentMount );

	internal override void OnViewSequenceEvent( Mdl.Event animationEvent, SkinnedModelRenderer renderer )
	{
		switch ( animationEvent.Index )
		{
			case 5005:
			case 5105:
				if ( ContentMount is { } mount )
					NpcAudio.Play( mount.NpcAudio.Find( animationEvent.Options ), renderer.GetAttachment( "attachment1" )?.Position ?? renderer.WorldPosition );
				break;
			case 7001 when Owner.IsValid() && renderer.GetAttachment( "attachment1" ) is { } attachment:
				EjectSpoon( attachment.Position );
				break;
		}
	}

	void EjectSpoon( Vector3 position )
	{
		var model = Model.Load( WorldModelPath );
		if ( !model.IsValid() || model.IsError ) return;

		var spoon = new GameObject( false, "Grenade spoon" ) { NetworkMode = NetworkMode.Never, WorldPosition = position };
		spoon.Tags.Add( "debris", "nocollide_props" );
		var renderer = spoon.AddComponent<ModelRenderer>();
		renderer.Model = model;
		renderer.SetBodyGroup( "studio", 2 );
		var collider = spoon.AddComponent<BoxCollider>();
		collider.Scale = new Vector3( 1, 1, 4 );
		collider.Elasticity = 0.35f;
		collider.ColliderFlags |= ColliderFlags.IgnoreTraces;
		var body = spoon.AddComponent<Rigidbody>();
		body.RigidbodyFlags |= RigidbodyFlags.DisableCollisionSounds;
		spoon.AddComponent<TemporaryEffect>().DestroyAfterSeconds = 2.5f;
		spoon.Enabled = true;

		var basis = Owner.EyeTransform.Rotation;
		body.Velocity = basis.Forward * Game.Random.Float( 50, 70 )
			+ basis.Right * Game.Random.Float( 100, 150 ) + basis.Up * Game.Random.Float( 65, 85 );
		body.AngularVelocity = Vector3.Random * 10;
	}
}
