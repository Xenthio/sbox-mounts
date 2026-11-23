namespace GoldSrc.HalfLife;

[Hide]
public sealed class ShellCasing : Component, Component.ICollisionListener
{
	bool _shotgun;
	GameMount _mount;

	internal static void Eject( Vector3 origin, Rotation facing, bool shotgun, Vector3 velocity, GameMount mount = null, string modelPath = null )
	{
		if ( Application.IsDedicatedServer ) return;

		mount ??= Sandbox.Mounting.Directory.Get( "hl1" ) as HalfLifeMount;
		var relativePath = modelPath ?? (shotgun ? "models/shotgunshell.mdl" : "models/shell.mdl");
		var path = mount is HalfLifeMount ? $"valve/{relativePath}" : mount?.FindMountPath( relativePath );
		if ( path is null ) return;
		var model = Model.Load( $"mount://{mount.Ident}/{path}.vmdl" );
		if ( !model.IsValid() || model.IsError ) return;

		var shell = new GameObject( false, shotgun ? "Shotgun shell" : "Brass casing" );
		shell.NetworkMode = NetworkMode.Never;
		shell.Tags.Add( "debris" );
		shell.Tags.Add( "nocollide_props" );
		shell.WorldPosition = origin;
		shell.WorldRotation = Rotation.FromYaw( facing.Angles().yaw );
		shell.AddComponent<ModelRenderer>().Model = model;
		var collider = shell.AddComponent<ModelCollider>();
		collider.Model = model;
		collider.ColliderFlags |= ColliderFlags.IgnoreTraces;
		collider.Elasticity = 0.35f;
		var body = shell.AddComponent<Rigidbody>();
		body.RigidbodyFlags |= RigidbodyFlags.DisableCollisionSounds;
		body.EnhancedCcd = true;
		shell.AddComponent<TemporaryEffect>().DestroyAfterSeconds = 2.5f;
		var casing = shell.AddComponent<ShellCasing>();
		casing._shotgun = shotgun;
		casing._mount = mount;
		shell.Enabled = true;
		body.AngularVelocity = facing.Forward * Game.Random.Float( -14, 14 );
		body.Velocity = velocity;
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( collision.Other.IsTrigger || _mount is null ) return;
		var speed = System.MathF.Abs( collision.Contact.Speed.z );
		if ( speed <= 0 || (speed < 200 && Game.Random.Int( 0, 3 ) != 0) ) return;
		var sample = _shotgun ? "weapons/sshell" : "player/pl_shell";
		var volume = (_shotgun ? 0.5f : 0.8f) * System.MathF.Min( speed / 350, 1 );
		NpcAudio.Play( _mount.NpcAudio.Find( sample ), collision.Contact.Point, volume );
	}
}
