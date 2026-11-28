namespace UnityMount;

/// <summary>Reads compiled states, transitions and blend nodes from an Animator Controller.</summary>
static class UnityAnimatorGraph
{
	public static (UnityBlendTreeData[] Blends, UnityAnimatorControllerData Graph) Read( UnityReader reader, UnityTypeTree tree )
	{
		if ( !tree.HasField( "m_Controller" ) || !tree.HasField( "m_TOS" ) ) return ([], null);

		var names = new Dictionary<uint, string>();
		reader.Position = 0;
		tree.SeekField( reader, "m_TOS" ).VisitArray( reader, (_, _) =>
			names.TryAdd( reader.ReadUInt32(), reader.ReadAlignedString() ) );

		var results = new List<UnityBlendTreeData>();
		reader.Position = 0;
		var controller = tree.SeekField( reader, "m_Controller" );
		var controllerStart = reader.Position;
		var graph = new UnityAnimatorControllerData
		{
			Parameters = ReadParameters( controller, controllerStart ),
			Layers = ReadArray( controller, controllerStart, "m_LayerArray", ReadLayer ),
			StateMachines = ReadArray( controller, controllerStart, "m_StateMachineArray", ReadMachine )
		};
		return (results.ToArray(), graph);

		UnityAnimatorControllerData.Parameter[] ReadParameters( UnityTypeTree root, long start )
		{
			var defaults = NodeData( root, start, "m_DefaultValues" );
			var origin = reader.Position;
			var floats = ReadArray( defaults, origin, "m_FloatValues", (_, _) => reader.ReadSingle() );
			var integers = ReadArray( defaults, origin, "m_IntValues", (_, _) => reader.ReadInt32() );
			var booleans = ReadArray( defaults, origin, "m_BoolValues", (_, _) => reader.ReadBoolean() );
			var values = NodeData( root, start, "m_Values" );
			return ReadArray( values, reader.Position, "m_ValueArray", (entry, position) =>
			{
				var name = Name( entry, position, "m_ID" );
				var type = (UnityAnimatorControllerData.ParameterType)Field( entry, position, "m_Type", reader.ReadInt32 );
				var index = Field( entry, position, "m_Index", reader.ReadUInt32 );
				double? value = type switch
				{
					UnityAnimatorControllerData.ParameterType.Float when index < floats.Length && float.IsFinite( floats[index] ) => (double)floats[index],
					UnityAnimatorControllerData.ParameterType.Int when index < integers.Length => integers[index],
					UnityAnimatorControllerData.ParameterType.Bool when index < booleans.Length => booleans[index] ? 1 : 0,
					UnityAnimatorControllerData.ParameterType.Trigger => index < booleans.Length && booleans[index] ? 1 : 0,
					_ => null
				};
				return new UnityAnimatorControllerData.Parameter( name, type, value );
			} );
		}

		UnityAnimatorControllerData.Layer ReadLayer( UnityTypeTree entry, long _ )
		{
			var layer = entry.SeekField( reader, "data" );
			var start = reader.Position;
			var mask = NodeData( layer, start, "m_SkeletonMask" );
			var bones = ReadArray( mask, reader.Position, "m_Data", (bone, offset) =>
				new UnityAnimatorControllerData.MaskBone( Field( bone, offset, "m_PathHash", reader.ReadUInt32 ),
					Field( bone, offset, "m_Weight", ReadFiniteFloat ) ) );
			return new( Name( layer, start, "m_Binding" ),
				Field( layer, start, "m_StateMachineIndex", reader.ReadInt32 ),
				Field( layer, start, "m_StateMachineSynchronizedLayerIndex", reader.ReadInt32 ),
				(UnityAnimatorControllerData.LayerMode)Field( layer, start, "(int&)m_LayerBlendingMode", reader.ReadInt32 ),
				Field( layer, start, "m_DefaultWeight", ReadFiniteFloat ), bones,
				SynchronizedLayerAffectsTiming: layer.HasField( "m_SyncedLayerAffectsTiming" ) &&
					Field( layer, start, "m_SyncedLayerAffectsTiming", reader.ReadBoolean ) );
		}

		UnityAnimatorControllerData.Machine ReadMachine( UnityTypeTree entry, long _ )
		{
			var machine = entry.SeekField( reader, "data" );
			var start = reader.Position;
			var defaultState = Field( machine, start, "m_DefaultState", reader.ReadInt32 );
			var states = ReadArray( machine, start, "m_StateConstantArray", ReadState );
			var anyState = ReadArray( machine, start, "m_AnyStateTransitionConstantArray", ReadTransition );

			var selectors = machine.HasField( "m_SelectorStateConstantArray" )
				? ReadArray( machine, start, "m_SelectorStateConstantArray", ReadSelector ) : [];
			return new( defaultState, states, anyState, selectors );
		}

		UnityAnimatorControllerData.Selector ReadSelector( UnityTypeTree entry, long _ )
		{
			var selector = entry.SeekField( reader, "data" );
			var start = reader.Position;
			return new( Field( selector, start, "m_FullPathID", reader.ReadUInt32 ),
				Field( selector, start, "m_IsEntry", reader.ReadBoolean ),
				ReadArray( selector, start, "m_TransitionConstantArray", (wrapper, _) =>
				{
					var transition = wrapper.SeekField( reader, "data" );
					var offset = reader.Position;
					return new UnityAnimatorControllerData.SelectorTransition(
						Field( transition, offset, "m_Destination", reader.ReadInt32 ),
						ReadArray( transition, offset, "m_ConditionConstantArray", ReadCondition ) );
				} ) );
		}

		UnityAnimatorControllerData.State ReadState( UnityTypeTree wrapper, long _ )
		{
			var state = wrapper.SeekField( reader, "data" );
			var start = reader.Position;
			var name = Name( state, start, "m_NameID" );
			var path = Name( state, start, "m_FullPathID" );
			var speed = Field( state, start, "m_Speed", ReadFiniteFloat );
			var speedParameter = Name( state, start, "m_SpeedParamID" );
			var phase = Field( state, start, "m_CycleOffset", ReadFiniteFloat );
			var looping = Field( state, start, "m_Loop", reader.ReadBoolean );
			var mirrored = Field( state, start, "m_Mirror", reader.ReadBoolean );
			var motions = new List<UnityAnimatorControllerData.Motion>();
			var index = 0;
			reader.Position = start;
			state.SeekField( reader, "m_BlendTreeConstantArray" ).VisitArray( reader, (entry, _) =>
			{
				var treeIndex = index++;
				var data = entry.SeekField( reader, "data" );
				var nodes = ReadArray( data, reader.Position, "m_NodeArray", (node, _) =>
				{
					var value = node.SeekField( reader, "data" );
					return ReadNode( value, reader.Position );
				} );
				if ( nodes.Length == 0 || nodes.Any( node => node.Children.Any( child => (uint)child >= nodes.Length ) ) ) return;
				motions.Add( new( treeIndex, nodes[0].ClipSlot, null, nodes[0].CycleOffset, nodes[0].Mirrored ) );
				if ( nodes[0].Children.Length == 0 ) return;
				results.Add( new()
				{
					State = name,
					StatePath = path,
					TreeIndex = treeIndex,
					Speed = speed,
					SpeedParameter = speedParameter,
					CycleOffset = phase,
					Looping = looping,
					Mirrored = mirrored,
					Nodes = nodes
				} );
			} );
			return new( name, path, looping, speed, speedParameter, phase, Name( state, start, "m_CycleOffsetParamID" ),
				Name( state, start, "m_TimeParamID" ), mirrored, Name( state, start, "m_MirrorParamID" ),
				ReadArray( state, start, "m_BlendTreeConstantIndexArray", (_, _) => reader.ReadInt32() ), motions.ToArray(),
				ReadArray( state, start, "m_TransitionConstantArray", ReadTransition ) );
		}

		UnityAnimatorControllerData.Transition ReadTransition( UnityTypeTree entry, long _ )
		{
			var data = entry.SeekField( reader, "data" );
			var start = reader.Position;
			var conditions = ReadArray( data, start, "m_ConditionConstantArray", ReadCondition );
			return new( Field( data, start, "m_DestinationState", reader.ReadInt32 ),
				Name( data, start, "m_FullPathID" ),
				Field( data, start, "m_HasExitTime", reader.ReadBoolean ),
				Field( data, start, "m_ExitTime", ReadFiniteFloat ),
				Field( data, start, "m_HasFixedDuration", reader.ReadBoolean ),
				Field( data, start, "m_TransitionDuration", ReadFiniteFloat ),
				Field( data, start, "m_TransitionOffset", ReadFiniteFloat ),
				(UnityAnimatorControllerData.InterruptionSource)Field( data, start, "m_InterruptionSource", reader.ReadInt32 ),
				Field( data, start, "m_OrderedInterruption", reader.ReadBoolean ),
				Field( data, start, "m_CanTransitionToSelf", reader.ReadBoolean ), conditions );
		}

		UnityAnimatorControllerData.Condition ReadCondition( UnityTypeTree wrapper, long _ )
		{
			var condition = wrapper.SeekField( reader, "data" );
			var offset = reader.Position;
			return new( Name( condition, offset, "m_EventID" ),
				(UnityAnimatorControllerData.ConditionMode)Field( condition, offset, "m_ConditionMode", reader.ReadInt32 ),
				Field( condition, offset, "m_EventThreshold", ReadFiniteFloat ),
				Field( condition, offset, "m_ExitTime", ReadFiniteFloat ) );
		}

		UnityBlendTreeData.Node ReadNode( UnityTypeTree node, long start )
		{
			var result = new UnityBlendTreeData.Node
			{
				Type = (UnityBlendTreeData.BlendType)Field( node, start, "m_BlendType", reader.ReadInt32 ),
				Children = ReadArray( node, start, "m_ChildIndices", (_, _) => reader.ReadInt32() ),
				ClipSlot = Field( node, start, "m_ClipID", reader.ReadInt32 ),
				Duration = Field( node, start, "m_Duration", ReadFiniteFloat ),
				CycleOffset = Field( node, start, "m_CycleOffset", ReadFiniteFloat ),
				Mirrored = Field( node, start, "m_Mirror", reader.ReadBoolean )
			};
			if ( result.Children.Length == 0 ) return result;
			result = result with
			{
				ParameterX = Name( node, start, "m_BlendEventID" ),
				ParameterY = Name( node, start, "m_BlendEventYID" )
			};
			switch ( result.Type )
			{
				case UnityBlendTreeData.BlendType.Simple1D:
				{
					var data = NodeData( node, start, "m_Blend1dData" );
					return result with { Thresholds = ReadArray( data, reader.Position, "m_ChildThresholdArray", (_, _) => ReadFiniteFloat() ) };
				}
				case UnityBlendTreeData.BlendType.SimpleDirectional2D:
				case UnityBlendTreeData.BlendType.FreeformDirectional2D:
				case UnityBlendTreeData.BlendType.FreeformCartesian2D:
				{
					var data = NodeData( node, start, "m_Blend2dData" );
					var origin = reader.Position;
					return result with
					{
						Positions = ReadArray( data, origin, "m_ChildPositionArray", ReadVector ),
						Magnitudes = ReadArray( data, origin, "m_ChildMagnitudeArray", (_, _) => ReadFiniteFloat() ),
						PairVectors = ReadArray( data, origin, "m_ChildPairVectorArray", ReadPairVector ),
						PairAverageMagnitudeInverses = ReadArray( data, origin, "m_ChildPairAvgMagInvArray", (_, _) => FiniteOrNull( reader.ReadSingle() ) ),
						Neighbors = ReadArray( data, origin, "m_ChildNeighborListArray", (entry, offset) =>
							ReadArray( entry, offset, "m_NeighborArray", (_, _) => reader.ReadInt32() ) )
					};
				}
				case UnityBlendTreeData.BlendType.Direct:
				{
					var data = NodeData( node, start, "m_BlendDirectData" );
					var origin = reader.Position;
					return result with
					{
						DirectParameters = ReadArray( data, origin, "m_ChildBlendEventIDArray", (_, _) => names.GetValueOrDefault( reader.ReadUInt32() ) ),
						NormalizeDirectWeights = Field( data, origin, "m_NormalizedBlendValues", reader.ReadBoolean )
					};
				}
				default:
					return result;
			}
		}

		UnityTypeTree NodeData( UnityTypeTree parent, long start, string field )
		{
			reader.Position = start;
			return parent.SeekField( reader, field ).SeekField( reader, "data" );
		}

		string Name( UnityTypeTree parent, long start, string field ) =>
			names.GetValueOrDefault( Field( parent, start, field, reader.ReadUInt32 ) );

		Vector2 ReadVector( UnityTypeTree _, long offset ) => new( ReadFiniteFloat(), ReadFiniteFloat() );

		Vector2? ReadPairVector( UnityTypeTree _, long offset )
		{
			var x = reader.ReadSingle();
			var y = reader.ReadSingle();
			return float.IsFinite( x ) && float.IsFinite( y ) ? new Vector2( x, y ) : null;
		}

		float ReadFiniteFloat() => FiniteOrNull( reader.ReadSingle() ) ??
			throw new InvalidDataException( "Non-finite animator blend value." );

		static float? FiniteOrNull( float value ) => float.IsFinite( value ) ? value : null;

		T Field<T>( UnityTypeTree parent, long start, string field, Func<T> read )
		{
			reader.Position = start;
			parent.SeekField( reader, field );
			return read();
		}

		T[] ReadArray<T>( UnityTypeTree parent, long start, string field, Func<UnityTypeTree, long, T> read )
		{
			reader.Position = start;
			var values = new List<T>();
			parent.SeekField( reader, field ).VisitArray( reader, (entry, offset) => values.Add( read( entry, offset ) ) );
			return values.ToArray();
		}
	}
}
