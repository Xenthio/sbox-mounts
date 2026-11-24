using GoldSrc.Maps;

namespace GoldSrc.SvenCoop;

public sealed partial class Medkit
{
	const int RevivalCost = 50;
	const float RevivalTime = 2;
	Component _reviveTarget;
	TimeUntil _reviveAt;
	TimeUntil _heldUntil;
	TimeUntil _reviveRetry;

	public override void SecondaryAttack() => ChargeRevive();

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ChargeRevive()
	{
		if ( !IsActive || !Owner.IsValid() || !Inventory.IsValid() || PrimaryAmmoType is null || _reviveRetry > 0 ||
			MapState.PlayerVitals( Owner ) is not { Health: > 0 } ) return;

		if ( Inventory.GetAmmo( PrimaryAmmoType ) < RevivalCost )
		{
			DenyRevive( "items/suitchargeno1.wav" );
			return;
		}

		var hit = TraceMelee( AimRay, 16 );
		var npc = hit.GameObject?.GetComponentInParent<RevivableCorpse>();
		var corpse = npc.IsValid() && npc.CanRevive( Owner ) ? npc as Component : SandboxPlayerRevival.Find( hit.GameObject );
		if ( !corpse.IsValid() )
		{
			DenyRevive( "items/medshotno1.wav" );
			return;
		}

		if ( corpse != _reviveTarget || _heldUntil <= 0 )
		{
			_reviveTarget = corpse;
			_reviveAt = RevivalTime;
			PlayRevivalCue( "items/suitchargeok1.wav", "longuse" );
		}
		_heldUntil = 0.3f;
		if ( _reviveAt > 0 ) return;

		if ( !CompleteRevival( corpse ) )
		{
			DenyRevive( "items/medshotno1.wav" );
			return;
		}
		_reviveTarget = null;
		_reviveRetry = _nextTreatment = RevivalTime;
		ContentMount?.NpcAudio.Emit( "weapons/electro4.wav", Owner.WorldPosition, pitch: 1.1f, attenuation: 0.8f );
		PlayRevivalCue( "items/medshot5.wav", "shortuse" );
	}

	bool CompleteRevival( Component corpse )
	{
		var inventory = Inventory;
		var ammo = PrimaryAmmoType;
		if ( inventory.TakeAmmo( ammo, RevivalCost ) != RevivalCost ) return false;
		if ( corpse is RevivableCorpse npc )
		{
			if ( npc.TryFindStandingPlace( out var placement ) )
			{
				npc.Revive( placement );
				return true;
			}
		}
		else if ( SandboxPlayerRevival.TryRevive( corpse ) )
		{
			return true;
		}
		inventory.GiveAmmo( ammo, RevivalCost );
		return false;
	}

	void DenyRevive( string sample )
	{
		_reviveTarget = null;
		_reviveRetry = 1;
		ContentMount?.NpcAudio.Emit( sample, Owner.WorldPosition, pitch: 1.1f, attenuation: 0.8f );
	}

	void PlayRevivalCue( string sample, string sequence )
	{
		ContentMount?.NpcAudio.Emit( sample, Owner.WorldPosition, attenuation: 0.8f );
		using ( Rpc.FilterInclude( Owner.Network.Owner ?? Connection.Local ) )
			PlayTreatment( sequence );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Networking.IsHost && _heldUntil <= 0 ) _reviveTarget = null;
	}

	protected override void OnHolstered()
	{
		base.OnHolstered();
		_reviveTarget = null;
	}
}
