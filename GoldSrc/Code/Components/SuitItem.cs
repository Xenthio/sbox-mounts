namespace GoldSrc.Maps;

public sealed class SuitItem : MapEntity, Component.ITriggerListener
{
	[ConVar( "hl1_skill" )] public static int Skill { get; set; } = 1;
	[Property] public string Item { get; set; }
	[Property] public bool ShortLogon { get; set; }
	[Property] public Dictionary<string, int> Ammo { get; set; } = [];

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;
		var start = WorldPosition;
		var drop = Scene.Trace.Ray( start + Vector3.Up, start + Vector3.Down * 256 ).WithTag( "world" );
		if ( Item != "weaponbox" ) drop = drop.Size( new BBox( new Vector3( -16, -16, 0 ), new Vector3( 16, 16, 16 ) ) );
		var floor = drop.Run();
		if ( floor.Hit && !floor.StartedSolid ) WorldPosition = floor.EndPosition;
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( !CanTrigger || !Networking.IsHost ) return;
		var player = other.GetComponentInParent<PlayerController>();
		if ( !Grant( player, Item, ShortLogon, Ammo ) ) return;
		Fire( OnTrigger, player.GameObject, detached: true );
		Kill();
	}

	internal static bool Grant( PlayerController player, string item, bool shortLogon = false, Dictionary<string, int> ammo = null )
	{
		if ( !player.IsValid() || MapState.PlayerVitals( player ) is not { Health: > 0 } vitals ) return false;
		var suit = player.GetComponent<HevSuit>();
		string sound = null;
		switch ( item )
		{
			case "item_suit" when !suit.IsValid():
				player.GameObject.AddComponent<HevSuit>().Speak( shortLogon ? "!HEV_A0" : "!HEV_AAx" );
				break;
			case "item_battery" when suit.IsValid() && vitals.Armour < 100:
				vitals.Armour = System.Math.Min( 100, vitals.Armour + (Skill >= 3 ? 10 : 15) );
				suit.ReportCharge( vitals.Armour );
				sound = "items/gunpickup2.wav";
				break;
			case "item_healthkit" when vitals.Health < vitals.MaxHealth:
				vitals.Health = System.Math.Min( vitals.MaxHealth, vitals.Health + (Skill >= 3 ? 10 : 15) );
				sound = "items/smallmedkit1.wav";
				break;
			case "item_longjump" when suit.IsValid() && !player.GetComponent<LongJumpModule>().IsValid():
				player.GameObject.AddComponent<LongJumpModule>();
				suit.Speak( "!HEV_A1" );
				break;
			case "weaponbox" when player.GetComponentInChildren<BaseInventoryComponent>() is {} inventory:
				foreach ( var (path, count) in ammo ?? [] )
					if ( GameResource.Load<BaseAmmoResource>( path ) is {} resource ) inventory.GiveAmmo( resource, count );
				sound = "items/gunpickup2.wav";
				break;
			default:
				return false;
		}
		if ( sound is not null && Sandbox.Mounting.Directory.Get( "hl1" ) is HalfLifeMount mount )
			mount.NpcAudio.Emit( sound, player.WorldPosition );
		return true;
	}
}
