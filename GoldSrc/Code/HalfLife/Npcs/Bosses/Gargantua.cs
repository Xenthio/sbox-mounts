namespace GoldSrc.HalfLife;

[Title( "Gargantua" ), Category( "Half-Life" )]
[Alias( "GoldSrc.HalfLifeGargantua" )]
[NpcDefinition( "monster_gargantua", "garg", MonsterClass.AlienMonster, 800, 42, 128, 145, Attack = NpcAttack.Melee, Damage = 30, Range = 80, StrikeEvents = new[] { 1 }, Blood = BloodColor.Yellow )]
public sealed class Gargantua : GargantuaNpc
{
	internal override NpcCorpse AddCorpseComponent( GameObject go ) => go.AddComponent<DyingGargantua>();
}

[Hide]
public abstract partial class GargantuaNpc : Npc
{
	internal override float ViewDot => -0.2f;

	internal override NpcSounds Sounds { get; } = new( "gar_idle", "gar_alert", Death: "gar_die", Attack: "gar_attack" );

	internal virtual float AttackScale => 1;
	internal virtual float FlameDamage => 5;
	internal virtual float StompDamage => 100;
	internal virtual bool Armored => true;
	internal virtual string SoundDirectory => "garg";
	GameMount EffectsMount => Sandbox.Mounting.Directory.Get( Definition.Mount ) as GameMount;

	internal override string VoiceSample( string sample ) => $"{SoundDirectory}/{sample}";

	static readonly string[] Attacks = ["stomp", "shootflames2"];
	internal override string[] ExtraActions => Attacks;
	internal override float AttackRange => _nextStomp <= 0 ? 1024 * AttackScale : _nextFlame <= 0 ? 330 * AttackScale : Definition.Range;
	TimeUntil _nextStomp, _nextPain;
	GameObject _enemy;
	[Sync( SyncFlags.FromHost )] bool EnemyVisible { get; set; }
	SpriteRenderer _eye;
	float _brightness;

	void UpdateEye()
	{
		if ( !_eye.IsValid() )
		{
			if ( !Renderer.IsValid() || Renderer.GetAttachmentObject( "attachment1" ) is not { } attachment ) return;
			if ( EffectsMount is not { } mount || mount.GetIndexedSprite( "gargeye1" ) is not { } sprite ) return;
			var go = new GameObject( attachment, false, "Gargantua eye" );
			go.NetworkMode = NetworkMode.Never;
			go.LocalPosition = Vector3.Zero;
			_eye = go.AddComponent<SpriteRenderer>();
			_eye.Size = 32 * AttackScale;
			_eye.Sprite = sprite;
			_eye.Additive = true;
			go.Enabled = true;
		}
		var target = EnemyVisible ? 200f : 0f;
		_brightness += System.Math.Clamp( target - _brightness, -260 * Time.Delta, 260 * Time.Delta );
		_eye.Color = Color.White.WithAlpha( _brightness / 255 );
	}

	internal override void PlaceAtSpawn()
	{
		_nextStomp = 5;
		_nextFlame = 2;
	}

	internal override void BeginAttack( GameObject target )
	{
		_enemy = target;
		if ( WorldPosition.Distance( target.WorldPosition ) <= Definition.Range )
		{
			base.BeginAttack( target );
			return;
		}
		if ( WorldPosition.Distance( target.WorldPosition ) <= 330 * AttackScale && _nextFlame <= 0 )
		{
			StartFlames();
			return;
		}
		if ( _nextStomp <= 0 && Find( "stomp" ) is { } stomp ) PlayAction( stomp );
	}

	internal override void PlaySequenceSound( NpcAudio audio, int id )
	{
		if ( Networking.IsHost && id is 3 or 4 ) GroundShake.Emit( Scene, WorldPosition, 4, 3, 1, 750 );
		var sound = id switch { 3 or 4 => "gar_step", 6 => "gar_breathe", _ => null };
		if ( sound is not null ) NpcAudio.Play( audio.Find( VoiceSample( sound ) ), WorldPosition, pitch: Game.Random.Float( 0.9f, 1.1f ) );
	}

	internal override void HandleActionEvent( int id )
	{
		if ( id != 5 ) return;
		_nextStomp = 12;
		var start = WorldPosition + (Vector3.Up * 60 + WorldRotation.Forward * 35) * AttackScale;
		var aim = _enemy.IsValid() ? (_enemy.GetBounds().Center - start).Normal : WorldRotation.Forward;
		var end = Scene.Trace.Ray( start, start + aim * (1024 * AttackScale) ).IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "npc", "player", "playercontroller", "trigger" ).Run().EndPosition;
		StompWave.Spawn( this, start, end, StompDamage, AttackScale );
		GroundShake.Emit( Scene, WorldPosition, 12, 100, 2, 1000 );
		EmitSound( VoiceSample( "gar_stomp" ), WorldPosition, pitch: Game.Random.Float( 0.9f, 1.1f ) );
		var floor = Scene.Trace.Ray( WorldPosition + Vector3.Up, WorldPosition + Vector3.Down * 20 )
			.IgnoreGameObjectHierarchy( GameObject ).Run();
		if ( floor.Hit ) StompMark( floor.EndPosition, floor.Normal );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void StompMark( Vector3 position, Vector3 normal ) => ImpactDecal.Place( position, normal, "gargstomp", 64 );

	internal override void PlayAttackSound( Vector3 origin ) => EmitSound( VoiceSample( Sounds.Attack ), origin );

	internal override void ModifyDamage( Component.IDamageable.DamageEvent damage )
	{
		base.ModifyDamage( damage );
		var tags = damage.DamageInfo.Tags;
		if ( !Armored || tags.Has( "blast" ) || tags.Has( "explosion" ) || tags.Has( "energybeam" ) || tags.Has( "crush" ) || tags.Has( "mortar" ) )
		{
			if ( HitStudioModel( damage.DamageInfo ) && _nextPain <= 0 )
			{
				_nextPain = Game.Random.Float( 2.5f, 4 );
				EmitSound( VoiceSample( "gar_pain" ), WorldPosition );
			}
			return;
		}
		if ( HitStudioModel( damage.DamageInfo ) )
		{
			if ( ShouldShowArmorImpact( 20, 101 ) ) ArmorRicochet.Show( damage.DamageInfo.Position, Game.Random.Float( 0.5f, 1.5f ) );
			damage.Cancelled = true;
		}
		else damage.AcceptedDamage *= 0.01f;
	}
}
