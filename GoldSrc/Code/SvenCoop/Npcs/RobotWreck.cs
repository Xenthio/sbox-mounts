using GoldSrc.HalfLife;

namespace GoldSrc.SvenCoop;

[Hide]
public sealed class RobotWreck : NpcCorpse
{
	TimeUntil _detonate = Game.Random.Int( 3, 7 );
	TimeUntil _nextSmoke;
	bool _exploded;
	internal override bool PlayEventsOnClients => true;

	protected override void OnStart()
	{
		FadeOnDeath = false;
		base.OnStart();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !Networking.IsHost || !IsValid || _exploded ) return;

		if ( _detonate <= 0 )
		{
			_exploded = true;
			var mount = Sandbox.Mounting.Directory.Get( MountIdent ) as GameMount;
			var position = WorldPosition + Vector3.Up * 8;
			Blast.Detonate( GameObject, GameObject, position, Vector3.Up, 100, false, mount );
			mount?.NpcAudio.Emit( "debris/beamstart14.wav", position, attenuation: 0.8f );
			ShowDebris( position, MountIdent );
			GameObject.Destroy();
			return;
		}

		if ( _nextSmoke > 0 ) return;
		_nextSmoke = 0.1f;
		var jitter = new Vector3( Game.Random.Float( -4, 4 ), Game.Random.Float( -4, 4 ), 0 );
		ShowSmoke( WorldPosition + jitter, MountIdent );
	}

	internal override void HandleEvent( int id )
	{
		if ( id == 11 && Renderer.IsValid() ) Renderer.SetBodyGroup( "weapons", 2 );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowSmoke( Vector3 position, string mountIdent )
	{
		if ( Application.IsDedicatedServer || Sandbox.Mounting.Directory.Get( mountIdent ) is not GameMount mount ) return;
		Blast.SmokePuff( "Robot wreck smoke", position, 1.5f, mount );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static async void ShowDebris( Vector3 position, string mountIdent )
	{
		if ( Application.IsDedicatedServer || Sandbox.Mounting.Directory.Get( mountIdent ) is not GameMount mount ) return;
		await Scatter( mount, "chromegibs", position, 64, Vector3.Up * 10 );
		await Scatter( mount, "computergibs", position, 96, Vector3.Up * 80 + new Vector3( 10, 20, 0 ) );
	}

	static async System.Threading.Tasks.Task Scatter( GameMount mount, string modelName, Vector3 position, float height, Vector3 velocity )
	{
		if ( mount.FindMountPath( $"models/{modelName}.mdl" ) is not { } path ) return;
		var scene = Game.ActiveScene;
		var model = await Model.LoadAsync( $"mount://{mount.Ident}/{path}.vmdl" );
		if ( !scene.IsValid() || !model.IsValid() || model.IsError || model.Parts.All.FirstOrDefault() is not { } part ) return;

		for ( int i = 0; i < 15; i++ )
		{
			var center = position + new Vector3( Game.Random.Float( -100, 100 ), Game.Random.Float( -100, 100 ), Game.Random.Float( 4, height / 2 ) );
			HalfLife.Gib.Spawn( model, part.Name, Game.Random.Int( 0, part.Choices.Count - 1 ), center, velocity + Vector3.Random * 30, Npc.BloodColor.None );
		}
	}
}
