using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "Hand Grenade" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "grenade", "grenades", 50, ClassName = "handgrenade", Bucket = 4, Weight = 5, Mount = "ssh" )]
public sealed class HandGrenade : ClassicHandGrenade
{
	enum ThrowMode { Standard, Cluster, Shockwave }
	[Sync( SyncFlags.FromHost )] ThrowMode Mode { get; set; }
	TimeUntil _nextSelection;

	internal override void Configure()
	{
		base.Configure();
		SecondaryDelay = PrimaryDelay;
		SecondaryAutomatic = false;
	}

	public override bool HasSecondaryAmmo() => HasPrimaryAmmo();
	public override bool CanSecondaryAttack() => CanPrime && base.CanSecondaryAttack();
	public override void SecondaryAttack() => Prime( true );
	internal override float ThrowSpeed( float pitch ) => System.Math.Clamp( (90 - pitch) * 4, 0, 500 );

	protected override void Think()
	{
		base.Think();
		if ( Input.Down( "use" ) && Input.Pressed( "reload" ) ) CycleMode();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void CycleMode()
	{
		if ( !IsActive || !Owner.IsValid() || !CanPrime || _nextSelection > 0 ) return;
		_nextSelection = 1;
		Mode = Mode == ThrowMode.Shockwave ? ThrowMode.Standard : Mode + 1;
		ShowMode( Mode );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowMode( ThrowMode mode )
	{
		if ( IsProxy || Application.IsDedicatedServer ) return;
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = $"{mode} mode selected",
			Position = new Vector2( -1, 0.5f ),
			Color = Color.White,
			HoldTime = 1,
			FadeOut = 0.25f
		} );
	}

	internal override void SpawnGrenade( Vector3 origin, Vector3 velocity, float fuse )
	{
		if ( ContentMount is not { } mount ) return;
		var model = Mode == ThrowMode.Cluster ? "mirv_grenade" : "w_grenade";
		if ( mount.FindMountPath( $"models/{model}.mdl" ) is not { } path ) return;
		var modelPath = $"mount://{mount.Ident}/{path}.vmdl";
		if ( Mode == ThrowMode.Cluster )
			TimedGrenade.Spawn<ClusterGrenade>( Owner.GameObject, origin, velocity, fuse, "Cluster grenade", modelPath,
				charge => charge.Shells = Underhand, mount );
		else if ( Mode == ThrowMode.Shockwave && !Underhand )
			TimedGrenade.Spawn<ShockwaveGrenade>( Owner.GameObject, origin, velocity, fuse, "Shockwave grenade", modelPath, effectsMount: mount );
		else
			TimedGrenade.Spawn<SlaughterhouseGrenade>( Owner.GameObject, origin, velocity, fuse, "Hand grenade", modelPath,
				charge => charge.Payload = Mode == ThrowMode.Shockwave ? GrenadePayload.Emp :
					Underhand ? GrenadePayload.Incendiary : GrenadePayload.Explosive, mount );
	}
}
