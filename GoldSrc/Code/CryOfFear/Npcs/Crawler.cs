using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Title( "Crawler" ), Category( "Cry of Fear" )]
[NpcDefinition( "monster_krypande", "krypande", MonsterClass.AlienMonster, 30, 16, 36, 138,
	Mount = "cof", Attack = NpcAttack.Melee, Damage = 4, Range = 64, Blood = Npc.BloodColor.Red )]
public sealed class Crawler : RushingMeleeNpc
{
	[Sync( SyncFlags.FromHost )] string CrawlSequence { get; set; }

	internal override string ChaseSequence => CrawlSequence ?? "walk1";
	internal override string SwingSounds => "slower/hammer_miss";
	internal override string HitSounds => "slower/hammer_strike";
	internal override string WallSounds => "slower/hammer_strike_wall";
	internal override string StrikeVoice => "slower/slower_attack";
	internal override NpcSounds Sounds { get; } = new( Alert: "slower/slower_alert", Pain: "slower/slower_pain" );
	internal override IEnumerable<StudioSequence> ExtraSequences => [ ..base.ExtraSequences, ..Activities( Walk ) ];

	internal override bool Pursue( Vector3 goal )
	{
		if ( Movement?.MoveTarget is null ) CrawlSequence = WeightedActivity( Walk )?.Name;
		return base.Pursue( goal );
	}

	internal override StudioSequence MovementSequence( float speed ) =>
		speed > 12 && Find( CrawlSequence ) is { } crawl ? crawl : base.MovementSequence( speed );

	internal override string SequenceSoundSample( string sample )
	{
		// The model's crawl events refer to a folder absent from the retail build.
		const string originalFolder = "krypande/";
		return sample.StartsWith( originalFolder, System.StringComparison.OrdinalIgnoreCase )
			? $"slower/{sample[originalFolder.Length..]}"
			: sample;
	}
}
