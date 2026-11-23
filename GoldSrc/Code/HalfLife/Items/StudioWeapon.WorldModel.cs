namespace GoldSrc.HalfLife;

public abstract partial class StudioWeapon
{
	Transform _gripInverse;
	BoneCollection.Bone _palm;

	// The studio hand bone starts at the wrist. Locate the handle inside the palm.
	internal virtual Vector3 GripPoint( bool hd ) => new( 3.5f, 1, 0 );
	internal virtual void SetupHeldModel( SkinnedModelRenderer renderer ) { }
	internal virtual string HeldModelName => Definition.Model;
	internal virtual string WorldModelPath => Definition.ModelPath( $"p_{HeldModelName}" );

	protected override void CreateWorldModel()
	{
		DestroyWorldModel();
		if ( !HolderRenderer.IsValid() ) return;
		WorldModel = MakeModel( WorldModelPath, HolderRenderer.GetBoneObject( HoldBone ) ?? GameObject, false );
	}

	void AlignHeldModel( SkinnedModelRenderer renderer, bool hd )
	{
		var bones = renderer.Model.Bones;
		var hand = bones.GetBone( "Bip01 R Hand" ) ?? bones.GetBone( "Bip01 R Forearm" ) ?? bones.GetBone( "Bip01 R Arm2" );
		if ( hand is null ) return;
		var grip = hand.LocalTransform;
		_gripInverse = grip.ToWorld( new Transform( GripPoint( hd ) ) ).ToLocal( global::Transform.Zero );
		renderer.LocalTransform = _gripInverse;
		_palm = HolderRenderer.Model.Bones.GetBone( "finger_middle_1_R" );
		_worldMuzzle = grip.ToWorld( new Transform( WorldMuzzleOffset( hd ) ) );
		SetupHeldModel( renderer );
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();
		UpdateHeldGrip();
	}

	void UpdateHeldGrip()
	{
		if ( !WorldModel.IsValid() || !HolderRenderer.IsValid() ) return;
		var parent = WorldModel.Parent;
		if ( !parent.IsValid() || _palm is null || !HolderRenderer.TryGetBoneTransform( _palm, out var finger ) ) return;
		var palm = parent.WorldTransform.PointToLocal( finger.Position );
		WorldModel.LocalTransform = _gripInverse.WithPosition( _gripInverse.Position + palm );
	}

	protected override void OnDestroy()
	{
		DestroyWorldModel();
		base.OnDestroy();
	}
}
