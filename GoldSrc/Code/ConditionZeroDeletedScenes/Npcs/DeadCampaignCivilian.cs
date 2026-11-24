using GoldSrc.HalfLife;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Dead Deleted Scenes Civilian" )]
[NpcDefinition( "monster_npc_dead", "npc-scientist", MonsterClass.HumanPassive, 8, 0, 0, 0, Mount = "czeror" )]
public sealed class DeadCampaignCivilian : PosedCorpse
{
	static readonly string[] Poses =
		["lying_on_back", "lying_on_stomach", "dead_sitting", "dead_hang", "dead_table1", "dead_table2", "dead_table3"];

	[Property] public int Pose { get; set; }
	[Property, Hide] public string NamedPose { get; set; }
	[Property, Hide] public int Head { get; set; } = -1;
	[Property, Hide] public int Body { get; set; }

	internal override float HealthFraction => 1;

	internal override string PoseName
	{
		get
		{
			var pose = !string.IsNullOrEmpty( NamedPose ) ? NamedPose
				: Pose >= 0 && Pose < Poses.Length ? Poses[Pose] : Poses[0];
			var clips = Renderer?.Model?.AnimationNames;
			return clips is null || clips.Contains( pose, System.StringComparer.OrdinalIgnoreCase )
				? pose : clips.FirstOrDefault( clip => clip.Contains( "dead", System.StringComparison.OrdinalIgnoreCase )
					|| clip.Contains( "lying", System.StringComparison.OrdinalIgnoreCase ) ) ?? clips.FirstOrDefault() ?? pose;
		}
	}

	internal void ApplyMapProperties( Bsp.Entity entity )
	{
		Pose = entity.GetInt( "pose" );
		NamedPose = entity.GetString( "deadanim" );
		Head = entity.GetInt( "head", -1 );
		Body = entity.GetInt( "body" );
		MaterialGroup = $"Skin {entity.GetInt( "skin" )}";
	}

	internal override void SetBody()
	{
		var body = System.Math.Max( Body, 0 );
		foreach ( var part in Renderer.Model.Parts.All )
		{
			var count = part.Choices.Count;
			if ( count == 0 ) continue;
			var choice = part.Name == "heads" && Head >= 0 && Head < count ? Head : body % count;
			Renderer.SetBodyGroup( part.Name, choice );
			body /= count;
		}
	}
}
