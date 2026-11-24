using GoldSrc.HalfLife;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public class CrossbowDart : CrossbowBolt
{
	internal override bool Explosive => false;
	protected virtual Color TrailColor => Color.White;

	protected override void OnStart()
	{
		base.OnStart();
		if ( !Application.IsDedicatedServer && ModelMount is { } mount )
			ProjectileTrail.Attach( GameObject, mount, "Crossbow dart trail", "laserdot", 0.4f, 1, TrailColor.WithAlpha( 128f / 255 ) );
	}
}

[Hide]
public sealed class ExplosiveDart : CrossbowDart
{
	protected override Color TrailColor => new Color32( 255, 128, 32, 255 );

	internal override void HandleContact( SceneTraceResult hit )
	{
		var origin = hit.EndPosition + hit.Normal;
		Blast.DamageInRadius( GameObject, Shooter, origin, Damage, 128, "blast", "alwaysgib" );
		Explode( origin );
		GameObject.Destroy();
	}
}

[Hide]
public sealed class FlareDart : CrossbowDart
{
	protected override Color TrailColor => new Color32( 255, 225, 64, 255 );
	internal override float PlayerDamage => 5;

	internal override void HandleContact( SceneTraceResult hit )
	{
		base.HandleContact( hit );
		if ( !Enabled ) ShowFlare( WorldPosition );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowFlare( Vector3 origin )
	{
		if ( Application.IsDedicatedServer ) return;
		var light = new GameObject( false, "Crossbow flare" ) { WorldPosition = origin, NetworkMode = NetworkMode.Never };
		var glow = light.AddComponent<PointLight>();
		glow.Radius = 640;
		glow.LightColor = new Color32( 255, 192, 64, 255 );
		glow.Shadows = false;
		light.AddComponent<TemporaryEffect>().DestroyAfterSeconds = 10;
		light.Enabled = true;
	}
}
