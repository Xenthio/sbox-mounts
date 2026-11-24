using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

[Title( "Rappelling Robot Grunt" ), Category( "Sven Co-op" )]
[NpcDefinition( "monster_robogrunt_repel", "rgrunt", MonsterClass.Machine, 0, 0, 0, 0, Mount = "svencoop" )]
public sealed class RoboGruntRepel : GruntRepel
{
	[Property, Hide] public Dictionary<string, string> ChildProperties { get; set; }

	public RoboGruntRepel() => GruntPrefab = RoboGrunt.Prefab;

	internal override void ApplyMapProperties( Bsp.Entity entity ) => ChildProperties = new( entity.KeyValues );

	internal override void ConfigureGrunt( HumanGrunt grunt )
	{
		if ( grunt is not RoboGrunt robot || ChildProperties is null ) return;
		var entity = new Bsp.Entity( ChildProperties );
		robot.ApplyMapProperties( entity );
		robot.ApplyMapModel( entity );
		if ( entity.GetFloat( "health" ) > 0 ) robot.Health = entity.GetFloat( "health" );
	}
}
