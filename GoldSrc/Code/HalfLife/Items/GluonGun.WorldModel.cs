namespace GoldSrc.HalfLife;

public abstract partial class ClassicGluonGun
{
	SkinnedModelRenderer _held;
	BoneCollection.Bone _barrel;
	readonly List<(BoneCollection.Bone Weapon, BoneCollection.Bone Body, Rotation Offset)> _bindings = [];

	internal override Vector3 GripPoint( bool hd ) => new( 12, -5, -2 );

	internal override void SetupHeldModel( SkinnedModelRenderer renderer )
	{
		_held = renderer;
		_barrel = renderer.Model.Bones.GetBone( "Bip01 R Forearm" ) ?? renderer.Model.Bones.GetBone( "Bip01 R Arm2" );
		_bindings.Clear();
		Bind( "Bip01 Spine1", "spine_1" );
		Bind( "Bip01 Neck", "neck_0" );
		Bind( "Bip01 R UpperArm", "arm_upper_R" );
		Bind( "Bip01 R Arm1", "arm_upper_R" );
	}

	void Bind( string source, string destination )
	{
		var weapon = _held.Model.Bones.GetBone( source );
		var body = HolderRenderer.Model.Bones.GetBone( destination );
		if ( weapon is null || body is null ) return;
		// The GoldSrc player bind pose faces -Y. The s&box player faces +X.
		var offset = body.LocalTransform.Rotation.Inverse * Rotation.FromYaw( 90 ) * weapon.LocalTransform.Rotation;
		_bindings.Add( (weapon, body, offset) );
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();
		if ( !_held.IsValid() || !HolderRenderer.IsValid() ) return;
		// Keep the barrel on the hand when its parent (the hose on the upper arm) moves.
		if ( _barrel is not null ) _held.SetBoneTransform( _barrel, _barrel.LocalTransform );
		foreach ( var (weapon, body, offset) in _bindings )
		{
			if ( !HolderRenderer.TryGetBoneTransform( body, out var pose ) ) continue;
			var aligned = pose.WithRotation( pose.Rotation * offset );
			_held.SetBoneTransform( weapon, _held.WorldTransform.ToLocal( aligned ) );
		}
	}
}
