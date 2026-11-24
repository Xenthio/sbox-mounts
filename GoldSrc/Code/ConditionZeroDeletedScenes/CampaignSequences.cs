using System;
using System.Globalization;
using System.Text.RegularExpressions;
using GoldSrc.Maps;

namespace GoldSrc.ConditionZeroDeletedScenes;

internal sealed class CampaignSequences( ConditionZeroDeletedScenesMount mount )
{
	static readonly Regex Comments = new( """
		"(?:\\.|[^"\\])*"|//[^\r\n]*|/\*[\s\S]*?\*/
		""" );
	static readonly Regex Blocks = new( """(?m)^\s*([%!]\s*[^\s{]+)\s*\{((?:"(?:\\.|[^"\\])*"|[^"{}])*)\}""" );
	static readonly Regex Markers = new( """
		"(?:\\.|[^"\\])*"|([#$@])([a-zA-Z_][\w.-]*)
		""" );
	static readonly Regex Fields = new( """(?:^|,)\s*([\w]+)\s*=\s*("(?:\\.|[^"\\])*"|[^,\r\n]+)""" );
	static readonly Regex LocalizedTokens = new( """(?m)^\s*"([^"]+)"[ \t]+"((?:\\.|[^"\\])*)"\s*$""" );

	readonly Dictionary<string, Source> _sources = new( StringComparer.OrdinalIgnoreCase );
	Dictionary<string, string> _localized;

	internal sealed record Cue( float Pause, string Sound, string Speaker, string Listener, string FireTargets, string KillTargets,
		TitleMessage Text, bool Repeat );
	internal sealed record Program( Cue[] Cues, string Source );
	sealed record Source( Dictionary<string, Directive[]> Blocks, Directive[] Defaults );
	sealed record Directive( char Mode, string Name, Dictionary<string, string> Fields );

	internal void EnsureSpeech( string file ) => Read( file );

	internal Program Find( string file, string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) ) return null;
		var local = Read( file );
		var global = Read( "global" );
		var commands = local.Blocks.GetValueOrDefault( id );
		if ( commands is null )
		{
			commands = global.Blocks.GetValueOrDefault( id );
			file = "global";
			local = global;
		}
		if ( commands is null ) return null;

		var defaults = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
		ApplyDefaults( defaults, global.Defaults );
		if ( local != global ) ApplyDefaults( defaults, local.Defaults );
		var cues = new List<Cue>();
		foreach ( var command in commands )
		{
			if ( command.Mode == '$' )
			{
				Merge( defaults, command.Fields );
				continue;
			}
			if ( command.Mode == '@' )
			{
				var style = local.Blocks.GetValueOrDefault( command.Name ) ?? global.Blocks.GetValueOrDefault( command.Name );
				if ( style is not null ) ApplyDefaults( defaults, style );
				continue;
			}

			var fields = new Dictionary<string, string>( defaults, StringComparer.OrdinalIgnoreCase );
			Merge( fields, command.Fields );
			cues.Add( new Cue( Math.Max( 0, Number( fields, "pause" ) ), fields.GetValueOrDefault( "sound" ),
				fields.GetValueOrDefault( "speaker" ), fields.GetValueOrDefault( "listener" ), fields.GetValueOrDefault( "firetargets" ),
				fields.GetValueOrDefault( "killtargets" ), Text( fields ), Number( fields, "repeat" ) != 0 ) );
		}
		return new( cues.ToArray(), file );
	}

	Source Read( string file )
	{
		file ??= "";
		if ( _sources.TryGetValue( file, out var cached ) ) return cached;
		var path = file.Length > 0 && file.All( c => char.IsAsciiLetterOrDigit( c ) || c is '_' or '-' )
			? mount.FindFile( $"sequences/{file}.seq" ) : null;
		var content = path is null ? "" : mount.FileSystem.ReadAllText( path );
		content = Comments.Replace( content, match => match.Value[0] == '"' ? match.Value : " " );

		var blocks = new Dictionary<string, Directive[]>( StringComparer.OrdinalIgnoreCase );
		var defaults = new List<Directive>();
		var cursor = 0;
		foreach ( Match block in Blocks.Matches( content ) )
		{
			defaults.AddRange( ParseDirectives( content[cursor..block.Index] ) );
			cursor = block.Index + block.Length;
			var marker = block.Groups[1].Value;
			var name = marker[1..].Trim();
			var body = block.Groups[2].Value;
			if ( marker[0] == '%' ) blocks[name] = ParseDirectives( body );
			else if ( name.Equals( "sentences", StringComparison.OrdinalIgnoreCase ) )
				mount.NpcAudio.IndexSequenceSpeech( file, body );
		}
		defaults.AddRange( ParseDirectives( content[cursor..] ) );
		return _sources[file] = new( blocks, defaults.ToArray() );
	}

	static Directive[] ParseDirectives( string text )
	{
		var markers = Markers.Matches( text ).Where( match => match.Groups[1].Success ).ToArray();
		var result = new Directive[markers.Length];
		for ( var i = 0; i < markers.Length; i++ )
		{
			var marker = markers[i];
			var mode = marker.Groups[1].Value[0];
			var name = marker.Groups[2].Value;
			var end = i + 1 < markers.Length ? markers[i + 1].Index : text.Length;
			var fields = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
			if ( mode != '@' )
			{
				var clauses = name + text[(marker.Index + marker.Length)..end];
				foreach ( Match field in Fields.Matches( clauses ) )
					fields[field.Groups[1].Value] = Unquote( field.Groups[2].Value.Trim() );
			}
			result[i] = new( mode, name, fields );
		}
		return result;
	}

	static void ApplyDefaults( Dictionary<string, string> settings, IEnumerable<Directive> directives )
	{
		foreach ( var directive in directives )
			if ( directive.Mode == '$' ) Merge( settings, directive.Fields );
	}

	static void Merge( Dictionary<string, string> settings, Dictionary<string, string> fields )
	{
		foreach ( var (key, value) in fields ) settings[key] = value;
	}

	TitleMessage Text( Dictionary<string, string> fields )
	{
		var text = fields.GetValueOrDefault( "text" );
		if ( string.IsNullOrEmpty( text ) ) return default;
		if ( text.StartsWith( '#' ) )
		{
			_localized ??= ReadLocalizedTokens();
			text = _localized.GetValueOrDefault( text[1..], text );
		}

		var position = Components( fields, "position" );
		var hold = Number( fields, "holdtime", 2 );
		return new TitleMessage
		{
			Text = text, Position = position.Length == 2 ? new( position[0], position[1] ) : new( 0.8f, 0.8f ),
			Color = ReadColor( fields, "color", new Color32( 240, 100, 0, 255 ) ),
			Color2 = ReadColor( fields, "color2", new Color32( 0, 100, 240, 255 ) ),
			Effect = (int)Number( fields, "effect" ), FadeIn = Number( fields, "fadein", 1.5f ),
			FadeOut = Number( fields, "fadeout", 0.5f ), HoldTime = hold < 0 ? text.Length * 0.05f + 1 : hold,
			FxTime = Number( fields, "fxtime", 0.25f )
		};
	}

	Dictionary<string, string> ReadLocalizedTokens()
	{
		var tokens = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
		if ( mount.FindFile( "resource/czeror_english.txt" ) is not { } path ) return tokens;
		foreach ( Match match in LocalizedTokens.Matches( mount.FileSystem.ReadAllText( path ) ) )
			tokens[match.Groups[1].Value] = Unquote( match.Groups[2].Value );
		return tokens;
	}

	static string Unquote( string value ) => value.Trim( '"' ).Replace( @"\n", "\n" ).Replace( "\\\"", "\"" );

	static float ParseNumber( string value, float fallback = 0 ) =>
		float.TryParse( value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed ) && float.IsFinite( parsed ) ? parsed : fallback;

	static float Number( Dictionary<string, string> fields, string key, float fallback = 0 ) =>
		ParseNumber( fields.GetValueOrDefault( key ), fallback );

	static float[] Components( Dictionary<string, string> fields, string key ) =>
		fields.GetValueOrDefault( key )?.Split( [' ', '\t'], StringSplitOptions.RemoveEmptyEntries ).Select( item => ParseNumber( item ) ).ToArray() ?? [];

	static Color ReadColor( Dictionary<string, string> fields, string key, Color fallback )
	{
		var channels = Components( fields, key );
		return channels.Length == 3 ? new Color( channels[0] / 255, channels[1] / 255, channels[2] / 255 ) : fallback;
	}
}
