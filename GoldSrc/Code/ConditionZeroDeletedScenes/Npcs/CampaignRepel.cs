using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ConditionZeroDeletedScenes;

[Title( "Rappelling Counter-Terrorist" )]
[NpcDefinition( "monster_counter_terrorist_repel", "sas", MonsterClass.PlayerAlly, 0, 0, 0, 0, Mount = "czeror" )]
public sealed class CampaignRepel : MapEntity
{
	const string DefaultSoldier = "monster_ct_sas_mp5";

	[Property] public string SoldierClass { get; set; } = DefaultSoldier;
	[Property] public int Head { get; set; }
	[Property] public int Skin { get; set; }

	bool _deployed;

	internal void ApplyMapProperties( Bsp.Entity entity )
	{
		SoldierClass = entity.GetString( "monstertype" ) is { Length: > 0 } type ? type : DefaultSoldier;
		Head = entity.GetInt( "repelhead" );
		Skin = entity.GetInt( "repelskin" );
		TargetName = entity.TargetName;
	}

	protected override void OnStart()
	{
		if ( !Map.IsValid() ) Trigger();
	}

	public override void Trigger( GameObject activator = null )
	{
		if ( !Networking.IsHost || !CanTrigger || _deployed || string.IsNullOrEmpty( SoldierClass ) ) return;
		var prefab = GameObject.GetPrefab( $"mount://czeror/entities/npcs/{SoldierClass}.prefab" );
		if ( !prefab.IsValid() || prefab.GetComponent<CampaignSoldier>( true ) is not { IsCounterTerrorist: true } ) return;

		var go = prefab.Clone( WorldTransform, startEnabled: false );
		var soldier = go.GetComponent<CampaignSoldier>( true );
		_deployed = true;
		soldier.Head = Head;
		soldier.Skin = Skin;
		soldier.Health = 200;
		soldier.PrepareRappel();
		go.Enabled = true;
		go.NetworkSpawn( true, null );
		Kill();
	}

	public override void TurnOff( GameObject activator = null ) => Trigger( activator );
}
