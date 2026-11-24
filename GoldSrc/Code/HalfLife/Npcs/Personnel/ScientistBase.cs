namespace GoldSrc.HalfLife;

[Hide]
public abstract class ScientistBase : Personnel
{
	protected ScientistBase() => Body = -1;

	// A spawned object's id is shared by peers, so a random head survives late joins.
	internal static int ResolveBody( int body, GameObject owner ) => body == -1 ? owner.Id.GetHashCode() & 3 : body;
	internal override int EffectiveBody => ResolveBody( Body, GameObject );

	internal static readonly Color FrostColor = new Color32( 128, 192, 255, 255 );
	[Sync( SyncFlags.FromHost )] TimeUntil FrostEnds { get; set; }
	Color? _normalTint;
	(StudioSequence Sequence, float Time) _frozenPose;

	protected override int PainVariants => 5;
	protected override string PreDisasterGreeting => "PHELLO";
	protected override string PreDisasterIdle => "PIDLE";
	internal override void DeathSound()
	{
		if ( _frozenPose.Sequence is null ) PainSound();
	}

	protected override void OnDamaged( Component.IDamageable.DamageEvent damage )
	{
		base.OnDamaged( damage );
		if ( Health > 0 && damage.DamageInfo.Tags.Has( "slowfreeze" ) ) FrostEnds = 4;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !Renderer.IsValid() ) return;
		if ( FrostEnds > 0 && _normalTint is null )
		{
			_normalTint = Renderer.Tint;
			Renderer.Tint *= FrostColor;
		}
		else if ( FrostEnds <= 0 && _normalTint is { } tint )
		{
			Renderer.Tint = tint;
			_normalTint = null;
		}
	}

	protected override void OnDeath( Component.IDamageable.DeathEvent death )
	{
		if ( death.DamageInfo.Tags.Has( "slowfreeze" ) ) _frozenPose = DisplayedPose;
		base.OnDeath( death );
	}

	protected override GameObject CreateCorpse() =>
		_frozenPose.Sequence is { } sequence
			? NpcCorpse.Spawn( this, sequence, anchored: true, frozenPose: _frozenPose.Time )
			: base.CreateCorpse();

	internal override string SentencePrefix => "SC";

	internal override int EffectiveSkin => Skin == 0 && EffectiveBody == 2 ? 1 : Skin;

	internal override float VoicePitch => (EffectiveBody % 3) switch { 0 => 1.05f, 2 => 0.95f, _ => 1 };

	internal override NpcSounds Sounds { get; } = new( "!SC_IDLE", "!SC_FEAR", "scientist/sci_pain", null,
		Follow: "!SC_OK", Wait: "!SC_WAIT" );
}
