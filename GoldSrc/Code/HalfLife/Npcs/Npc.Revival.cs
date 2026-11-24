namespace GoldSrc.HalfLife;

public partial class Npc
{
	GameObject _revivalCorpse;
	bool _retainingDeath;
	internal bool IsAwaitingRevival => _revivalCorpse.IsValid();

	internal bool RetainForRevival( Component.IDamageable.DyingEvent dying )
	{
		if ( !_loaded || _revivalCorpse.IsValid() || ShouldGib( dying.DamageInfo ) ) return false;

		// Keep the original actor so scripted sequences and BSP outputs retain their references.
		_retainingDeath = true;
		try
		{
			Speech?.Stop();
			OnDeath( new Component.IDamageable.DeathEvent { Victim = GameObject, DamageInfo = dying.DamageInfo, Instigator = dying.Instigator } );
		}
		finally
		{
			_retainingDeath = false;
		}
		if ( !_revivalCorpse.IsValid() ) return false;

		var target = _revivalCorpse.AddComponent<SvenCoop.RevivableCorpse>();
		target.Actor = this;
		if ( _revivalCorpse.GetComponent<NpcCorpse>() is { Renderer: { } renderer } &&
			!_revivalCorpse.GetComponent<ModelHitboxes>().IsValid() )
		{
			var hitboxes = _revivalCorpse.AddComponent<ModelHitboxes>();
			hitboxes.Renderer = renderer;
			hitboxes.Target = _revivalCorpse;
		}
		dying.Cancelled = true;
		InterruptAction();
		Movement?.Stop();
		Animation.ClearLookTarget();
		LastHostile = null;
		GameObject.Enabled = false;
		var death = new Component.IDamageable.DeathEvent
		{
			Victim = GameObject, DamageInfo = dying.DamageInfo, Instigator = dying.Instigator, Ragdoll = _revivalCorpse
		};
		Scene.RunEvent<Component.IDamageable.IEvents>( listener => listener.OnDied( death ) );
		return true;
	}

	internal void RestoreFromCorpse( Transform position )
	{
		_revivalCorpse = null;
		_death = null;
		WorldTransform = position;
		Health = System.MathF.Max( 1, MaxHealth * 0.5f );
		GameObject.Enabled = true;
	}
}
