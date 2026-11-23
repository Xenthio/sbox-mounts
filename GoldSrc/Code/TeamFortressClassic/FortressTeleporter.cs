using System;
using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.TeamFortressClassic;

[Hide]
public sealed class FortressTeleporter : FortressBuilding
{
	internal const float PadHeight = 12;
	const float WarmupTime = 0.35f;
	const float ArrivalTime = 0.25f;
	const float RechargeTime = 10.5f;
	const float CheckInterval = 0.1f;

	[Sync( SyncFlags.FromHost )] public bool IsExit { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool IsReady { get; private set; }
	[Sync( SyncFlags.FromHost )] public PlayerController Traveller { get; private set; }

	PlayerController _lockedPlayer;
	FortressTeleporter _destination;
	TimeUntil _nextCheck, _phaseComplete, _recharge;
	bool _arriving;

	public override float MaxHealth => 125;
	internal override BuildingKind Kind => IsExit ? BuildingKind.TeleporterExit : BuildingKind.TeleporterEntrance;

	internal static FortressTeleporter Construct( PlayerController builder, Vector3 position, Rotation rotation, bool exit ) =>
		StartConstruction<FortressTeleporter>( builder, position, rotation,
			exit ? "TFC teleporter exit" : "TFC teleporter entrance", "teleporter", PadHeight, pad => pad.IsExit = exit );

	internal override void OnBuilt( BaseInventoryComponent inventory ) => _nextCheck = 0;

	internal override void Service( BaseInventoryComponent inventory, BaseAmmoResource cells )
	{
		if ( !IsBuilt || Health <= 0 ) return;
		var cost = (int)MathF.Ceiling( (MaxHealth - Health) / 5 );
		var consumed = inventory.TakeAmmo( cells, cost );
		if ( consumed <= 0 ) return;
		Health = MathF.Min( MaxHealth, Health + consumed * 5 );
		Mount?.NpcAudio.Emit( "items/r_item2.wav", WorldPosition, attenuation: 0.8f );
	}

	internal override void OnBuildingUpdate()
	{
		if ( Traveller.IsValid() )
		{
			if ( !IsAlive( Traveller ) || (_arriving && _phaseComplete <= 0) )
				EndTransfer();
			else if ( !_arriving && (!CanTravel( Traveller ) || !IsStandingOnPad( Traveller ) || !_destination.IsValid()) )
				CancelTransfer();
			else if ( !_arriving && _phaseComplete <= 0 )
				SendTraveller();
			return;
		}
		if ( _nextCheck > 0 ) return;
		_nextCheck = CheckInterval;

		var partner = Scene.GetAllComponents<FortressTeleporter>().FirstOrDefault( pad =>
			pad.IsBuilt && pad.Health > 0 && pad.Builder == Builder && pad.IsExit != IsExit );
		var ready = partner.IsValid() && _recharge <= 0 && partner._recharge <= 0 && !partner.Traveller.IsValid();
		if ( ready && !IsReady ) Mount?.NpcAudio.Emit( "misc/teleport_ready.wav", WorldPosition );
		IsReady = ready;
		if ( !ready || IsExit ) return;

		var traveller = Scene.GetAllComponents<PlayerController>().FirstOrDefault( player =>
			IsAlive( player ) && CanTravel( player ) && IsStandingOnPad( player ) && player.Velocity.LengthSquared <= 4 &&
			!Scene.GetAllComponents<FortressTeleporter>().Any( pad => pad.Traveller == player ) );
		if ( !traveller.IsValid() ) return;

		Traveller = traveller;
		_destination = partner;
		_arriving = false;
		_phaseComplete = WarmupTime;
		IsReady = partner.IsReady = false;
		Mount?.NpcAudio.Emit( "misc/teleport_in.wav", WorldPosition );
		ShowRing( WorldPosition );
		FadeTraveller( false, WarmupTime );
	}

	static bool IsAlive( PlayerController player ) => player.IsValid() && player.Enabled &&
		MapState.PlayerVitals( player ) is { Health: > 0 };

	bool CanTravel( PlayerController player ) => FortressPlayer.AreTeammates( Builder, player ) != false;

	bool IsStandingOnPad( PlayerController player )
	{
		if ( !player.IsOnGround ) return false;
		var delta = WorldTransform.PointToLocal( player.WorldPosition );
		return MathF.Abs( delta.x ) <= 16 && MathF.Abs( delta.y ) <= 16 && MathF.Abs( delta.z - PadHeight ) <= 3;
	}

	void SendTraveller()
	{
		var position = _destination.WorldPosition + Vector3.Up * (PadHeight + 1);
		if ( !ClearLanding( Traveller, position ) )
		{
			CancelTransfer();
			_nextCheck = 0.5f;
			return;
		}

		_recharge = _destination._recharge = RechargeTime;
		_arriving = true;
		_phaseComplete = ArrivalTime;
		using ( Rpc.FilterInclude( Traveller.Network.Owner ?? Connection.Local ) )
			MoveTraveller( Traveller, position );
		Mount?.NpcAudio.Emit( "misc/teleport_out.wav", _destination.WorldPosition );
		ShowRing( _destination.WorldPosition );
	}

	bool ClearLanding( PlayerController traveller, Vector3 position )
	{
		var hull = traveller.BodyBox( 2 );
		var landing = new BBox( position + hull.Mins, position + hull.Maxs );
		var occupants = Scene.GetAllComponents<PlayerController>().Where( other => other != traveller && IsAlive( other ) &&
			other.BodyBox( 2 ).Transform( other.WorldTransform ).Overlaps( landing ) ).ToArray();

		var trace = Scene.Trace.Box( hull, position, position + Vector3.Up * 0.1f )
			.IgnoreGameObjectHierarchy( traveller.GameObject ).IgnoreGameObjectHierarchy( _destination.GameObject )
			.WithoutTags( "trigger", "debris" );
		foreach ( var occupant in occupants ) trace = trace.IgnoreGameObjectHierarchy( occupant.GameObject );
		var hit = trace.Run();
		if ( hit.Hit || hit.StartedSolid ) return false;

		foreach ( var occupant in occupants )
		{
			var damage = new DamageInfo { Damage = 5000, Origin = position, Position = occupant.WorldPosition,
				Attacker = traveller.GameObject, Weapon = GameObject };
			damage.Tags.Add( "telefrag" );
			MapState.PlayerVitals( occupant )?.OnDamage( damage );
		}
		return !occupants.Any( IsAlive );
	}

	void EndTransfer()
	{
		Traveller = null;
		_destination = null;
		_arriving = false;
	}

	void CancelTransfer()
	{
		if ( Traveller.IsValid() ) FadeTraveller( true, ArrivalTime );
		EndTransfer();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		var local = Traveller is { IsProxy: false } ? Traveller : null;
		if ( _lockedPlayer == local ) return;
		ReleaseMovement();
		if ( !local.IsValid() ) return;
		_lockedPlayer = local;
		FortressSpeed.SetTeleporting( local, true );
	}

	protected override void OnDisabled()
	{
		if ( Networking.IsHost ) CancelTransfer();
		ReleaseMovement();
	}

	void ReleaseMovement()
	{
		if ( _lockedPlayer.IsValid() ) FortressSpeed.SetTeleporting( _lockedPlayer, false );
		_lockedPlayer = null;
	}

	void FadeTraveller( bool fadeIn, float duration )
	{
		using ( Rpc.FilterInclude( Traveller.Network.Owner ?? Connection.Local ) )
			ShowFade( Traveller, fadeIn, duration );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void MoveTraveller( PlayerController traveller, Vector3 position )
	{
		if ( !traveller.IsValid() || traveller.IsProxy ) return;
		traveller.PreventGrounding( 0.1f );
		traveller.WorldPosition = position;
		if ( traveller.Body.IsValid() ) traveller.Body.Velocity = Vector3.Zero;
		traveller.GameObject.Transform.ClearInterpolation();
		ApplyFade( traveller, true, ArrivalTime );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowFade( PlayerController traveller, bool fadeIn, float duration ) => ApplyFade( traveller, fadeIn, duration );

	static void ApplyFade( PlayerController traveller, bool fadeIn, float duration )
	{
		if ( !traveller.IsValid() || traveller.IsProxy || traveller.Scene.Camera is not { IsValid: true } camera ) return;
		camera.GameObject.GetOrAddComponent<ScreenFadeDisplay>().Start( Color.White, duration, 0, fadeIn );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowRing( Vector3 position )
	{
		if ( Mount is { } mount ) SonicRing.Spawn( position + Vector3.Up * PadHeight, Color.White, 48, mount, width: 8 );
	}

	protected override void DestroyBuilding( GameObject attacker )
	{
		var source = Builder.IsValid() ? Builder.GameObject : attacker;
		FortressBlast.Apply( GameObject, source, WorldPosition + Vector3.Up * 7, 25, falloff: 0.5f );
		Mount?.NpcAudio.Emit( "weapons/explode3.wav", WorldPosition );
		ShowExplosion( WorldPosition );
		GameObject.Destroy();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowExplosion( Vector3 position )
	{
		if ( Mount is { } mount )
			Blast.SpriteEffect( "TFC teleporter explosion", "explode01", position, 3, 15, additive: false, mount: mount );
	}
}
