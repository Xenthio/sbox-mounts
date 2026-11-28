namespace UnityMount;

[Title( "Mounted Omni Effect" )]
public sealed class UnityOmniEffect : Component, Component.DontExecuteOnServer
{
	public sealed record Element( GameObject Emitter, bool Emit, float? MaximumRadius, float BonusEmission, Component RendererOverride = null );
	public sealed record Group( Element[] Elements );

	[Property, Hide] public Group[] Groups { get; set; } = [];

	protected override void OnStart()
	{
		var radius = MathF.Abs( LocalScale.x );
		foreach ( var group in Groups )
		{
			var minimum = 0f;
			foreach ( var element in group.Elements )
			{
				var maximum = element.MaximumRadius ?? float.PositiveInfinity;
				if ( radius <= maximum )
				{
					Apply( element, radius - minimum );
					break;
				}
				minimum = maximum;
			}
		}
	}

	static void Apply( Element element, float bonusRadius )
	{
		var node = element.Emitter;
		if ( !node.IsValid() ) return;
		if ( element.RendererOverride.IsValid() )
		{
			foreach ( var renderer in node.Components.GetAll() )
				if ( renderer is UnityParticleBillboards or ParticleSpriteRenderer or UnityParticleMeshes )
					renderer.Enabled = renderer == element.RendererOverride;
		}
		if ( element.Emit && node.GetComponent<UnityShapeEmitter>( true ) is { Bursts.Length: > 0 } emitter &&
			node.GetComponent<ParticleEffect>( true ) is { } effect )
		{
			var bursts = emitter.Bursts.ToArray();
			var count = (int)bursts[0].Count.Evaluate( 0, 1 ) + (int)(bonusRadius * element.BonusEmission);
			bursts[0] = bursts[0] with { Time = 0f, Count = Math.Clamp( count, 0, effect.MaxParticles ) };
			emitter.Bursts = bursts;
		}
		node.Enabled = element.Emit;
	}
}
