using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.ScientistSlaughterhouse;

[Title( "9mm Assault Rifle" ), Category( "Scientist Slaughterhouse" )]
[WeaponPickup( "9mmar", "9mm", 400, Bucket = 2, Weight = 15, Mount = "ssh" )]
public sealed class Rifle : ClassicRifle
{
	enum GrenadeType { HighExplosive, Incendiary, Laser, PlasmaBomb }

	[Sync( SyncFlags.FromHost )] GrenadeType Grenade { get; set; }
	TimeUntil _nextModeChange;

	internal override int FreshRounds => 50;
	internal override float BulletDamage => 5;
	internal override string GrenadeModel => "mount://ssh/valve/models/grenade.mdl.vmdl";
	internal override GameMount GrenadeEffectsMount => ContentMount;

	public override Ray AimRay => Owner.IsValid() ? Owner.EyeTransform.ForwardRay : base.AimRay;

	protected override bool WantsReload() => !Input.Down( "use" ) && base.WantsReload();

	protected override void Think()
	{
		base.Think();
		if ( Input.Down( "use" ) && Input.Pressed( "reload" ) ) CycleGrenades();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void CycleGrenades()
	{
		if ( !IsActive || !Owner.IsValid() || _nextModeChange > 0 ) return;
		_nextModeChange = 1;
		Grenade = Grenade == GrenadeType.PlasmaBomb ? GrenadeType.HighExplosive : Grenade + 1;
		ShowGrenadeType( Grenade );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void ShowGrenadeType( GrenadeType type )
	{
		if ( IsProxy || Application.IsDedicatedServer ) return;
		var name = type switch
		{
			GrenadeType.HighExplosive => "High Explosive",
			GrenadeType.PlasmaBomb => "Plasma Bomb",
			_ => type.ToString()
		};
		Scene.GetSystem<MessageDisplay>()?.Show( new TitleMessage
		{
			Text = $"{name} grenades selected",
			Position = new Vector2( -1, 0.5f ),
			Color = Color.White,
			HoldTime = 1,
			FadeOut = 0.25f
		} );
	}

	internal override ContactGrenade SpawnGrenade( Ray aim, string model )
	{
		var origin = aim.Position + aim.Forward * 16;
		ContactGrenade Fire<T>() where T : ContactGrenade, new() =>
			NpcProjectile.Fire<T>( Owner.GameObject, model, 100, null, origin, aim.Forward, 800 );

		var grenade = Grenade switch
		{
			GrenadeType.Incendiary => Fire<IncendiaryRifleGrenade>(),
			GrenadeType.Laser => Fire<LaserRifleGrenade>(),
			GrenadeType.PlasmaBomb => Fire<PlasmaRifleGrenade>(),
			_ => Fire<ContactGrenade>()
		};
		grenade.PitchSpeed = Game.Random.Float( -500, -100 );
		return grenade;
	}
}
