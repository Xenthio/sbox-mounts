using GoldSrc.HalfLife;
using GoldSrc.Maps;

namespace GoldSrc.CryOfFear;

[Hide]
public sealed class RandomSpawn : MapEntity
{
	const int MarkerLimit = 30;
	[Property] public string ChildClass { get; set; }
	[Property] public bool IsCoordinator { get; set; }
	[Property] public int DrawCount { get; set; }
	[Property] public bool HitMonsterClip { get; set; }
	[Property] public string ModelOverride { get; set; }

	readonly List<Npc> _children = [];
	bool _selected;
	TimeUntil? _selectAt;

	internal sealed record SavedChild( string ClassName, Transform Transform, float Health, int Body, int Skin, Npc.SavedMagazine Magazine = null );
	internal sealed record SavedGroup( bool Selected, float RemainingTime, SavedChild[] Children );

	internal SavedGroup CaptureGroup() => new( _selected, _selectAt is { } timer ? (float)timer : 2f, _children
		.Where( child => child.IsValid() && child.Health > 0 )
		.Select( child => new SavedChild( child.ClassName, child.WorldTransform, child.Health, child.Body, child.Skin, child.CaptureMagazine() ) ).ToArray() );

	internal void RestoreGroup( SavedGroup save )
	{
		foreach ( var child in _children )
			if ( child.IsValid() ) child.GameObject.Destroy();
		_children.Clear();
		_selected = save.Selected;
		_selectAt = save.RemainingTime;
		foreach ( var child in save.Children )
			Spawn( child.ClassName, child.Transform, child );
	}

	protected override void OnStart() => _selectAt ??= 2;

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !IsCoordinator || _selected || _selectAt is not { } timer || timer > 0 || !Map.IsValid() ) return;
		_selected = true;

		var markers = Map.Entities.OfType<RandomSpawn>()
			.Where( marker => marker.IsValid() && marker.TargetName == TargetName )
			.OrderBy( marker => marker.EntityIndex ).Take( MarkerLimit ).ToArray();

		// The original keeps the same pool size for every roll. A repeat spends a draw.
		for ( var draw = 0; draw < DrawCount && draw < markers.Length; draw++ )
		{
			var index = Game.Random.Int( 0, markers.Length - 1 );
			if ( markers[index] is not { } marker ) continue;
			markers[index] = null;
			Spawn( marker.ChildClass, marker.WorldTransform );
		}
	}

	void Spawn( string className, Transform transform, SavedChild saved = null )
	{
		if ( string.IsNullOrEmpty( className ) ) return;
		var prefab = GameObject.GetPrefab( $"mount://cof/entities/npcs/{className}.prefab" );
		if ( !prefab.IsValid() ) return;

		var actor = Monster.CloneSpawn( prefab, Map, transform, className );
		var npc = actor.Npc;
		if ( !npc.IsValid() )
		{
			actor.GameObject.Destroy();
			return;
		}

		if ( HitMonsterClip ) MonsterClip.Restrict( actor.GameObject );
		if ( !string.IsNullOrEmpty( ModelOverride ) && Model.Load( ModelOverride ) is { IsError: false } model )
		{
			npc.ModelPath = model.ResourcePath;
			npc.Renderer.Model = model;
		}
		npc.Body = saved?.Body ?? (className.Contains( "_slower" ) ? Game.Random.Int( 0, 2 ) : npc.Body);
		if ( saved is not null )
		{
			npc.Health = saved.Health;
			npc.Skin = saved.Skin;
			npc.RestoreMagazine( saved.Magazine );
		}
		_children.Add( npc );
		actor.GameObject.Enabled = true;
		actor.GameObject.NetworkSpawn( true, null );
	}
}
