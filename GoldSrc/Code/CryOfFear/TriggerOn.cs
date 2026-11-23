using GoldSrc.Maps;

namespace GoldSrc.CryOfFear;

[Hide]
public sealed class TriggerOn : MapEntity
{
	public override void Trigger( GameObject activator = null )
	{
		if ( !CanTrigger || !Map.IsValid() ) return;
		if ( Map.Find( Target ) is TriggerVolume { Hurt: false } volume &&
			volume.GetComponent<Collider>( true ) is { } collider )
			collider.Enabled = true;
	}

	public override void TurnOff( GameObject activator = null ) => Trigger( activator );
}
