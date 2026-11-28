namespace UnityMount;

readonly record struct UnityPrintAnimationData( Curve Curve, float Duration, Vector2 Height, Vector2 Bias, Vector2? FlowPower,
	bool PlayOnStart, bool Paused, bool DisableWhenFinished, bool IgnoreNestedControllers );

/// <summary>Applies a mounted prefab's authored print reveal to its Standard surfaces.</summary>
[Title( "Mounted Print Animation" )]
public sealed class UnityPrintAnimation : Component, Component.DontExecuteOnServer
{
	const string AttributeName = "UnityPrintState";

	[Property] public Curve Curve { get; set; }
	[Property] public float Duration { get; set; }
	[Property] public Vector2 Height { get; set; }
	[Property] public Vector2 Bias { get; set; }
	[Property] public Vector2 FlowPower { get; set; } = new( -1, -1 );
	[Property] public bool PlayOnStart { get; set; }
	[Property] public bool Paused { get; set; }
	[Property] public bool DisableWhenFinished { get; set; }
	[Property] public bool IgnoreNestedControllers { get; set; }

	ModelRenderer[] _renderers = [];
	float _time;
	bool _playing;

	protected override void OnEnabled()
	{
		_renderers = GameObject.GetComponentsInChildren<ModelRenderer>( true )
			.Where( renderer => !IgnoreNestedControllers || renderer.GameObject == GameObject ||
				renderer.GameObject.GetComponent<UnityPrintAnimation>( true ) is null ).ToArray();
		if ( PlayOnStart ) Play();
		else Stop();
	}

	public void Play()
	{
		_time = 0;
		_playing = Duration > 0;
		Apply( Curve.Evaluate( 0 ) );
	}

	public void Stop()
	{
		_playing = false;
		Apply( 1 );
	}

	protected override void OnUpdate()
	{
		if ( !_playing || Paused || _time >= Duration ) return;
		_time = Math.Min( _time + Time.Delta, Duration );
		if ( _time >= Duration && DisableWhenFinished ) Stop();
		else Apply( Curve.Evaluate( _time / Duration ) );
	}

	protected override void OnDisabled() => Stop();

	void Apply( float weight )
	{
		var height = MathX.Lerp( Height.x, Height.y, weight, clamp: false );
		var bias = MathX.Lerp( Bias.x, Bias.y, weight, clamp: false );
		var power = MathX.Lerp( FlowPower.x, FlowPower.y, weight, clamp: false );
		foreach ( var renderer in _renderers )
			if ( renderer.IsValid() ) renderer.Attributes.Set( AttributeName, new Vector4( height, bias, _playing ? 1 : -1, power ) );
	}
}
