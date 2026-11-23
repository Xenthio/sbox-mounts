using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Spitter" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_spitter", "spitter", MonsterClass.AlienMonster, 60, 12, 72, 0,
	Mount = "cof", Attack = NpcAttack.Projectile, Damage = 15, Movement = NpcMovement.Stationary, Blood = Npc.BloodColor.Red )]
public sealed class Spitter : VoicedNpc
{
	const float WakeRange = 128;
	const string YawParameter = "spitter_yaw";
	static readonly string[] Controls = [null, YawParameter];
	StudioController _yawController;
	TimeUntil _nextWakeCheck, _nextSpit;

	[Property, Hide] public string EmergenceTarget { get; set; }
	[Sync( SyncFlags.FromHost )] bool Emerged { get; set; }
	[Sync( SyncFlags.FromHost )] float HeadYaw { get; set; }

	internal override float ViewDot => -0.7f;
	internal override string[] ControllerParameters => Controls;
	internal override StudioSequence RestSequence => Emerged ? base.RestSequence : Find( "appearidle" ) ?? base.RestSequence;
	internal override IEnumerable<StudioSequence> ExtraSequences => [ Find( "appearidle" ), Find( "appear" ), Find( "spit" ) ];
	internal override NpcSounds Sounds { get; } = new( Alert: "flygare/flygare_alert", Pain: "flygare/flygare_pain.wav", Death: "flygare/flygare_death.wav" );

	internal override void ApplyMapProperties( Bsp.Entity entity ) => EmergenceTarget = entity.GetString( "target" );
	internal override string MapModel( Bsp.Entity entity ) => entity.GetString( "model" );

	internal override void OnModelLoaded( ModelLoader loader )
	{
		EyeHeight = 4;
		_yawController = loader.Controllers is { Length: > 1 } ? loader.Controllers[1] : null;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( _yawController is not null && Health > 0 && Renderer.IsValid() )
			Renderer.Animation.Parameters.TrySet( YawParameter, _yawController.Fraction( HeadYaw ) );
	}

	protected override void Think()
	{
		if ( Emerged )
		{
			if ( IsLiveTarget( LastHostile ) )
				HeadYaw = (WorldRotation.Inverse * (LastHostile.WorldPosition - WorldPosition)).EulerAngles.Normal.yaw;
			base.Think();
			return;
		}

		if ( _nextWakeCheck > 0 || Find( "appear" ) is not { } appearance ) return;
		_nextWakeCheck = 0.1f;
		if ( !Scene.GetAllComponents<PlayerController>().Any( player => WorldPosition.DistanceSquared( player.WorldPosition ) <= WakeRange * WakeRange ) )
			return;

		Emerged = true;
		_nextSpit = 1;
		PlayAction( appearance );
		if ( !string.IsNullOrEmpty( EmergenceTarget ) )
			GetComponent<Maps.Monster>()?.FireAnimationTarget( EmergenceTarget );
	}

	internal override void IdleMovement() { }

	internal override float MovementPlaybackRate( StudioSequence sequence, float speed ) =>
		!Emerged && sequence.Name == "appearidle" ? 0 : base.MovementPlaybackRate( sequence, speed );

	internal override void Engage( GameObject target )
	{
		HeadYaw = (WorldRotation.Inverse * (target.WorldPosition - WorldPosition)).EulerAngles.Normal.yaw;
		if ( _nextSpit > 0 || Find( "spit" ) is not { } sequence ) return;
		var mouth = Renderer.GetAttachment( "attachment1" )?.Position ?? EyePosition;
		var direction = (TargetEyePosition( target ) - mouth).Normal;
		direction += new Vector3( Game.Random.Float( -0.05f, 0.05f ), Game.Random.Float( -0.05f, 0.05f ), Game.Random.Float( -0.05f, 0 ) );
		_nextSpit = Game.Random.Float( 1, 3 );
		PlayAction( sequence );
		AcidSpit.MuzzleDroplets( mouth );
		NpcProjectile.Fire<AcidSpit>( GameObject, null, Definition.Damage, target, mouth, direction, null );
	}

	internal override void ModifyDamage( Component.IDamageable.DamageEvent damage )
	{
		base.ModifyDamage( damage );
		if ( damage.DamageInfo.Tags.Has( "bullet" ) ) damage.AcceptedDamage *= 0.3f;
	}

	internal override void PainSound()
	{
		if ( Game.Random.Int( 0, 5 ) < 2 )
			EmitSound( Sounds.Pain, WorldPosition, pitch: Game.Random.Int( 95, 104 ) / 100f, speaker: this );
	}
}
