using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

public sealed partial class HeavyWeaponsGrunt
{
	Minigun _droppedMinigun;

	protected override void Think()
	{
		if ( _stunned > 0 ) return;
		if ( Action is null && !HasMinigun && _droppedMinigun.IsValid() && !_droppedMinigun.Inventory.IsValid() &&
			!FindVisibleEnemy().IsValid() && Find( "pickup_minigun" ) is { } pickup )
		{
			var destination = _droppedMinigun.WorldPosition;
			if ( WorldPosition.Distance( destination ) <= 72 )
			{
				FaceCorpse( destination );
				PlayAction( pickup );
				return;
			}
			if ( MoveToward( destination, 45 ) ) return;
		}
		base.Think();
	}

	void RecoverMinigun()
	{
		if ( !_droppedMinigun.IsValid() || _droppedMinigun.Inventory.IsValid() ||
			WorldPosition.Distance( _droppedMinigun.WorldPosition ) > 80 ) return;
		_droppedMinigun.GameObject.Destroy();
		_droppedMinigun = null;
		HasMinigun = true;
	}

	bool DropMinigun()
	{
		_droppedMinigun = SpawnMinigun( Renderer, toss: true );
		if ( !_droppedMinigun.IsValid() ) return false;
		HasMinigun = false;
		return true;
	}

	static Minigun SpawnMinigun( SkinnedModelRenderer renderer, bool toss = false )
	{
		var transform = renderer.GetAttachment( "attachment1" ) ?? renderer.WorldTransform;
		return WeaponPickupLoader.SpawnDrop<Minigun>( transform, toss );
	}

	internal override void OnGibbed()
	{
		if ( HasMinigun && !DisableMinigunDrop ) DropMinigun();
	}

	protected override void OnDeath( Component.IDamageable.DeathEvent death )
	{
		// Legacy studio models have no event to drop the gun during their death sequence.
		if ( HasMinigun && !DisableMinigunDrop && Find( "pickup_minigun" ) is null ) DropMinigun();
		base.OnDeath( death );
	}

	internal override NpcCorpse AddCorpseComponent( GameObject go )
	{
		var corpse = go.AddComponent<HeavyWeaponsGruntCorpse>();
		corpse.CarriesMinigun = HasMinigun && !DisableMinigunDrop;
		return corpse;
	}

	[Hide]
	public sealed class HeavyWeaponsGruntCorpse : NpcCorpse
	{
		[Property] public bool CarriesMinigun { get; set; }
		[Sync( SyncFlags.FromHost ), Change( nameof( OnDropped ) )] bool Dropped { get; set; }

		internal override void HandleEvent( int id )
		{
			if ( id == 11 && CarriesMinigun && !Dropped ) Dropped = SpawnMinigun( Renderer ).IsValid();
		}

		void OnDropped( bool _, bool dropped ) => SetBody();

		internal override void SetBody()
		{
			if ( Dropped && Renderer.IsValid() && Renderer.Model.IsValid() ) Renderer.SetBodyGroup( "weapons", 4 );
		}
	}
}
