using System;
using System.Runtime.CompilerServices;
using GoldSrc.HalfLife;

namespace GoldSrc.TeamFortressClassic;

[Title( "Concussion Grenade" ), Category( "Team Fortress Classic" )]
[WeaponPickup( "grenade", "tfc_concussion_grenades", 3, EntityName = "tf_weapon_concussiongrenade",
	PickupModel = "conc_grenade", HudIcon = "d_concussiongrenade", Bucket = 4, Mount = "tfc" )]
public sealed class ConcussionGrenade : FortressHandGrenadeWeapon
{
	internal override void Configure()
	{
		base.Configure();
		StartingAmmo = 3;
	}

	internal override string LaunchGrenade( Vector3 origin, Vector3 direction, float pitch, float pinAge ) =>
		Launch<FortressConcussionGrenade>( origin, pinAge, "TFC concussion grenade", "models/conc_grenade.mdl" );
}

[Hide]
public sealed class FortressConcussionGrenade : FortressHandGrenade
{
	const float Radius = 280;

	internal override void Detonate()
	{
		var origin = WorldPosition;
		foreach ( var player in Scene.GetAllComponents<PlayerController>() )
		{
			if ( !player.Enabled || Maps.MapState.PlayerVitals( player ) is { Health: <= 0 } ) continue;
			var offset = FortressHandGrenadeWeapon.HullCenter( player ) - origin;
			if ( offset.LengthSquared > Radius * Radius ) continue;
			using ( Rpc.FilterInclude( player.Network.Owner ?? Connection.Local ) )
				ConcussPlayer( player, offset );
		}
		Mount?.NpcAudio.Emit( "weapons/concgren_blast", origin, attenuation: 0.8f );
		ShowRing( origin );
	}

	internal static Vector3 BlastVelocity( Vector3 offset, Vector3 currentVelocity )
	{
		var distance = offset.Length;
		// At close range the blast amplifies the player's movement. Further out it pushes away from the grenade.
		var direction = distance < 16 ? currentVelocity * 0.33f : offset;
		var scale = (Radius - distance * 0.5f) * 0.03f;
		return direction.WithZ( direction.z * 1.5f ) * scale;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ConcussPlayer( PlayerController player, Vector3 offset )
	{
		if ( !player.IsValid() || player.IsProxy || !player.Body.IsValid() ) return;
		var velocity = BlastVelocity( offset, player.Body.Velocity );
		if ( velocity.z > 0 ) player.PreventGrounding( 0.2f );
		player.Body.Velocity = velocity;
		ConcussionView.Apply( player );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowRing( Vector3 origin )
	{
		if ( Mount is { } mount ) SonicRing.Spawn( origin + Vector3.Up * 16, Color.White, 600, mount, width: 70 );
	}
}

internal sealed class ConcussionView : CameraEffectSystem.BaseEffect
{
	static readonly ConditionalWeakTable<PlayerController, ConcussionView> Active = new();
	PlayerController _player;

	internal static void Apply( PlayerController player )
	{
		if ( Application.IsDedicatedServer || !player.Scene.Camera.IsValid() ) return;
		Cure( player );
		var concussion = new ConcussionView { _player = player, Camera = player.Scene.Camera, Duration = 15 };
		Active.Add( player, concussion );
		player.Scene.GetSystem<CameraEffectSystem>().Add( concussion );
	}

	internal static void Cure( PlayerController player )
	{
		if ( Active.TryGetValue( player, out var previous ) ) previous.Stop();
		Active.Remove( player );
	}

	public override bool IsDone => base.IsDone || !_player.IsValid() || !_player.Enabled ||
		Maps.MapState.PlayerVitals( _player ) is { Health: <= 0 };

	public override void Evaluate( float scale, ref Vector3 position, ref Angles angles, ref float fieldOfView )
	{
		if ( IsDone || _player.ThirdPerson ) return;
		var time = Time.Now;
		var intensity = (int)(100 * Fraction) * scale;
		angles += new Angles( MathF.Sin( time ) * 0.3f, MathF.Sin( time * 2 ) * 0.3f, MathF.Sin( time * 0.5f ) * 0.1f ) * intensity;
	}
}
