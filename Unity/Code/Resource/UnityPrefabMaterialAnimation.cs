namespace UnityMount;

static class UnityPrefabMaterialAnimation
{
	const uint HashMask = 0x0fffffff;
	const uint Scalar = 0x80000000;
	static readonly uint SliceHeight = Hash( "_SliceHeight" );
	static readonly uint BandHeight = Hash( "_SliceBandHeight" );
	static readonly uint AlphaDepth = Hash( "_SliceAlphaDepth" );
	static readonly uint PrintBias = Hash( "_PrintBias" );
	static readonly uint SliceTransform = Hash( "_SliceAlphaTex_ST" );
	static readonly uint EmissionTint = Hash( "_EmColor" );
	static readonly uint PrintBoost = Hash( "_PrintBoost" );
	static readonly uint EmissionPower = Hash( "_EmPower" );
	static readonly uint FresnelBoost = Hash( "_FresnelBoost" );
	static readonly uint ExternalAlpha = Hash( "_ExternalAlpha" );
	const int ExternalAlphaSlot = 15;

	readonly record struct Channel( UnityAnimationClip.MaterialBinding Binding, int Slot );

	public static UnityMaterialAnimation.Clip BuildClip( UnityAnimationClip clip, bool looping, IReadOnlyDictionary<uint, int> paths,
		IReadOnlyList<GameObject> targets )
	{
		if ( clip.MaterialBindings.Count == 0 ) return null;
		var renderers = new Dictionary<Renderer, List<Channel>>();
		foreach ( var binding in clip.MaterialBindings )
		{
			if ( !paths.TryGetValue( binding.Path, out var index ) || DecodeSlot( binding.Attribute ) is not { } slot ) continue;
			foreach ( var renderer in targets[index].GetComponents<Renderer>( true ) )
			{
				if ( !Matches( renderer, binding.RendererClass, slot ) ) continue;
				if ( !renderers.TryGetValue( renderer, out var channels ) ) renderers.Add( renderer, channels = new() );
				channels.Add( new( binding, slot ) );
			}
		}

		return renderers.Count > 0 ? new( clip.Name, clip.Duration, clip.Rate, looping,
			renderers.Select( pair => Bake( clip, pair.Key, pair.Value ) ).ToArray() ) : null;
	}

	static bool Matches( Renderer renderer, int classId, int slot )
	{
		if ( !UnityPrefabAnimation.MatchesRenderer( renderer, classId ) ) return false;
		return renderer switch
		{
			LineRenderer line => SupportsSlot( line.Texturing.Material, slot ),
			ModelRenderer { Model: { } model } => model.Materials.Any( material => SupportsSlot( material, slot ) ),
			_ => false
		};
	}

	static bool SupportsSlot( Material material, int slot )
	{
		var shader = material?.ShaderName;
		return slot == ExternalAlphaSlot
			? shader is "shaders/unity/cloud_remap.shader" or "shaders/unity/opaque_cloud.shader"
			: shader == "shaders/unity/standard_surface.shader";
	}

	static UnityMaterialAnimation.Track Bake( UnityAnimationClip clip, Renderer renderer, List<Channel> channels )
	{
		var frames = new UnityMaterialAnimation.Sample[(int)MathF.Ceiling( clip.Duration * clip.Rate ) + 1];
		uint mask = 0;
		foreach ( var channel in channels ) mask |= 1u << channel.Slot;
		for ( var i = 0; i < frames.Length; i++ )
		{
			var time = clip.TimeAtFrame( i );
			var slice = Vector4.Zero;
			var transform = Vector4.Zero;
			var tint = Vector4.Zero;
			var boost = 0f;
			var emissionPower = 0f;
			var fresnelBoost = 0f;
			var alpha = 0f;
			foreach ( var (binding, slot) in channels )
			{
				var value = clip.SampleMaterial( binding, time );
				if ( !float.IsFinite( value ) ) continue;
				switch ( slot )
				{
					case < 4: slice[slot] = value; break;
					case < 8: transform[slot - 4] = value; break;
					case < 12: tint[slot - 8] = value; break;
					case 12: boost = value; break;
					case 13: emissionPower = value; break;
					case 14: fresnelBoost = value; break;
					case ExternalAlphaSlot: alpha = value; break;
				}
			}
			frames[i] = new( slice, transform, tint, boost, emissionPower, fresnelBoost, alpha );
		}
		return new( renderer, mask, frames );
	}

	// Unity stores the property hash in the low 28 bits and the component in the high bits.
	static int? DecodeSlot( uint attribute )
	{
		var hash = attribute & HashMask;
		if ( (attribute & Scalar) != 0 )
		{
			if ( hash == SliceHeight ) return 0;
			if ( hash == BandHeight ) return 1;
			if ( hash == AlphaDepth ) return 2;
			if ( hash == PrintBias ) return 3;
			if ( hash == PrintBoost ) return 12;
			if ( hash == EmissionPower ) return 13;
			if ( hash == FresnelBoost ) return 14;
			if ( hash == ExternalAlpha ) return ExternalAlphaSlot;
			return null;
		}

		var channel = (int)((attribute >> 28) & 3);
		if ( hash == SliceTransform ) return 4 + channel;
		if ( hash == EmissionTint ) return 8 + channel;
		return null;
	}

	static uint Hash( string name ) => UnitySkin.HashPath( name ) & HashMask;
}
