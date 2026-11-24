using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class ElectricalDart : CrossbowDart
{
	static readonly Color ArcColor = new Color32( 20, 15, 255, 255 );

	protected override Color TrailColor => new Color32( 4, 170, 255, 255 );
	internal override float PlayerDamage => 5;

	internal override void HandleContact( SceneTraceResult hit )
	{
		base.HandleContact( hit );
		if ( Enabled ) return;
		var field = GameObject.AddComponent<ElectricalDartField>();
		field.Initialize( this, hit.Normal );
		FlashArc( WorldPosition, hit.EndPosition );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	internal void FlashArc( Vector3 start, Vector3 end )
	{
		if ( !Application.IsDedicatedServer )
			EnergyBeam.Show( start, end, ArcColor, "lgtning", "Electrical dart arc", 2, 0.1f, ModelMount );
	}
}

[Hide]
public sealed class ElectricalDartField : Component
{
	const float Interval = 0.1f;
	const float Radius = 256;

	ElectricalDart _dart;
	Rotation _surface;
	TimeUntil _nextPulse = Interval;

	internal void Initialize( ElectricalDart dart, Vector3 normal )
	{
		_dart = dart;
		_surface = Rotation.LookAt( normal );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !_dart.IsValid() || _nextPulse > 0 ) return;
		_nextPulse += Interval;

		Blast.ApplyRadiusDamage( GameObject, _dart.Shooter, WorldPosition, 5, Radius, ignoreAttacker: false, "shock", "nevergib" );
		var direction = (_surface * Rotation.From( new Angles( Game.Random.Float( -90, 90 ), Game.Random.Float( -90, 90 ), 0 ) )).Forward;
		var start = WorldPosition + _surface.Forward;
		var end = Scene.Trace.Ray( start, start + direction * Radius ).IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "playercontroller" ).Run().EndPosition;
		_dart.FlashArc( start, end );
	}
}
