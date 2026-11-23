using GoldSrc.HalfLife;

namespace GoldSrc.CryOfFear;

[Hide]
[Library]
public sealed class AcidSpit : NpcProjectile
{
	internal override float LaunchSpeed => 900;
	internal override string HitTag => "generic";

	protected override void OnStart()
	{
		if ( Sandbox.Mounting.Directory.Get( "cof" ) is not GameMount mount || mount.GetIndexedSprite( "flier_spit" ) is not { } sprite )
			return;

		var renderer = GameObject.AddComponent<SpriteRenderer>();
		renderer.Sprite = sprite;
		renderer.Size = new Vector2( sprite.Animations[0].Frames[0].Texture.Width, sprite.Animations[0].Frames[0].Texture.Height ) * 0.5f;
		renderer.PlaybackSpeed = 10f / sprite.Animations[0].FrameRate;
	}

	internal override void OnImpact( IDamageable victim, Vector3 position, Vector3 normal )
	{
		base.OnImpact( victim, position, normal );
		if ( Sandbox.Mounting.Directory.Get( "cof" ) is GameMount mount )
			mount.NpcAudio.Emit( "flygare/flygare_acid_hit.wav", position, pitch: Game.Random.Float( 0.9f, 1.1f ) );
		if ( victim is null ) PlaceMark( position, normal, Game.Random.Int( 1, 6 ) );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void PlaceMark( Vector3 position, Vector3 normal, int variant )
	{
		if ( Sandbox.Mounting.Directory.Get( "cof" ) is GameMount mount )
			ImpactDecal.Place( position, normal, $"blood{variant}", 0, mount );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	internal static void MuzzleDroplets( Vector3 position )
	{
		if ( Sandbox.Mounting.Directory.Get( "cof" ) is GameMount mount )
			BloodEffects.EmitDroplets( mount, position, 4, (int)Npc.BloodColor.Red );
	}
}
