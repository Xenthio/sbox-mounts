using System.Globalization;

namespace UnityMount.Wwise;

/// <summary>Reads the routing metadata published with Wwise banks.</summary>
static class WwiseEventCatalog
{
	internal sealed record Event( uint Id, string Name, string DurationType, float? MinDuration, float? MaxDuration,
		float? MaxAttenuation, uint[] Media, Route[] Routes, WwiseEventResource.StateChange[] StateChanges );

	internal sealed record Route( WwiseEventResource.SwitchValue[] Conditions, uint[] Media );

	public static Dictionary<string, Event[]> Read( BaseFileSystem fileSystem, string path,
		IReadOnlyDictionary<string, Func<string, bool>> banks )
	{
		var result = new Dictionary<string, Event[]>( StringComparer.OrdinalIgnoreCase );
		if ( !fileSystem.FileExists( path ) ) return result;

		using var stream = fileSystem.OpenRead( path );
		if ( stream.Length > 32 * 1024 * 1024 ) throw new InvalidDataException( "Wwise event catalog is too large." );
		using var reader = new StreamReader( stream );
		var document = new WwiseManifestXml( reader.ReadToEnd() ).Read();
		if ( document.Name != "SoundBanksInfo" || document.Child( "SoundBanks" ) is not { } soundBanks ) return result;
		foreach ( var bank in soundBanks.Children( "SoundBank" ) )
		{
			var name = bank.Child( "ShortName" )?.Value;
			if ( name is null || bank.Attribute( "Language" ) != "SFX" || !banks.TryGetValue( name, out var select ) ) continue;

			var events = new List<Event>();
			foreach ( var entry in bank.Child( "Events" )?.Children( "Event" ) ?? [] )
			{
				var eventName = entry.Attribute( "Name" );
				if ( string.IsNullOrWhiteSpace( eventName ) || select is not null && !select( eventName ) ||
					!TryReadId( entry, "Id", out var id ) ) continue;

				var routes = new List<Route>();
				ReadRoutes( entry.Child( "SwitchContainers" ), [], routes );
				var changes = new List<WwiseEventResource.StateChange>();
				foreach ( var change in entry.Child( "ActionSetState" )?.Children( "ActionSetStateEntry" ) ?? [] )
				{
					if ( TryReadId( change, "GroupId", out var group ) && TryReadId( change, "Id", out var state ) )
						changes.Add( new( group, state, change.Attribute( "Name" ) ) );
				}

				events.Add( new( id, eventName, entry.Attribute( "DurationType" ), Number( entry, "DurationMin" ),
					Number( entry, "DurationMax" ), Number( entry, "MaxAttenuation" ), ReadMedia( entry ), routes.ToArray(), changes.ToArray() ) );
			}
			result.TryAdd( name, events.ToArray() );
		}
		return result;
	}

	static void ReadRoutes( WwiseManifestXml.Element parent, WwiseEventResource.SwitchValue[] conditions, List<Route> routes )
	{
		foreach ( var container in parent?.Children( "SwitchContainer" ) ?? [] )
		{
			if ( container.Child( "SwitchValue" ) is not { } value || !TryReadId( value, "GroupId", out var group ) ||
				!TryReadId( value, "Id", out var id ) ) continue;

			WwiseEventResource.SwitchValue[] chain =
				[..conditions, new( group, id, value.Attribute( "GroupType" ), value.Attribute( "Default" ) == "true" )];
			var media = ReadMedia( container );
			if ( media.Length > 0 ) routes.Add( new( chain, media ) );
			ReadRoutes( container.Child( "Children" ), chain, routes );
		}
	}

	static uint[] ReadMedia( WwiseManifestXml.Element entry )
	{
		var ids = new List<uint>();
		foreach ( var reference in entry.Child( "MediaRefs" )?.Children( "MediaRef" ) ?? [] )
			if ( TryReadId( reference, "Id", out var id ) ) ids.Add( id );
		return ids.ToArray();
	}

	static bool TryReadId( WwiseManifestXml.Element element, string attribute, out uint id ) =>
		uint.TryParse( element.Attribute( attribute ), NumberStyles.None, CultureInfo.InvariantCulture, out id );

	static float? Number( WwiseManifestXml.Element element, string attribute ) =>
		float.TryParse( element.Attribute( attribute ), NumberStyles.Float, CultureInfo.InvariantCulture, out var number ) &&
		float.IsFinite( number ) ? number : null;
}
