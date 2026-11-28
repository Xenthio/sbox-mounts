using UnityMount.Wwise;

namespace UnityMount;

public sealed class RiskOfRain2Mount : GameMount
{
	public override string Ident => "ror2";
	public override string Title => "Risk of Rain 2";
	protected override long AppId => 632360;
	protected override float ParticleGravity => 30f; // PhysicsManager gravity in globalgamemanagers.
	protected override string BuiltinResourceDirectory => "Risk of Rain 2_Data/Resources";
	protected override string BundleDirectory => "Risk of Rain 2_Data/StreamingAssets/aa/StandaloneWindows64";
	protected override IReadOnlyList<string> SceneGroups =>
	[
		"ror2-base-arena",
		"ror2-base-artifactworld",
		"ror2-base-bazaar",
		"ror2-base-blackbeach",
		"ror2-base-blackbeach2",
		"ror2-base-crystalworld",
		"ror2-base-dampcavesimple",
		"ror2-base-foggyswamp",
		"ror2-base-frozenwall",
		"ror2-base-goldshores",
		"ror2-base-golemplains",
		"ror2-base-golemplains2",
		"ror2-base-goolake",
		"ror2-base-limbo",
		"ror2-base-moon",
		"ror2-base-moon2",
		"ror2-base-mysteryspace",
		"ror2-base-rootjungle",
		"ror2-base-shipgraveyard",
		"ror2-base-skymeadow",
		"ror2-base-wispgraveyard",
		"ror2-dlc1-ancientloft",
		"ror2-dlc1-gauntlet1",
		"ror2-dlc1-gauntlet2",
		"ror2-dlc1-gauntlet3",
		"ror2-dlc1-gauntlet4",
		"ror2-dlc1-infinitetowerworld",
		"ror2-dlc1-itancientloft",
		"ror2-dlc1-itdampcave",
		"ror2-dlc1-itfrozenwall",
		"ror2-dlc1-itgolemplains",
		"ror2-dlc1-itgoolake",
		"ror2-dlc1-itmoon",
		"ror2-dlc1-itskymeadow",
		"ror2-dlc1-snowyforest",
		"ror2-dlc1-sulfurpools",
		"ror2-dlc1-voidraid",
		"ror2-dlc1-voidstage",
		"ror2-dlc2-artifactworld01",
		"ror2-dlc2-artifactworld02",
		"ror2-dlc2-artifactworld03",
		"ror2-dlc2-habitat",
		"ror2-dlc2-habitatfall",
		"ror2-dlc2-helminthroost",
		"ror2-dlc2-lakes",
		"ror2-dlc2-lakesnight",
		"ror2-dlc2-lemuriantemple",
		"ror2-dlc2-meridian",
		"ror2-dlc2-village",
		"ror2-dlc2-villagenight",
		"ror2-dlc3-computationalexchange",
		"ror2-dlc3-conduitcanyon",
		"ror2-dlc3-ironalluvium",
		"ror2-dlc3-ironalluvium2",
		"ror2-dlc3-nest",
		"ror2-dlc3-repurposedcrater",
		"ror2-dlc3-solusweb",
		"ror2-dlc3-solutionalhaunt",
		"ror2-dlc4-champrush",
		"ror2-dlc4-fungal",
		"ror2-dlc4-mire",
		"ror2-dlc4-sadism-sad1",
		"ror2-dlc4-sanctum",
		"ror2-dlc4-soul-soul1",
		"ror2-dlc4-survival-surv2",
		"ror2-dlc4-ultima",

		// Shipped presentation scenes with world geometry.
		"ror2-base-eclipseworld",
		"ror2-base-intro",
		"ror2-base-lobby",
		"ror2-base-logbook",
		"ror2-base-outro",
		"ror2-base-title",
		"ror2-dlc1",
		"ror2-dlc1-voidoutro"
	];

	const string SoundBankDirectory = "Risk of Rain 2_Data/StreamingAssets/Audio/GeneratedSoundBanks/Windows";
	static readonly (string Group, string Bank)[] Characters =
	[
		("ror2-base-commando", "char_Commando"),
		("ror2-base-lemurian", null),
		("ror2-base-captain", "char_captain"),
		("ror2-base-engi", "char_Engi"),
		("ror2-base-bandit2", "char_bandit2"),
		("ror2-base-croco", "char_Croco"),
		("ror2-base-heretic", "char_heretic"),
		("ror2-base-huntress", "char_Huntress"),
		("ror2-base-loader", "char_loader"),
		("ror2-base-mage", "char_Mage"),
		("ror2-base-merc", "char_Merc"),
		("ror2-base-toolbot", "char_Toolbot"),
		("ror2-base-treebot", "char_treeBot"),
		("ror2-base-beetle", null),
		("ror2-base-beetlequeen", null),
		("ror2-base-bison", null),
		("ror2-base-golem", null),
		("ror2-base-greaterwisp", null),
		("ror2-base-vagrant", null),
		("ror2-base-brother", "Boss_Brother"),
		("ror2-base-brother-karma", null),
		("ror2-base-brotherhaunt", null),
		("ror2-base-artifactshell", "Boss_ArtifactShell"),
		("ror2-base-beetleguard", null),
		("ror2-base-bell", null),
		("ror2-base-birdshark", null),
		("ror2-base-clayboss", null),
		("ror2-base-claybruiser", "Mob_clayBruiser"),
		("ror2-base-dronebackup", null),
		("ror2-base-drones", null),
		("ror2-base-electricworm", null),
		("ror2-base-grandparent", "Boss_Grandparent"),
		("ror2-base-gravekeeper", "Boss_Gravekeeper"),
		("ror2-base-hermitcrab", null),
		("ror2-base-imp", null),
		("ror2-base-impboss", null),
		("ror2-base-jellyfish", null),
		("ror2-base-lemurianbruiser", null),
		("ror2-base-lunarexploder", "Mob_lunarExploder"),
		("ror2-base-lunargolem", "Mob_lunarGolem"),
		("ror2-base-lunarwisp", "Mob_lunarWisp"),
		("ror2-base-magmaworm", null),
		("ror2-base-minimushroom", "Mob_miniMushroom"),
		("ror2-base-nullifier", "Mob_nullifier"),
		("ror2-base-parent", "Mob_parent"),
		("ror2-base-roboballboss", "Boss_RoboBall"),
		("ror2-base-scav", "Boss_Scav"),
		("ror2-base-scavlunar", null),
		("ror2-base-shopkeeper", null),
		("ror2-base-shopkeeper-spectator", null),
		("ror2-base-titan", null),
		("ror2-base-vulture", "Mob_vulture"),
		("ror2-base-wisp", null),
		("ror2-dlc1-acidlarva", "Mob_acidLarva"),
		("ror2-dlc1-assassin2", null),
		("ror2-dlc1-claygrenadier", "Mob_clayGrenadier"),
		("ror2-dlc1-dronecommander", null),
		("ror2-dlc1-elitevoid", null),
		("ror2-dlc1-flyingvermin", "Mob_flyingVermin"),
		("ror2-dlc1-gup", "Mob_gup"),
		("ror2-dlc1-majorandminorconstruct", "Boss_majorConstruct"),
		("ror2-dlc1-minorconstructonkill", "Mob_minorConstruct"),
		("ror2-dlc1-railgunner", "char_railgunner"),
		("ror2-dlc1-sulfurpod", null),
		("ror2-dlc1-vermin", "Mob_blindVermin"),
		("ror2-dlc1-voidbarnacle", "Mob_voidBarnacle"),
		("ror2-dlc1-voidjailer", "Mob_voidJailer"),
		("ror2-dlc1-voidmegacrab", "Mob_voidDevastator"),
		("ror2-dlc1-voidraidcrab", "Boss_voidRaidCrab"),
		("ror2-dlc1-voidsurvivor", "char_VoidSurvivor"),
		("ror2-dlc2-chef", "char_chef"),
		("ror2-dlc2-child", "Mob_child"),
		("ror2-dlc2-halcyonite", "Mob_halcyonite"),
		("ror2-dlc2-scorchling", "Mob_scorchling"),
		("ror2-dlc2-seeker", "char_seeker"),
		("ror2-dlc2-falseson", "char_falseson"),
		("ror2-dlc2-falsesonboss", "Boss_FalseSon"),
		("ror2-dlc3", "Boss_SolusVendor"), // The vendor body and its visual assets are in the shared bundles.
		("ror2-dlc3-defectiveunit", "DLC3_Monster_DefectiveUnit"),
		("ror2-dlc3-drifter", "Char_Drifter"),
		("ror2-dlc3-drones", null),
		("ror2-dlc3-dronetech", "Char_DroneTech"),
		("ror2-dlc3-extractorunit", "DLC3_Monster_ExtractorUnit"),
		("ror2-dlc3-friendunit", "DLC3_Item_FriendUnit"),
		("ror2-dlc3-ironhauler", "DLC3_Monsters"),
		("ror2-dlc3-minepod", "DLC3_Monster_MinePod"),
		("ror2-dlc3-solusamalgamator", "Boss_SolusAmalgamator"),
		("ror2-dlc3-solusheart", null),
		("ror2-dlc3-solusheart-phasesandcombos", null),
		("ror2-dlc3-solusmine", null),
		("ror2-dlc3-solusvendorshrine", null),
		("ror2-dlc3-solusweb", "DLC3_Boss_SolusWeb"),
		("ror2-dlc3-soluswing", "Boss_SolusWing"),
		("ror2-dlc3-soluswingform1", null),
		("ror2-dlc3-tanker", "DLC3_Monster_TankerUnit"),
		("ror2-dlc3-vulturehunter", "DLC3_Boss_AlloyHunter"),
		("ror2-dlc3-workerunit", "DLC3_Monster_WorkerUnit"),
		("ror2-dlc4", "DLC4_Providence"),
		("ror2-dlc4-acolyte", "DLC4_Acolyte"),
		("ror2-dlc4-bigmush", "DLC4_Champions"),
		("ror2-dlc4-commandoumbras", null),
		("ror2-dlc4-enforcer", "DLC4_Enforcer"),
		("ror2-dlc4-hollowman", "DLC4_Monsters"),
		("ror2-dlc4-lilreaper", null),
		("ror2-dlc4-lynx2", null),
		("ror2-dlc4-lynxambusher", null),
		("ror2-dlc4-lynxarcher", null),
		("ror2-dlc4-lynxghostturret", null),
		("ror2-dlc4-mimic", null),
		("ror2-dlc4-minilynx", null),
		("ror2-dlc4-reaper", null),
		("ror2-dlc4-reaperboss", null),
		("ror2-dlc4-scavsanctum", null),
		("ror2-dlc4-shadowrunner", null),
		("ror2-dlc4-spirit", null),
		("ror2-dlc4-sporespit", null),
		("ror2-dlc4-swift", null),
		("ror2-dlc4-totem", null),
		("ror2-dlc4-wakesacrifice", null)
	];

	static readonly string[] DroneSoundBanks =
	[
		"drone_emergencyHealing", "DLC3_BombardmentDrone", "DLC3_CleanUpDrone", "DLC3_FreezeDrone",
		"DLC3_HaulerDrone", "DLC3_JailerDrone", "DLC3_JunkDrone", "DLC3_RechargeDrone"
	];

	// These Wwise events live in Global even when their media is kept in a separate bank.
	static readonly string[] GlobalCharacterSoundPrefixes =
	[
		"artifactboss_", "beetle_guard_", "beetle_worker_", "beetle_queen_", "bellbody_", "bison_",
		"clayboss_", "claybruiser_", "golem_", "grandparent_", "gravekeeper_", "greater_wisp_", "hermitcrab_",
		"imp_", "jellyfish_", "lemurianbruiser_", "lunar_exploder_", "lunar_golem_", "lunar_wisp_", "magmaworm_", "minimushroom_", "moonbrother_",
		"newtshopkeep_", "nullifier_", "parent_", "roboball_", "scav_", "titanboss_", "vagrant_", "vulture_", "wisp_",
		"acid_larva_", "blindvermin_", "claygrenadier_", "flyingvermin_", "gup_", "voidbarnacle_",
		"voiddevastator_", "voidjailer_", "voidraid_", "drone_", "emergency_drone_", "healing_drone_"
	];

	private protected override UnityVisualOverrides ReadVisualOverrides( BundleIndex bundles, UnityAssetFile[] files )
	{
		const string catalogPath = "Risk of Rain 2_Data/StreamingAssets/aa/catalog.json";
		var visuals = new UnityVisualOverrides();
		RiskOfRain2LightAnimations.Read( bundles, files, visuals );
		RiskOfRain2PrintAnimations.Read( bundles, files, visuals );
		RiskOfRain2HurtboxMetadata.Read( bundles, files, visuals );
		RiskOfRain2OmniEffects.Read( bundles, files, visuals );
		RiskOfRain2EffectMetadata.Read( bundles, files, visuals );
		RiskOfRain2LocalSpins.Read( bundles, files, visuals );
		try
		{
			visuals.SnowMicrofacetNoise = RiskOfRain2ShaderTextures.FindSnowNoise( bundles );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or NotSupportedException )
		{
			Log.Warning( $"Could not read Risk of Rain 2 global shader textures: {e.Message}" );
		}
		if ( !FileSystem.FileExists( catalogPath ) ) return visuals;

		try
		{
			var addresses = new UnityAddressedAssets( bundles, UnityAddressables.Read( FileSystem, catalogPath ) );
			foreach ( var file in files.Concat( bundles.FindGroup( "static_monolith_skindefparams" ) ) )
				addresses.Index( file );
			RiskOfRain2VisualSkins.Read( bundles, addresses, files, visuals );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or FormatException or NotSupportedException or System.Text.Json.JsonException )
		{
			Log.Warning( $"Could not index Risk of Rain 2 Addressables: {e.Message}" );
		}
		return visuals;
	}

	IReadOnlyDictionary<uint, string> _sceneSoundEvents = new Dictionary<uint, string>();

	private protected override UnitySceneMarkers ReadSceneMarkers( BundleIndex bundles, UnityAssetFile scene ) =>
		RiskOfRain2SceneMarkers.Read( bundles, scene, _sceneSoundEvents );

	static bool IsMountedCharacterAudio( string name )
	{
		if ( name.StartsWith( "play_", StringComparison.OrdinalIgnoreCase ) || name.StartsWith( "stop_", StringComparison.OrdinalIgnoreCase ) )
			name = name[5..];
		return name.Equals( "item_use_BFG_charge", StringComparison.OrdinalIgnoreCase ) ||
			name.Equals( "item_proc_rosebuckler", StringComparison.OrdinalIgnoreCase ) ||
			name.Equals( "item_proc_igniteOnKill_Loop", StringComparison.OrdinalIgnoreCase ) ||
			name.Contains( "lemurian", StringComparison.OrdinalIgnoreCase ) ||
			GlobalCharacterSoundPrefixes.Any( prefix => name.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) );
	}

	protected override Task Mount( MountContext context )
	{
		var assets = base.Mount( context );
		if ( !IsInstalled ) return assets;

		var banks = new Dictionary<string, Func<string, bool>>( StringComparer.OrdinalIgnoreCase );
		foreach ( var (_, bank) in Characters )
			if ( bank is not null ) banks.Add( bank, null );
		foreach ( var bank in DroneSoundBanks )
			banks.Add( bank, null );
		banks.Add( "Music", null );
		banks.Add( "DLC4_Weather", null );
		banks.Add( "DLC4_Monster_PlantMush", null );
		banks.Add( "DLC3_Stages_SolutionalHaunt", null );
		banks.Add( "Global", IsMountedCharacterAudio );
		var events = ReadEvents( banks );
		var media = new Dictionary<string, Dictionary<uint, string>>( StringComparer.OrdinalIgnoreCase );
		var shared = new Dictionary<uint, string>();
		foreach ( var (bank, select) in banks )
		{
			try
			{
				var eventMedia = events.GetValueOrDefault( bank )?.SelectMany( entry =>
					entry.Media.Concat( entry.Routes.SelectMany( route => route.Media ) ) ).ToHashSet();
				var registered = WwiseSoundBank.Mount( context, FileSystem, $"{SoundBankDirectory}/{bank}.bnk", select, eventMedia );
				media.Add( bank, registered );
				foreach ( var (id, path) in registered ) shared.TryAdd( id, path );
				if ( registered.Count > 0 ) IsMounted = true;
			}
			catch ( Exception e ) when ( e is IOException or InvalidDataException )
			{
				Log.Warning( $"Could not index Wwise bank '{bank}': {e.Message}" );
			}
		}

		var paths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		var soundEvents = new Dictionary<uint, string>();
		foreach ( var (bank, entries) in events )
		{
			if ( !media.TryGetValue( bank, out var registered ) ) continue;
			foreach ( var entry in entries )
			{
				var path = $"sounds/wwise/events/{bank}/{Uri.EscapeDataString( entry.Name.ToLowerInvariant() )}.wwevent";
				if ( paths.Add( path ) )
				{
					context.Add( ResourceType.GameResource, path, new WwiseEventLoader( entry, Ident, registered, shared ) );
					if ( entry.Media.Any( shared.ContainsKey ) )
						soundEvents.TryAdd( entry.Id, $"mount://{Ident}/{path}" );
				}
			}
		}
		_sceneSoundEvents = soundEvents;
		return assets;
	}

	Dictionary<string, WwiseEventCatalog.Event[]> ReadEvents( Dictionary<string, Func<string, bool>> banks )
	{
		try
		{
			return WwiseEventCatalog.Read( FileSystem, $"{SoundBankDirectory}/SoundbanksInfo.xml", banks );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException )
		{
			Log.Warning( $"Could not read Wwise event catalog: {e.Message}" );
			return new( StringComparer.OrdinalIgnoreCase );
		}
	}

	protected override IReadOnlyList<string> SpeedTreeGroups { get; } =
	[
		"ror2-base-arena",
		"ror2-base-artifactworld",
		"ror2-base-blackbeach",
		"ror2-base-common-props",
		"ror2-base-dampcave",
		"ror2-base-foggyswamp",
		"ror2-base-frozenwall",
		"ror2-base-golemplains",
		"ror2-base-goolake",
		"ror2-base-moon",
		"ror2-base-shipgraveyard",
		"ror2-base-skymeadow",
		"ror2-base-title",
		"ror2-base-wispgraveyard",
		"ror2-dlc1-ancientloft",
		"ror2-dlc1-itancientloft",
		"ror2-dlc1-itskymeadow",
		"ror2-dlc1-voidraid",
		"ror2-dlc2-habitat-assets",
		"ror2-dlc2-habitatfall-assets",
		"ror2-dlc2-lakes-assets",
		"ror2-dlc2-lemuriantemple-assets",
		"ror2-dlc2-meridian-assets",
		"ror2-dlc2-village-assets"
	];

	protected override IReadOnlyList<string> BundleGroups { get; } =
	[
		"ror2-base-alienhead",
		"ror2-base-cleanse",
		"ror2-base-cleanse_static",
		"ror2-base-common",
		"ror2-base-common-globaltextures",
		"ror2-base-common-miscicons",
		"ror2-base-common-skyboxes",
		"ror2-base-common-vfx",
		"ror2-base-common-vfx_static",
		"ror2-base-common-vfx-particlemasks",
		"ror2-base-pearl", // The Artifact Shell skin references the Pearl mesh.
		"ror2-base-portalartifactworld_static",
		"ror2-dlc3-temporaryitemsdistributor",
		"ror2-dlc3-temporaryitemsdistributor_static",

		// Summons, artifact variants, and destructibles stored outside the main character bundles.
		"ror2-base-altarskeleton_static",
		"ror2-base-beetlegland_static",
		"ror2-base-beetlegroup-beetleward",
		"ror2-base-beetlegroup-beetleward_static",
		"ror2-base-elitepoison_static",
		"ror2-base-explosivepotdestructible_static",
		"ror2-base-fusioncelldestructible_static",
		"ror2-base-roboballbuddy_static",
		"ror2-base-shipgraveyard_static",
		"ror2-base-squid",
		"ror2-base-squid_static",
		"ror2-base-weeklyrun_static",
		"ror2-base-wispondeath_static",
		"ror2-cu8_static",
		"ror2-dlc1-eliteearth_static",
		"ror2-dlc2_static",
		"ror2-dlc3-destructiblespawner_static",
		"ror2-dlc3-explosivejunkbombdestructible_static",
		"ror2-dlc3-solutionalhaunt",
		"ror2-dlc4-hexhaunting_static",
		"ror2-dlc4-sadism-sad1_static",
		"ror2-dlc4-sanctum",
		"ror2-dlc4-shrinecompulsion_static",
		"ror2-dlc4-survival-surv2_static",
		"ror2-dlc4-ui",
		"ror2-dlc4-vengefulspirit_static",
		"ror2-dlc4-wakestop_static",
		.. Characters.SelectMany( character => new[] { character.Group, $"{character.Group}_static" } )
	];
}
