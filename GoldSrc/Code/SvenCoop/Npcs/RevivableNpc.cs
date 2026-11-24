using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

[Hide]
public sealed class RevivableNpc : Component, Component.IDamageable.IEvents
{
	[Property] public bool Allowed { get; set; } = true;

	void Component.IDamageable.IEvents.OnDying( Component.IDamageable.DyingEvent dying )
	{
		if ( !Allowed || dying.Victim != GameObject || dying.Cancelled || !dying.CreateRagdoll ||
			GetComponent<Npc>() is not { Definition.Movement: NpcMovement.Ground } npc ||
			npc is SnarkNest || npc.Classification is not (MonsterClass.PlayerAlly or MonsterClass.HumanPassive) ) return;
		npc.RetainForRevival( dying );
	}
}
