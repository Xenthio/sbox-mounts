namespace GoldSrc.HalfLife;

public partial class HumanGrunt
{
	internal override bool CanShootFromRope => !GunDropped && Ammo > 0;

	internal const string DeploymentPrefab = "mount://hl1/entities/npcs/monster_human_grunt.prefab";

	internal static HumanGrunt Deploy( Component source, Vector3 start, Rotation rotation, string prefabPath = DeploymentPrefab )
	{
		var prefab = PrefabFile.Load( prefabPath );
		if ( prefab is null ) return null;
		var go = GameObject.Clone( prefab, new Transform( start, rotation ), startEnabled: false );
		var grunt = go.GetComponent<HumanGrunt>( true );
		grunt.PrepareRappel();
		if ( source is GruntRepel rappel )
		{
			go.SetParent( source.GameObject, true );
			rappel.ConfigureGrunt( grunt );
		}
		go.Enabled = true;
		go.NetworkSpawn( true, null );
		return grunt;
	}
}
