namespace GoldSrc.HalfLife;

[Hide]
public sealed class StudioWeaponView : BaseWeaponModel
{
	IReadOnlyList<StudioSequence> _clips;
	StudioSequence _current;
	TimeSince _elapsed;
	float _idleAfter;
	bool _loop;
	internal float RemainingIdleTime
	{
		get => System.MathF.Max( 0, _idleAfter - _elapsed );
		set => _idleAfter = _elapsed + value;
	}
	readonly StudioSequenceEvents _sequenceEvents = new();
	NpcAudio _audio;

	internal void Initialize( StudioWeapon weapon, ModelLoader loader, NpcAudio audio )
	{
		_clips = loader.Sequences;
		_audio = audio;
		_weapon = weapon;
		Renderer.UseAnimGraph = true;
		Renderer.Animation.Graph = loader.AnimationGraphs.GetGraph( _clips.ToArray() );
		OnDeploy();
	}

	internal bool Play( string name, bool holdLastFrame = false, float? idleAfter = null )
	{
		var sequence = _clips?.FirstOrDefault( clip => string.Equals( clip.Name, name, System.StringComparison.OrdinalIgnoreCase ) );
		if ( sequence is null ) return false;
		_current = sequence;
		_idleAfter = holdLastFrame ? float.PositiveInfinity : idleAfter ?? sequence.Duration;
		_loop = idleAfter > sequence.Duration && (sequence.Description.Flags & (int)Mdl.SequenceFlags.Looping) != 0;
		_elapsed = 0;
		_sequenceEvents.Reset( _current, 0, true );
		return true;
	}

	StudioWeapon _weapon;

	public override void OnDeploy() => Play( _weapon.DeploySequence );
	public override void OnAttack( Vector3? hitPoint = null, Vector3? origin = null ) =>
		Play( _weapon.ShotSequence, idleAfter: _weapon.IdleAfterShot );
	public override void OnReloadStart() =>
		Play( _weapon.ReloadSequence, idleAfter: _weapon.IdleAfterReloadStart );

	protected override void OnUpdate()
	{
		if ( Scene.Camera.IsValid() ) WorldTransform = Scene.Camera.WorldTransform;
		if ( _current is null || !Renderer.Animation.IsValid ) return;
		var time = _loop ? (float)_elapsed % _current.Duration : (float)_elapsed;
		_sequenceEvents.Update( _audio, Renderer, time, _loop, weapon: _weapon );
		if ( _elapsed >= _idleAfter && !_weapon.IsReloading && _weapon.CanIdle )
		{
			_weapon.PlayIdle( this );
			time = _elapsed;
		}
		Renderer.Animation.Parameters.Set( StudioAnimationGraphs.StateParameter, _current.Name );
		Renderer.Animation.Parameters.Set( _current.TimeParameter, System.MathF.Min( 1, time / _current.Duration ) );
	}
}
