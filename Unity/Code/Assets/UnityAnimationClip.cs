using Key = UnityMount.UnityAnimatorParameterCurve.Key;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector = System.Numerics.Vector3;

namespace UnityMount;

/// <summary>Unity 2021.3 player animation channels, stored as streamed, dense and constant curves.</summary>
sealed class UnityAnimationClip( string name, float rate, float start, float stop, bool looping, UnityAnimationClip.Binding[] bindings,
	UnityAnimationClip.MaterialBinding[] materialBindings, UnityAnimationClip.LightBinding[] lightBindings,
	UnityAnimationClip.ScriptBinding[] scriptBindings, UnityAnimationClip.Binding[] rootBindings,
	UnityAnimationClip.VisibilityBinding[] visibilityBindings, UnityAnimationClip.ParameterBinding[] parameterBindings,
	int? rootTranslation, int[] animatorRootChannels, float cycleOffset,
	Key[][] streamed, UnityAnimationClip.Dense dense,
	float[] constants, float[] additiveReference, UnityAnimationClip.ClipEvent[] events )
{
	public const int ClassId = 74;
	public string Name => name;
	public float Rate => rate;
	public float Duration => stop - start;
	public bool Looping => looping;
	public IReadOnlyList<ClipEvent> Events => events;
	public IReadOnlyList<MaterialBinding> MaterialBindings => materialBindings;
	public IReadOnlyList<LightBinding> LightBindings => lightBindings;
	public IReadOnlyList<ScriptBinding> ScriptBindings => scriptBindings;
	public int? RootTranslation => rootTranslation;
	public IReadOnlyList<Binding> RootBindings => rootBindings;
	public IReadOnlyList<VisibilityBinding> VisibilityBindings => visibilityBindings;
	public IReadOnlyList<ParameterBinding> ParameterBindings => parameterBindings;
	public bool HasTransformBindings => bindings.Length > 0;
	public bool HasAdditiveReference => additiveReference.Length > 0;
	public float CycleOffset => cycleOffset;
	public bool HasAnimatorRoot => animatorRootChannels.Any( channel => channel >= 0 );
	public int FrameCount => Math.Clamp( (int)MathF.Round( (stop - start) * Rate ) + 1, 1, 6000 );

	public readonly record struct Binding( uint Path, uint Attribute, int Channel, bool RawEuler );
	public readonly record struct ParameterBinding( uint Hash, int Channel );
	public readonly record struct VisibilityBinding( uint Path, int RendererClass, int Channel );
	public readonly record struct MaterialBinding( uint Path, uint Attribute, int RendererClass, int Channel );
	public readonly record struct LightBinding( uint Path, int Channel );
	public readonly record struct ScriptBinding( uint Attribute, UnityReference Script, int Channel );
	public readonly record struct Dense( int Frames, int Channels, float Rate, float Start, float[] Samples );
	public readonly record struct ClipEvent( float Time, string Function, string Data, UnityReference Object, float FloatParameter,
		int IntParameter, int MessageOptions );

	static readonly uint RendererEnabled = UnitySkin.HashPath( "m_Enabled" );
	static readonly uint LightIntensity = UnitySkin.HashPath( "m_Intensity" );
	public static readonly uint SplineProgress = UnitySkin.HashPath( "normalizedPositionAlongCurve" );
	public static readonly uint CameraFieldOfView = UnitySkin.HashPath( "fovOverride" );
	public static readonly uint CameraEnabled = UnitySkin.HashPath( "m_Enabled" );

	public static UnityAnimationClip Read( UnityReader reader, UnityTypeTree tree, bool lightOnly = false )
	{
		tree.SeekField( reader, "m_Name" );
		var name = reader.ReadAlignedString();
		reader.Position = 0;
		tree.SeekField( reader, "m_SampleRate" );
		var rate = reader.ReadSingle();
		reader.Position = 0;
		tree.SeekField( reader, "m_MuscleClipSize" );
		if ( reader.ReadUInt32() == 0 || !float.IsFinite( rate ) || rate is < 1 or > 240 ) return null;

		reader.Position = 0;
		var bindingTree = tree.SeekField( reader, "m_ClipBindingConstant" );
		bindingTree.SeekField( reader, "genericBindings" );
		var count = reader.ReadInt32();
		if ( count < 0 || count > reader.Remaining / 28 ) throw new InvalidDataException( "Invalid Unity animation binding count." );
		var bindings = new List<Binding>();
		var root = new List<Binding>();
		var visibility = new List<VisibilityBinding>();
		var parameters = new List<ParameterBinding>();
		var materials = new List<MaterialBinding>();
		var lights = new List<LightBinding>();
		var scripts = new List<ScriptBinding>();
		var animatorRoot = new[] { -1, -1, -1, -1, -1, -1, -1 };
		int? translation = null;
		var channels = 0;
		for ( var i = 0; i < count; i++ )
		{
			var path = reader.ReadUInt32();
			var attribute = reader.ReadUInt32();
			var script = UnityReference.Read( reader );
			var classId = reader.ReadInt32();
			var custom = reader.ReadByte();
			var pointer = reader.ReadBoolean();
			reader.Align( 4 );

			var transform = classId == UnitySceneObjects.TransformClassId;
			var components = transform ? attribute switch { 1 or 3 or 4 => 3, 2 => 4, _ => 1 } : 1;
			var rawEuler = attribute == 4 && custom == 4;
			// Unsupported custom transforms still occupy vector channels in the clip stream.
			if ( transform && !pointer && (custom == 0 || rawEuler) && components > 1 )
				(path == 0 ? root : bindings).Add( new( path, attribute, channels, rawEuler ) );
			if ( transform && path == 0 && attribute == 1 && custom == 0 && !pointer )
				translation = channels;
			if ( !pointer && custom == 22 && classId is UnityMesh.RendererClassId or UnitySkin.RendererClassId or UnityLine.ClassId )
				materials.Add( new( path, attribute, classId, channels ) );
			// The Animator stores RootT and RootQ as seven independent scalar bindings.
			if ( !pointer && path == 0 && classId == UnityAvatar.AnimatorClassId && custom == 8 && attribute is >= 7 and <= 13 )
				animatorRoot[attribute - 7] = channels;
			if ( !pointer && path == 0 && classId == UnityAvatar.AnimatorClassId && custom == 0 )
				parameters.Add( new( attribute, channels ) );
			if ( !pointer && custom == 0 && attribute == RendererEnabled &&
				classId is UnityMesh.RendererClassId or UnitySkin.RendererClassId or UnityLine.ClassId )
				visibility.Add( new( path, classId, channels ) );
			if ( !pointer && classId == UnityLight.ClassId && custom == 25 && attribute == LightIntensity )
				lights.Add( new( path, channels ) );
			if ( !pointer && classId == SerializedFile.MonoBehaviourClassId && path == 0 &&
				((custom == 0 && (attribute == SplineProgress || attribute == CameraFieldOfView)) || (custom == 24 && attribute == CameraEnabled)) )
				scripts.Add( new( attribute, script, channels ) );
			channels += components;
		}
		if ( lightOnly ? lights.Count == 0 : bindings.Count + materials.Count + lights.Count + scripts.Count + root.Count + visibility.Count + parameters.Count == 0 && animatorRoot.All( channel => channel < 0 ) ) return null;

		reader.Position = 0;
		var muscle = tree.SeekField( reader, "m_MuscleClip" );
		var muscleStart = reader.Position;
		muscle.SeekField( reader, "m_StartTime" );
		var start = reader.ReadSingle();
		var stop = reader.ReadSingle();
		if ( !float.IsFinite( start ) || !float.IsFinite( stop ) || stop < start || (stop - start) * rate >= 6000 ) return null;
		reader.Position = muscleStart;
		muscle.SeekField( reader, "m_LoopTime" );
		var looping = reader.ReadBoolean();
		var phase = 0f;
		if ( muscle.HasField( "m_CycleOffset" ) )
		{
			reader.Position = muscleStart;
			muscle.SeekField( reader, "m_CycleOffset" );
			phase = reader.ReadSingle();
		}

		reader.Position = muscleStart;
		var clip = muscle.SeekField( reader, "m_Clip" ).SeekField( reader, "data" );
		clip.SeekField( reader, "m_StreamedClip" );
		var streamed = ReadStreamed( reader );
		var frames = reader.ReadInt32();
		var denseChannels = reader.ReadInt32();
		var denseRate = reader.ReadSingle();
		var denseStart = reader.ReadSingle();
		var samples = ReadFloats( reader );
		if ( frames < 0 || denseChannels < 0 || (long)frames * denseChannels != samples.Length ||
			denseChannels > 0 && (frames == 0 || !float.IsFinite( denseRate ) || denseRate <= 0 || !float.IsFinite( denseStart )) )
			throw new InvalidDataException( "Invalid Unity dense animation samples." );
		var dense = new Dense( frames, denseChannels, denseRate, denseStart, samples );
		var constants = ReadFloats( reader );
		if ( channels != streamed.Length + dense.Channels + constants.Length ) return null;
		float[] reference = [];
		if ( !lightOnly && muscle.HasField( "m_ValueArrayReferencePose" ) )
		{
			reader.Position = muscleStart;
			muscle.SeekField( reader, "m_ValueArrayReferencePose" );
			var values = ReadFloats( reader );
			if ( values.Length == channels && values.All( float.IsFinite ) ) reference = values;
		}

		return new( name, rate, start, stop, looping, bindings.ToArray(), materials.ToArray(), lights.ToArray(), scripts.ToArray(), root.ToArray(),
			visibility.ToArray(), parameters.ToArray(), translation, animatorRoot, float.IsFinite( phase ) ? phase : 0, streamed, dense,
			constants, reference, ReadEvents( reader, tree ) );
	}

	static ClipEvent[] ReadEvents( UnityReader reader, UnityTypeTree tree )
	{
		if ( !tree.HasField( "m_Events" ) ) return [];
		reader.Position = 0;
		var array = tree.SeekField( reader, "m_Events" );
		var events = new List<ClipEvent>();
		array.VisitArray( reader, ( _, _ ) =>
		{
			var time = reader.ReadSingle();
			var function = reader.ReadAlignedString();
			var data = reader.ReadAlignedString();
			var reference = UnityReference.Read( reader );
			var number = reader.ReadSingle();
			var integer = reader.ReadInt32();
			var options = reader.ReadInt32();
			if ( float.IsFinite( time ) && time >= 0 && float.IsFinite( number ) && !string.IsNullOrEmpty( function ) )
				events.Add( new( time, function, data, reference, number, integer, options ) );
		} );
		return events.OrderBy( entry => entry.Time ).ToArray();
	}

	public (int Joint, Binding Binding)[] Bind( IReadOnlyDictionary<uint, int> paths ) =>
		bindings.Where( binding => paths.ContainsKey( binding.Path ) )
			.Select( binding => (paths[binding.Path], binding) ).ToArray();

	public float TimeAtFrame( int frame ) => Math.Min( stop, start + frame / Rate );
	public float SampleMaterial( MaterialBinding binding, float time ) => Sample( binding.Channel, time );

	public float[] SampleLight( LightBinding binding ) => SampleScalarFrames( binding.Channel );

	public float[] SampleScalarFrames( int channel )
	{
		var frames = new float[(int)MathF.Ceiling( Duration * Rate ) + 1];
		for ( var i = 0; i < frames.Length; i++ )
			frames[i] = Sample( channel, TimeAtFrame( i ) );
		return frames;
	}

	public NumericsVector SampleVector( int channel, float time, bool referencePose = false ) =>
		new( Sample( channel, time, referencePose ), Sample( channel + 1, time, referencePose ), Sample( channel + 2, time, referencePose ) );

	public Transform? SampleAnimatorRoot( float time )
	{
		var position = UnityCoordinates.Position( new( ReadChannel( 0 ), ReadChannel( 1 ), ReadChannel( 2 ) ) );
		var rotation = new NumericsQuaternion( ReadChannel( 3 ), ReadChannel( 4 ), ReadChannel( 5 ), ReadChannel( 6, 1 ) );
		if ( !float.IsFinite( position.LengthSquared ) || !float.IsFinite( rotation.LengthSquared() ) || rotation.LengthSquared() <= 0 )
			return null;
		return new( position, UnityCoordinates.RotationFor( NumericsQuaternion.Normalize( rotation ) ) );

		float ReadChannel( int component, float fallback = 0 ) =>
			animatorRootChannels[component] is >= 0 and var channel ? Sample( channel, time ) : fallback;
	}

	public NumericsQuaternion SampleRotation( Binding binding, float time, bool referencePose = false )
	{
		if ( binding.Attribute == 2 )
		{
			var value = SampleVector( binding.Channel, time, referencePose );
			var raw = new NumericsQuaternion( value, Sample( binding.Channel + 3, time, referencePose ) );
			return raw.LengthSquared() > 0 ? NumericsQuaternion.Normalize( raw ) : NumericsQuaternion.Identity;
		}

		var euler = SampleVector( binding.Channel, time, referencePose ) * (MathF.PI / 180);
		var x = NumericsQuaternion.CreateFromAxisAngle( NumericsVector.UnitX, euler.X );
		var y = NumericsQuaternion.CreateFromAxisAngle( NumericsVector.UnitY, euler.Y );
		var z = NumericsQuaternion.CreateFromAxisAngle( NumericsVector.UnitZ, euler.Z );
		// Raw local Euler curves use Unity's transform rotation order.
		return binding.RawEuler ? y * x * z : z * y * x;
	}

	public UnityAnimatorParameterCurve BindParameterCurve( ParameterBinding binding, string parameter )
	{
		var curve = new UnityAnimatorParameterCurve { Sequence = Name, Parameter = parameter, ClipStart = start, Duration = Duration };
		var channel = binding.Channel;
		if ( channel < streamed.Length )
		{
			var keys = streamed[channel].Where( key => !float.IsPositiveInfinity( key.Time ) ).ToArray();
			if ( keys.Length == 0 || keys.Any( key => !float.IsFinite( key.Value ) ) ) return null;

			for ( var i = 0; i < keys.Length; i++ )
			{
				var key = keys[i];
				// Sentinel and non-finite slopes evaluate as holds. Store only finite metadata.
				if ( !float.IsFinite( key.Time ) || !float.IsFinite( key.Cubic ) ||
					!float.IsFinite( key.Quadratic ) || !float.IsFinite( key.Linear ) )
					keys[i] = new( MathF.Max( key.Time, float.MinValue ), 0, 0, 0, key.Value );
			}
			return curve with { Keys = keys };
		}

		channel -= streamed.Length;
		if ( channel < dense.Channels )
		{
			var samples = new float[dense.Frames];
			for ( var i = 0; i < samples.Length; i++ ) samples[i] = dense.Samples[i * dense.Channels + channel];
			return samples.All( float.IsFinite ) && float.IsFinite( dense.Start )
				? curve with { DenseStart = dense.Start, DenseRate = dense.Rate, Samples = samples } : null;
		}

		var value = constants[channel - dense.Channels];
		return float.IsFinite( value ) ? curve with { Samples = [value] } : null;
	}

	float Sample( int channel, float time, bool referencePose = false )
	{
		if ( referencePose ) return additiveReference[channel];
		if ( channel < streamed.Length ) return UnityAnimatorParameterCurve.SampleStreamed( streamed[channel], time );

		channel -= streamed.Length;
		if ( channel < dense.Channels )
		{
			var sample = Math.Clamp( (time - dense.Start) * dense.Rate, 0, dense.Frames - 1 );
			var first = (int)sample;
			var next = Math.Min( first + 1, dense.Frames - 1 );
			var a = dense.Samples[first * dense.Channels + channel];
			var b = dense.Samples[next * dense.Channels + channel];
			return a + (b - a) * (sample - first);
		}

		return constants[channel - dense.Channels];
	}

	static Key[][] ReadStreamed( UnityReader reader )
	{
		var wordCount = reader.ReadInt32();
		if ( wordCount < 0 || wordCount > (reader.Remaining - 4) / 4 ) throw new InvalidDataException( "Invalid Unity streamed animation length." );
		var end = reader.Position + 4L * wordCount;
		var start = reader.Position;
		reader.Position = end;
		var curveCount = reader.ReadInt32();
		if ( curveCount is < 0 or > 32768 ) throw new InvalidDataException( "Invalid Unity streamed animation curve count." );
		var keys = new List<Key>[curveCount];
		reader.Position = start;
		var lastTime = float.NegativeInfinity;
		while ( reader.Position < end )
		{
			if ( end - reader.Position < 8 ) throw new InvalidDataException( "Truncated Unity animation frame." );
			var time = reader.ReadSingle();
			var count = reader.ReadInt32();
			if ( float.IsNaN( time ) || time < lastTime || count < 0 || count > (end - reader.Position) / 20 )
				throw new InvalidDataException( "Invalid Unity streamed animation frame." );
			lastTime = time;
			for ( var i = 0; i < count; i++ )
			{
				var channel = reader.ReadInt32();
				if ( (uint)channel >= keys.Length ) throw new InvalidDataException( "Invalid Unity animation curve index." );
				(keys[channel] ??= new()).Add( new( time, reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() ) );
			}
		}
		reader.Skip( 4 ); // Curve count.
		return keys.Select( curve => curve?.ToArray() ?? [] ).ToArray();
	}

	static float[] ReadFloats( UnityReader reader )
	{
		var count = reader.ReadInt32();
		if ( count < 0 || count > reader.Remaining / 4 ) throw new InvalidDataException( "Invalid Unity animation sample count." );
		var samples = new float[count];
		for ( var i = 0; i < count; i++ ) samples[i] = reader.ReadSingle();
		return samples;
	}
}
