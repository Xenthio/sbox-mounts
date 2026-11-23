using System;
using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.TeamFortressClassic;

[Hide]
public sealed class FortressDispenser : Component, Component.IDamageable
{
	const float SupplyInterval = 12;
	const float RefillReach = 38;
	const float ArmourCapacity = 500;

	static readonly (string Ammo, int Capacity, int Production, int Dispense, int Deposit)[] Supplies =
	[
		("buckshot", 400, 20, 20, 40),
		("9mm", 600, 30, 20, 40),
		("rockets", 300, 15, 10, 10),
		("uranium", 400, 20, 10, 40)
	];
	readonly int[] _stock = new int[Supplies.Length];
	float _armour;
	TimeUntil _nextProduction, _nextRefill;

	static GameMount Mount => Sandbox.Mounting.Directory.Get( "tfc" ) as TeamFortressClassicMount;

	[Sync( SyncFlags.FromHost )] public PlayerController Builder { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsBuilt { get; private set; }
	[Sync( SyncFlags.FromHost )] public float Health { get; set; } = 150;
	public float MaxHealth => 150;

	internal static FortressDispenser StartConstruction( PlayerController builder, Vector3 position, Rotation rotation )
	{
		if ( Mount?.FindMountPath( "models/dispenser.mdl" ) is not { } path ||
			Model.Load( $"mount://tfc/{path}.vmdl" ) is not { IsError: false } model ) return null;

		var go = new GameObject( false, "TFC dispenser" ) { WorldPosition = position, WorldRotation = rotation };
		go.Tags.Add( "removable" );
		go.AddComponent<ModelRenderer>().Model = model;
		var collider = go.AddComponent<BoxCollider>();
		collider.Center = Vector3.Up * 12;
		collider.Scale = new Vector3( 32, 32, 24 );
		collider.Static = true;
		var dispenser = go.AddComponent<FortressDispenser>();
		dispenser.Builder = builder;
		go.NetworkSpawn( true, null );
		return dispenser;
	}

	internal void Finish( BaseInventoryComponent inventory )
	{
		if ( IsBuilt ) return;
		for ( var i = 0; i < Supplies.Length; i++ )
		{
			var supply = Supplies[i];
			var ammo = GetAmmoType( supply.Ammo );
			var balance = inventory.GetAmmo( ammo );
			if ( balance <= 0 || balance == int.MaxValue ) continue;

			var amount = Math.Min( supply.Capacity, (int)MathF.Ceiling( balance * 0.25f ) );
			_stock[i] = inventory.TakeAmmo( ammo, amount );
		}
		if ( MapState.PlayerVitals( Builder ) is { MaxArmour: > 0 } vitals )
		{
			_armour = MathF.Min( ArmourCapacity, MathF.Min( vitals.Armour, MathF.Ceiling( vitals.Armour * 0.25f ) ) );
			vitals.Armour -= _armour;
		}
		IsBuilt = true;
		_nextProduction = SupplyInterval;
		_nextRefill = 0.2f;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !Builder.IsValid() )
		{
			GameObject.Destroy();
			return;
		}
		if ( !IsBuilt ) return;
		if ( _nextProduction <= 0 )
		{
			_nextProduction = SupplyInterval;
			for ( var i = 0; i < Supplies.Length; i++ )
				_stock[i] = Math.Min( Supplies[i].Capacity, _stock[i] + Supplies[i].Production );
			_armour = MathF.Min( ArmourCapacity, _armour + 50 );
		}

		if ( _nextRefill > 0 ) return;
		_nextRefill = 0.2f;
		foreach ( var player in Scene.GetAllComponents<PlayerController>().OrderBy( player => player.WorldPosition.DistanceSquared( WorldPosition ) ) )
		{
			if ( player.WorldPosition.DistanceSquared( WorldPosition ) > RefillReach * RefillReach ) break;
			if ( !player.Enabled || MapState.PlayerVitals( player ) is not { Health: > 0 } vitals ) continue;

			var cover = Scene.Trace.Ray( WorldPosition + Vector3.Up * 12, player.WorldPosition + Vector3.Up * 12 )
				.IgnoreGameObjectHierarchy( GameObject ).IgnoreGameObjectHierarchy( player.GameObject ).WithoutTags( "trigger" ).Run();
			if ( cover.Hit || !Supply( player, vitals ) ) continue;
			_nextRefill = 1;
			Mount?.NpcAudio.Emit( "items/ammopickup2.wav", player.WorldPosition, attenuation: 0.8f );
			break;
		}
	}

	bool Supply( PlayerController player, IDamageable vitals )
	{
		var supplied = 0;
		if ( player.GetComponentInChildren<BaseInventoryComponent>() is { } inventory )
		{
			for ( var i = 0; i < Supplies.Length; i++ )
			{
				var amount = Math.Min( _stock[i], Supplies[i].Dispense );
				if ( amount <= 0 ) continue;
				var given = inventory.GiveAmmo( GetAmmoType( Supplies[i].Ammo ), amount );
				_stock[i] -= given;
				supplied += given;
			}
		}
		var armour = MathF.Min( 20, MathF.Min( _armour, vitals.MaxArmour - vitals.Armour ) );
		if ( armour <= 0 ) return supplied > 0;
		_armour -= armour;
		vitals.Armour += armour;
		return true;
	}

	internal void Service( BaseInventoryComponent inventory, BaseAmmoResource cells )
	{
		if ( !IsBuilt || Health <= 0 ) return;
		_nextRefill = 3;
		if ( Health < MaxHealth )
		{
			var cost = (int)MathF.Ceiling( (MaxHealth - Health) / 5 );
			var consumed = inventory.TakeAmmo( cells, cost );
			if ( consumed == 0 ) return;
			Health = MathF.Min( MaxHealth, Health + consumed * 5 );
			Mount?.NpcAudio.Emit( "items/r_item2.wav", WorldPosition, attenuation: 0.8f );
			return;
		}

		for ( var i = 0; i < Supplies.Length; i++ )
		{
			var supply = Supplies[i];
			var count = Math.Min( supply.Deposit, supply.Capacity - _stock[i] );
			var taken = inventory.TakeAmmo( GetAmmoType( supply.Ammo ), count );
			_stock[i] += taken;
		}
	}

	static BaseAmmoResource GetAmmoType( string name ) => GameResource.Load<BaseAmmoResource>( $"mount://tfc/ammo/{name}.ammo" );

	public void OnDamage( in DamageInfo damage )
	{
		if ( !Networking.IsHost || !IsBuilt || Health <= 0 || !float.IsFinite( damage.Damage ) || damage.Damage <= 0 ) return;
		Health = MathF.Max( 0, Health - damage.Damage );
		if ( Health > 0 ) return;

		var blast = MathF.Min( 250, 25 + _stock[2] * 1.5f + _stock[3] );
		var origin = WorldPosition + Vector3.Up * 12;
		FortressBlast.Apply( GameObject, Builder.IsValid() ? Builder.GameObject : damage.Attacker, origin, blast, falloff: 0.5f );
		Mount?.NpcAudio.Emit( "weapons/explode3.wav", origin );
		ShowExplosion( origin );
		GameObject.Destroy();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowExplosion( Vector3 origin )
	{
		if ( Mount is { } mount )
			Blast.SpriteEffect( "TFC dispenser explosion", "explode01", origin, 3, 15, additive: false, mount: mount );
	}
}
