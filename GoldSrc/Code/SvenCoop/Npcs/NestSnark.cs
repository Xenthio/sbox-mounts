using GoldSrc.HalfLife;
using Sandbox.Physics;

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
	internal override MonsterClass Classification => Friendly ? MonsterClass.PlayerAlly : base.Classification;
	internal override bool CanBite( Component victim ) => base.CanBite( victim ) &&
		(!Friendly || GetDisposition( victim.GameObject ) == Sandbox.Npcs.Disposition.Hostile);

	internal override void ApplyMapProperties( Bsp.Entity entity ) =>
		Friendly = entity.GetInt( "is_player_ally" ) != 0 || entity.GetInt( "classify" ) == (int)MonsterClass.PlayerAlly;

	protected override void OnStart()
	{
		GameObject.Tags.Add( SnarkTag );
		IgnoreVisualDebris();
		base.OnStart();
	}

	void IgnoreVisualDebris()
	{
		var world = Scene.PhysicsWorld;
		var existing = world.CollisionRules;
		var pair = ("debris", SnarkTag);
		if ( existing?.Pairs.ContainsKey( pair ) == true ) return;

		// GoldSrc's break-model pieces are client effects. They cannot deflect a hatchling.
		var rules = new CollisionRules();
		if ( existing is not null )
		{
			rules.Defaults = new( existing.Defaults );
			rules.Pairs = new( existing.Pairs );
		}
		rules.Pairs[pair] = CollisionRules.Result.Ignore;
		world.CollisionRules = Json.Deserialize<CollisionRules>( Json.Serialize( rules ) );
	}
}
