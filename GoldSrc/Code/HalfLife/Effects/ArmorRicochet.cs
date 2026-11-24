namespace GoldSrc.HalfLife;

[Hide]
public sealed class ArmorRicochet : Component
{
	const float Brightness = 200f / 255;
	SpriteRenderer _sprite;
	TimeSince _age;

	[Rpc.Broadcast( NetFlags.HostOnly )]
	internal static void Show( Vector3 point, float scale, string mountIdent = "hl1" )
	{
		if ( Application.IsDedicatedServer || Sandbox.Mounting.Directory.Get( mountIdent ) is not GameMount mount ) return;
		NpcAudio.Play( mount.NpcAudio.Find( "weapons/ric" ), point );
		if ( mount.GetIndexedSprite( "richo1" ) is not { } sprite ) return;

		var go = new GameObject( false, "Armor ricochet" );
		go.NetworkMode = NetworkMode.Never;
		go.WorldPosition = point;
		go.WorldRotation = Rotation.FromRoll( Game.Random.Int( 0, 7 ) * 45 );
		var renderer = go.AddComponent<SpriteRenderer>();
		renderer.Sprite = sprite;
		renderer.Size = 32 * scale;
		renderer.PlaybackSpeed = 0;
		renderer.Additive = true;
		renderer.Color = Color.White.WithAlpha( Brightness );
		go.AddComponent<ArmorRicochet>()._sprite = renderer;
		go.Enabled = true;
	}

	protected override void OnEnabled() => _age = 0;

	protected override void OnUpdate()
	{
		var alpha = 1 - System.MathF.Max( 0, _age - 0.1f ) * 8;
		if ( alpha <= 0 ) GameObject.Destroy();
		else _sprite.Color = Color.White.WithAlpha( Brightness * alpha );
	}
}
