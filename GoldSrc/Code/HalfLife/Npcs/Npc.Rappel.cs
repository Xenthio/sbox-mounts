namespace GoldSrc.HalfLife;

public partial class Npc
{
	internal const string RappelSequence = "repel_repel";
	internal const string RappelAttack = "repel_shoot";
	internal const string RappelLanding = "repel_land";

	[Property, Sync( SyncFlags.FromHost )] public Vector3? RopeAnchor { get; private set; }

	float _descentSpeed;
	bool _descending;
	GameObject _rappelRope;
	TimeUntil _nextRappelBurst;

	internal virtual bool CanShootFromRope => false;

	internal void PrepareRappel()
	{
		RopeAnchor = WorldPosition + Vector3.Up * 112;
		_descentSpeed = Game.Random.Float( 128, 196 );
	}

	void StartRappel()
	{
		if ( !RopeAnchor.HasValue ) return;
		// A restored actor can still be on the rope.
		if ( _descentSpeed <= 0 ) _descentSpeed = Game.Random.Float( 128, 196 );
		_descending = true;
		Launch( Vector3.Down * _descentSpeed );
	}

	void UpdateRappel()
	{
		if ( !RopeAnchor.HasValue )
		{
			if ( _rappelRope.IsValid() ) _rappelRope.Destroy();
			return;
		}

		if ( !_rappelRope.IsValid() ) ShowRope( RopeAnchor.Value );
		if ( !Networking.IsHost || !_descending ) return;

		if ( IsOnGround )
		{
			_descending = false;
			RopeAnchor = null;
			// The ground check can land the actor before the descending hull reaches the floor.
			var floor = Scene.Trace.Ray( WorldPosition, WorldPosition + Vector3.Down * 8 )
				.IgnoreGameObjectHierarchy( GameObject ).WithCollisionRules( Tags )
				.WithoutTags( "npc", "player", "playercontroller", "trigger" ).Run();
			if ( floor.Hit && !floor.StartedSolid ) WorldPosition = floor.EndPosition;
			if ( Find( RappelLanding ) is { } landing ) PlayAction( landing );
			return;
		}

		var body = GetComponent<Rigidbody>();
		if ( body.IsValid() ) body.Velocity = Vector3.Down * _descentSpeed;
		if ( AdvanceAction() || _nextRappelBurst > 0 || !CanShootFromRope || Find( RappelAttack ) is not { } shoot ) return;
		var target = FindVisibleEnemy();
		if ( !IsLiveTarget( target ) ) return;
		_nextRappelBurst = shoot.Duration + 0.3f;
		AttackWith( target, shoot );
	}

	void ShowRope( Vector3 anchor )
	{
		_rappelRope = new GameObject( GameObject, false, "Rappel rope" );
		_rappelRope.NetworkMode = NetworkMode.Never;
		_rappelRope.LocalPosition = Vector3.Up * 48;
		var beam = _rappelRope.AddComponent<BeamEffect>();
		beam.TargetPosition = anchor;
		beam.Scale = 1;
		beam.BeamColor = Color.White;
		beam.BeamLifetime = 2;
		beam.Looped = true;
		if ( Sandbox.Mounting.Directory.Get( Definition.Mount ) is GameMount mount && mount.GetIndexedSprite( "rope" ) is { } sprite )
			beam.Texture = sprite.Animations[0].Frames[0].Texture;
		_rappelRope.Enabled = true;
	}
}
