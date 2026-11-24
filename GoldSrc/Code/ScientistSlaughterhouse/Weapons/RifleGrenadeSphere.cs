using System.Runtime.CompilerServices;

namespace GoldSrc.ScientistSlaughterhouse;

[Hide]
public sealed class RifleGrenadeSphere : Component
{
	static readonly ConditionalWeakTable<Sprite, Material[]> Materials = new();

	ModelRenderer _renderer;
	Material[] _frames;
	Color _tint;
	float _modelDiameter;
	float _maxRadius, _growth, _fadeDelay, _fadeRate;
	TimeSince _age;

	internal static void Show( Vector3 origin, Color tint, float maxRadius, float growth, float fadeDelay, float fadeRate, GameMount mount )
	{
		if ( mount.GetIndexedSprite( "blastsphere" ) is not { } sprite ) return;
		var model = Model.Load( "models/dev/sphere.vmdl" );
		if ( !model.IsValid() || model.IsError || model.Bounds.Size.x <= 0 ) return;

		var visual = new GameObject( false, "Rifle grenade blast sphere" ) { WorldPosition = origin };
		visual.NetworkMode = NetworkMode.Never;
		var effect = visual.AddComponent<RifleGrenadeSphere>();
		effect._frames = Materials.GetValue( sprite, CreateMaterials );
		effect._renderer = visual.AddComponent<ModelRenderer>();
		effect._renderer.Model = model;
		effect._renderer.MaterialOverride = effect._frames[0];
		effect._renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		effect._renderer.Tint = tint;
		effect._tint = tint;
		effect._modelDiameter = model.Bounds.Size.x;
		effect._maxRadius = maxRadius;
		effect._growth = growth;
		effect._fadeDelay = fadeDelay;
		effect._fadeRate = fadeRate;
		effect._age = 0;
		visual.WorldScale = Vector3.One * (2 / effect._modelDiameter);
		visual.Enabled = true;
	}

	static Material[] CreateMaterials( Sprite sprite ) =>
		sprite.Animations[0].Frames.Select( (frame, index) =>
		{
			var material = Material.Create( $"ssh_blastsphere_{index}", "shaders/goldsrc_generic.shader" );
			material.SetFeature( "F_TRANSLUCENT", 1 );
			material.SetFeature( "F_ADDITIVE_BLEND", 1 );
			material.Set( "Color", frame.Texture );
			material.Set( "Fullbright", Texture.White );
			return material;
		} ).ToArray();

	protected override void OnUpdate()
	{
		var alpha = 1 - System.MathF.Max( _age - _fadeDelay, 0 ) * _fadeRate;
		if ( alpha <= 0 )
		{
			GameObject.Destroy();
			return;
		}

		var radius = System.MathF.Min( 1 + _age * _growth, _maxRadius );
		WorldScale = Vector3.One * (2 * radius / _modelDiameter);
		_renderer.Tint = _tint.WithAlpha( alpha );

		var lastFrame = _frames.Length - 1;
		if ( lastFrame == 0 ) return;
		var frame = (int)(_age * 10) % (2 * lastFrame);
		var material = _frames[lastFrame - System.Math.Abs( lastFrame - frame )];
		if ( _renderer.MaterialOverride != material ) _renderer.MaterialOverride = material;
	}
}
