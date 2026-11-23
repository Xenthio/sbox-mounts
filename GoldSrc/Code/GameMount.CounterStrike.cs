using System;

namespace GoldSrc;

public abstract class CounterStrikeGameMount : GameMount
{
	const string ShotgunShell = "models/shotgunshell.mdl";
	static readonly string[] SharedSprites = ["sprites/fexplo.spr", "sprites/eexplo.spr", "sprites/steam1.spr", "sprites/bubble.spr", "sprites/glow01.spr"];
	static readonly string[] SharedSounds =
	[
		"items/cliprelease1.wav", "weapons/reload1.wav", "weapons/reload3.wav",
		"weapons/sshell1.wav", "weapons/sshell2.wav", "weapons/sshell3.wav",
		"weapons/debris1.wav", "weapons/debris2.wav", "weapons/debris3.wav"
	];

	protected override string FindFallbackFile( string relativePath )
	{
		if ( !string.Equals( relativePath, ShotgunShell, StringComparison.OrdinalIgnoreCase ) &&
			!SharedSprites.Contains( relativePath, StringComparer.OrdinalIgnoreCase ) ) return null;
		var path = $"valve/{relativePath}";
		return FileSystem.FileExists( path ) ? path : null;
	}

	protected override Task Mount( MountContext context )
	{
		if ( FileSystem is null ) return Task.CompletedTask;

		if ( FindFile( ShotgunShell ) is { } shell && shell.StartsWith( "valve/", StringComparison.Ordinal ) )
			context.Add( ResourceType.Model, shell, new ModelLoader( shell ) );
		foreach ( var sample in SharedSounds )
		{
			var relative = $"sound/{sample}";
			var path = $"valve/{relative}";
			if ( FindFile( relative ) is null && FileSystem.FileExists( path ) )
				context.Add( ResourceType.Sound, $"{GameDirs[^1]}/{relative}", new WavSoundLoader( path ) );
		}
		CounterStrike.SniperScope.Mount( this, context );

		return base.Mount( context );
	}
}
