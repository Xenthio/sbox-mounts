using GoldSrc.HalfLife;

namespace GoldSrc.HalfLifeMMod;

[Hide]
public sealed class HandGrenadeProjectile : TimedGrenade
{
	[Sync( SyncFlags.FromHost )] internal string FlightAnimation { get; set; }

	SkinnedModelRenderer _renderer;

	protected override void OnStart()
	{
		if ( Application.IsDedicatedServer || GetComponent<ModelRenderer>() is not { } model ) return;
		model.Enabled = false;
		_renderer = GameObject.AddComponent<SkinnedModelRenderer>();
		_renderer.Model = model.Model;
		_renderer.SetBodyGroup( "studio", 1 );
		_renderer.UseAnimGraph = false;
		_renderer.Sequence.Looping = true;
	}

	protected override void OnUpdate()
	{
		if ( !_renderer.IsValid() || string.IsNullOrEmpty( FlightAnimation ) ) return;
		if ( _renderer.Sequence.Name != FlightAnimation ) _renderer.Sequence.Name = FlightAnimation;
		var speed = GetComponent<Rigidbody>()?.Velocity.Length ?? 0;
		_renderer.Sequence.PlaybackRate = FlightAnimation == "roll1" ? (speed < 100 ? 0 : System.MathF.Min( speed / 200, 1 )) : 1.5f;
	}

	internal override void OnTumble( Rigidbody body )
	{
		if ( FlightAnimation == "roll1" || body.Velocity.z > 30 ) return;
		var ground = Scene.Trace.Ray( WorldPosition, WorldPosition + Vector3.Down * 5 ).IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "player", "npc", "trigger" ).Run();
		if ( ground.Hit && ground.Normal.z > 0.7f ) FlightAnimation = "roll1";
	}
}
