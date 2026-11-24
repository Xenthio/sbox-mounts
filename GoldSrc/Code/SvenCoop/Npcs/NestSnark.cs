using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

[Hide]
[NpcDefinition( "monster_snark", "w_squeak", MonsterClass.AlienWeapon, 2, 4, 8, 300,
	Mount = "svencoop", ShowInSpawnMenu = false, Movement = NpcMovement.Bouncing,
	Damage = 10, IdleSequence = "run", Blood = BloodColor.Yellow )]
public sealed class NestSnark : ClassicSnark
{
	internal const string Prefab = "mount://svencoop/entities/npcs/monster_snark.prefab";
	const string SnarkTag = "sven_nest_snark";

	[Property, Sync( SyncFlags.FromHost )] public bool Friendly { get; set; }
	bool _playerThrown;
	bool IsAllied => Friendly || _playerThrown;
	internal override MonsterClass Classification => IsAllied ? MonsterClass.PlayerAlly : base.Classification;
	internal override bool CanBite( Component victim ) => base.CanBite( victim ) &&
		(!_playerThrown || SvenPetCollision.IsNotPlayer( victim )) &&
		(!IsAllied || GetDisposition( victim.GameObject ) == Sandbox.Npcs.Disposition.Hostile);
	internal override System.Func<Component, bool> BlastVictimFilter => _playerThrown ? SvenPetCollision.IsNotPlayer : null;

	internal override void ApplyMapProperties( Bsp.Entity entity ) =>
		Friendly = entity.GetInt( "is_player_ally" ) != 0 || entity.GetInt( "classify" ) == (int)MonsterClass.PlayerAlly;

	protected override void OnStart()
	{
		GameObject.Tags.Add( SnarkTag );
		// GoldSrc's break-model pieces are client effects. They cannot deflect a hatchling.
		SvenPetCollision.Ignore( Scene, SnarkTag, "debris" );
		_playerThrown = Thrower?.GetComponent<PlayerController>().IsValid() == true;
		if ( _playerThrown ) SvenPetCollision.SetIgnoresPlayers( GameObject, true );
		base.OnStart();
	}
}
