namespace GoldSrc.HalfLife;

[Hide]
public class AmmoPickup : BaseAmmoPickup, Component.ITriggerListener
{
	[Property] public string SoundMount { get; set; } = "hl1";
	[Property] public string PickupSound { get; set; } = "items/9mmclip1.wav";

	internal static GameObject Create( Transform transform, Model model, BaseAmmoResource ammo, int amount, string title )
	{
		var go = new GameObject( false, title );
		go.Tags.Add( "prop", "removable" );
		go.WorldTransform = transform;
		go.AddComponent<ModelRenderer>().Model = model;
		var collider = go.AddComponent<BoxCollider>();
		collider.Center = model.Bounds.Center;
		collider.Scale = model.Bounds.Size;
		go.AddComponent<Rigidbody>();
		var pickup = go.AddComponent<AmmoPickup>();
		pickup.DisplayName = title;
		pickup.AmmoType = ammo;
		pickup.Amount = amount;
		var trigger = go.AddComponent<BoxCollider>();
		trigger.IsTrigger = true;
		trigger.Center = collider.Center;
		trigger.Scale = collider.Scale + new Vector3( 24, 24, 12 );
		go.Enabled = true;
		return go;
	}

	void ITriggerListener.OnTriggerEnter( GameObject other )
	{
		if ( !Networking.IsHost || GameObject.IsDestroyed ) return;
		var inventory = other.GetComponentInParent<PlayerController>()?.GameObject.GetComponentInChildren<BaseInventoryComponent>();
		if ( inventory?.PickupMode == BaseInventoryComponent.PickupBehaviour.Touch ) inventory.Add( this );
	}

	protected override bool OnAdding( BaseInventoryComponent inventory )
	{
		if ( OnCanPickup( inventory ) && Sandbox.Mounting.Directory.Get( SoundMount ) is GameMount mount )
			mount.NpcAudio.Emit( PickupSound, WorldPosition );
		return base.OnAdding( inventory );
	}
}
