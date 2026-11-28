using UnityMount.Wwise;

namespace UnityMount;

/// <summary>Authored event routes and the installed audio resources they reference.</summary>
[AssetType( Name = "Wwise Event Definition", Extension = "wwevent", Category = "Sounds" )]
public sealed class WwiseEventResource : GameResource
{
	public sealed record MediaReference( uint Id, string Path );
	public sealed record SwitchValue( uint GroupId, uint Id, string GroupType, bool IsDefault );
	public sealed record SwitchRoute( SwitchValue[] Conditions, MediaReference[] Media );
	public sealed record StateChange( uint GroupId, uint Id, string Name );

	public string EventName { get; set; }
	public uint EventId { get; set; }
	public string DurationType { get; set; }
	public float? MinDuration { get; set; }
	public float? MaxDuration { get; set; }
	public float? MaxAttenuation { get; set; }
	public MediaReference[] Media { get; set; } = [];
	public SwitchRoute[] SwitchRoutes { get; set; } = [];
	public StateChange[] StateChanges { get; set; } = [];
}

sealed class WwiseEventLoader( WwiseEventCatalog.Event source, string ident, IReadOnlyDictionary<uint, string> bankMedia,
	IReadOnlyDictionary<uint, string> sharedMedia ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		return new WwiseEventResource
		{
			EventName = source.Name,
			EventId = source.Id,
			DurationType = source.DurationType,
			MinDuration = source.MinDuration,
			MaxDuration = source.MaxDuration,
			MaxAttenuation = source.MaxAttenuation,
			Media = Bind( source.Media ),
			SwitchRoutes = source.Routes.Select( route => new WwiseEventResource.SwitchRoute( route.Conditions, Bind( route.Media ) ) ).ToArray(),
			StateChanges = source.StateChanges
		};
	}

	WwiseEventResource.MediaReference[] Bind( uint[] ids ) => ids.Select( id =>
	{
		var path = bankMedia.GetValueOrDefault( id ) ?? sharedMedia.GetValueOrDefault( id );
		return new WwiseEventResource.MediaReference( id, path is null ? null : $"mount://{ident}/{path}" );
	} ).ToArray();
}
