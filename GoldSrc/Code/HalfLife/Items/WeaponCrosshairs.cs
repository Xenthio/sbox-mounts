using System;
using Sandbox.Rendering;

namespace GoldSrc.HalfLife;

internal sealed class WeaponCrosshairs
{
	internal sealed class Image( string atlas, Rect source )
	{
		private Texture _texture;

		internal void Draw( Painter painter, Vector2 center )
		{
			_texture ??= Texture.Load( atlas );
			if ( _texture is not { IsError: false } ) return;
			var topLeft = (center - source.Size * 0.5f).SnapToGrid( 1 );
			using var scope = painter.Scope();
			painter.Clip( new Rect( topLeft, source.Size ) );
			painter.Texture( _texture, new Rect( topLeft - source.Position, _texture.Size ), Color.White, FilterMode.Point );
		}
	}

	private readonly Dictionary<(int Tier, string State), Image> _images = [];

	internal WeaponCrosshairs( GameMount mount, string className )
	{
		var file = mount.FindFile( $"sprites/{className}.txt" );
		if ( file is null ) return;
		foreach ( var line in mount.FileSystem.ReadAllText( file ).Split( '\n' ) )
		{
			var row = line.Split( (char[])null, StringSplitOptions.RemoveEmptyEntries );
			if ( row.Length < 7 || row[0] is not ("crosshair" or "autoaim" or "zoom" or "zoom_autoaim") ) continue;
			if ( !int.TryParse( row[1], out var tier ) || !int.TryParse( row[3], out var x ) || !int.TryParse( row[4], out var y )
				|| !int.TryParse( row[5], out var w ) || !int.TryParse( row[6], out var h ) || w <= 0 || h <= 0 ) continue;
			if ( mount.FindFile( $"sprites/{row[2]}.spr" ) is not {} atlas ) continue;
			_images.TryAdd( (tier, row[0]), new Image( $"mount://{mount.Ident}/{atlas}/frame0.vtex", new Rect( x, y, w, h ) ) );
		}
	}

	internal (Image Normal, Image OnTarget) Select( Vector2 screen, bool zoomed )
	{
		int tier = screen.x > 2560 && screen.y > 1600 ? 2560 : screen.x >= 1280 && screen.y > 720 ? 1280 : screen.x >= 640 ? 640 : 320;
		while ( tier > 320 && !_images.ContainsKey( (tier, "crosshair") ) ) tier /= 2;
		var normal = _images.GetValueOrDefault( (tier, "crosshair") );
		if ( zoomed ) normal = _images.GetValueOrDefault( (tier, "zoom") ) ?? normal;
		return (normal, _images.GetValueOrDefault( (tier, zoomed ? "zoom_autoaim" : "autoaim") ));
	}
}

public abstract partial class StudioWeapon
{
	private WeaponCrosshairs _crosshairs;

	// Sandbox Mode exposes its HUD visibility policy; other games can draw the weapon HUD directly.
	protected static bool CrosshairVisible()
	{
		var visible = TypeLibrary.GetType( "Sandbox.Hud" )?.GetMethod( "IsVisible" );
		var element = TypeLibrary.GetType( "HudElement" );
		if ( visible is null || element?.IsEnum != true ) return true;
		var crosshair = TypeLibrary.GetEnumDescription( element.TargetType ).FirstOrDefault( e => e.Name == "Crosshair" ).ObjectValue;
		return crosshair is null || visible.InvokeWithReturn<bool>( null, [crosshair] );
	}

	public override void DrawCrosshair( Painter painter, Vector2 center )
	{
		if ( !CrosshairVisible() ) return;
		if ( _crosshairs is null )
		{
			if ( ContentMount?.GetByPath( PickupPath ) is not WeaponPickupLoader pickup ) return;
			_crosshairs = pickup.Crosshairs;
		}
		var (image, onTarget) = _crosshairs.Select( painter.Bounds.Size, Zoomed );
		if ( onTarget is not null && Owner.IsValid() && (Networking.MaxPlayers > 1 || Maps.SuitItem.Skill == 1) )
		{
			var hit = TraceLineOfFire( Owner.EyeTransform.ForwardRay, 8192, 0 ).UseHitboxes().Run().GameObject;
			if ( hit.IsValid() && (hit.GetComponentInParent<Sandbox.Npcs.BaseNpc>() is { Health: > 0 }
				|| hit.GetComponentInParent<PlayerController>() is {} player && Maps.MapState.PlayerVitals( player ) is { Health: > 0 }) ) image = onTarget;
		}
		image?.Draw( painter, center );
	}
}
