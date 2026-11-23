using System;

namespace GoldSrc;

public partial class TeamFortressClassicMount
{
	static readonly string[] SharedModels = ["models/rpgrocket.mdl", "models/shotgunshell.mdl", "models/shell.mdl",
		"models/v_rpg.mdl", "models/w_rpg.mdl", "models/w_egon.mdl", "models/v_grenade.mdl", "models/w_grenade.mdl"];
	static readonly string[] SharedSounds =
	[
		"weapons/357_cock1.wav", "weapons/reload1.wav", "weapons/reload3.wav", "weapons/scock1.wav",
		"weapons/rocket1.wav", "weapons/rocketfire1.wav", "weapons/sbarrel1.wav",
		"weapons/glauncher.wav", "weapons/pl_gun3.wav", "ambience/rifle1.wav",
		"ambience/fire1.wav", "ambience/flameburst1.wav",
		"weapons/cbar_hit1.wav", "weapons/cbar_hit2.wav", "weapons/cbar_hitbod1.wav",
		"weapons/cbar_hitbod2.wav", "weapons/cbar_hitbod3.wav", "weapons/cbar_miss1.wav",
		"items/cliprelease1.wav", "items/clipinsert1.wav", "items/ammopickup2.wav",
		"items/medshot4.wav", "items/medshot5.wav", "ambience/steamburst1.wav",
		"weapons/grenade_hit1.wav", "weapons/grenade_hit2.wav", "weapons/grenade_hit3.wav",
		"weapons/explode3.wav", "weapons/explode4.wav", "weapons/explode5.wav",
		"weapons/mine_activate.wav", "weapons/mine_charge.wav", "weapons/mortarhit.wav",
		"weapons/debris1.wav", "weapons/debris2.wav", "weapons/debris3.wav",
		"player/pl_shell1.wav", "player/pl_shell2.wav", "player/pl_shell3.wav"
	];
	static readonly string[] SharedSprites = ["sprites/smoke.spr", "sprites/muzzleflash1.spr", "sprites/muzzleflash2.spr",
		"sprites/laserdot.spr", "sprites/crosshairs.spr", "sprites/WXplo1.spr", "sprites/bubble.spr", "sprites/shockwave.spr",
		"sprites/320hud1.spr", "sprites/640hud1.spr", "sprites/flare3.spr"];

	protected override string FindFallbackFile( string relativePath )
	{
		var relative = relativePath.Replace( '\\', '/' );
		if ( !relative.Equals( "gfx/palette.lmp", StringComparison.OrdinalIgnoreCase )
			&& !SharedModels.Contains( relative, StringComparer.OrdinalIgnoreCase )
			&& !SharedSprites.Contains( relative, StringComparer.OrdinalIgnoreCase ) ) return null;

		var path = $"valve/{relative}";
		return FileSystem.FileExists( path ) ? path : null;
	}

	protected override Task Mount( MountContext context )
	{
		if ( FileSystem is null ) return Task.CompletedTask;

		foreach ( var relative in SharedModels )
		{
			if ( FindFile( relative ) is { } path && path.StartsWith( "valve/", StringComparison.Ordinal ) )
				context.Add( ResourceType.Model, path, new ModelLoader( path, relative.StartsWith( "models/v_", StringComparison.Ordinal ) ) );
		}
		foreach ( var sample in SharedSounds )
		{
			var relative = $"sound/{sample}";
			if ( FindFile( relative ) is not null ) continue;
			var path = $"valve/{relative}";
			if ( FileSystem.FileExists( path ) ) context.Add( ResourceType.Sound, $"tfc/{relative}", new WavSoundLoader( path ) );
		}

		var mounting = base.Mount( context );
		foreach ( var relative in SharedSprites )
		{
			if ( FindFile( relative ) is { } path && path.StartsWith( "valve/", StringComparison.Ordinal ) )
				MountSprite( context, path );
		}
		return mounting;
	}
}
