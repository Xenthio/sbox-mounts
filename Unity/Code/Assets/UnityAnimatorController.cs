namespace UnityMount;

readonly record struct UnityAnimationSource( UnityAssetFile File, SerializedFile.Object Asset );

sealed record UnityControllerAnimations( UnityAnimationSource?[] Slots, UnityBlendTreeData[] BlendTrees, UnityAnimatorControllerData Graph )
{
	public UnityAnimationSource[] Clips { get; } = Slots.Where( source => source.HasValue )
		.Select( source => source.Value ).Distinct().ToArray();
}

/// <summary>Reads the authored clip references from a Unity Animator Controller.</summary>
static class UnityAnimatorController
{
	public const int ClassId = 91;
	public const int OverrideClassId = 221;

	public readonly record struct ClipOverride( UnityReference Original, UnityReference Replacement );

	public static UnityReference[] ReadClips( UnityReader reader, UnityTypeTree tree )
	{
		tree.SeekField( reader, "m_AnimationClips" );
		return UnityReference.ReadArray( reader );
	}

	public static (UnityReference Controller, ClipOverride[] Clips) ReadOverrides( UnityReader reader, UnityTypeTree tree )
	{
		tree.SeekField( reader, "m_Controller" );
		var controller = UnityReference.Read( reader );
		reader.Position = 0;
		var clips = new List<ClipOverride>();
		tree.SeekField( reader, "m_Clips" ).VisitArray( reader, (item, start) =>
		{
			item.SeekField( reader, "m_OriginalClip" );
			var original = UnityReference.Read( reader );
			reader.Position = start;
			item.SeekField( reader, "m_OverrideClip" );
			clips.Add( new( original, UnityReference.Read( reader ) ) );
		} );
		return (controller, clips.ToArray());
	}
}
