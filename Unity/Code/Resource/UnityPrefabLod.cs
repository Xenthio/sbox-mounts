namespace UnityMount;

sealed record UnityPrefabLod( Vector3 Center, float Size, UnityPrefabLod.Level[] Levels )
{
	public sealed record Level( float ScreenHeight, UnityAssetPointer[] Renderers, bool HasAuthoredRenderers );
}
