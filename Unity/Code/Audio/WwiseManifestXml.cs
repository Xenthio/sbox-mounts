using System.Text;

namespace UnityMount.Wwise;

/// <summary>Reads the XML subset used by generated Wwise catalogs. External declarations are unsupported.</summary>
sealed class WwiseManifestXml( string text )
{
	internal sealed class Element( string name )
	{
		readonly Dictionary<string, string> _attributes = new( StringComparer.Ordinal );
		readonly List<Element> _children = new();
		public string Name { get; } = name;
		public string Value { get; set; }
		public string Attribute( string name ) => _attributes.GetValueOrDefault( name );
		public Element Child( string name ) => _children.FirstOrDefault( child => child.Name == name );
		public IEnumerable<Element> Children( string name ) => _children.Where( child => child.Name == name );
		public void Add( Element child ) => _children.Add( child );
		public bool SetAttribute( string name, string value ) => _attributes.TryAdd( name, value );
	}

	int _offset;

	public Element Read()
	{
		SkipMisc();
		var element = ReadElement( 0 );
		SkipMisc();
		if ( _offset != text.Length ) throw InvalidXml();
		return element;
	}

	Element ReadElement( int depth )
	{
		if ( depth > 64 || !Take( "<" ) ) throw InvalidXml();
		var element = new Element( ReadName() );
		while ( true )
		{
			SkipSpace();
			if ( Take( "/>" ) ) return element;
			if ( Take( ">" ) ) break;
			var key = ReadName();
			SkipSpace();
			if ( !Take( "=" ) ) throw InvalidXml();
			SkipSpace();
			if ( _offset == text.Length || text[_offset] is not ('\'' or '"') ) throw InvalidXml();
			var quote = text[_offset++];
			var end = text.IndexOf( quote, _offset );
			if ( end < 0 || !element.SetAttribute( key, Decode( text[_offset..end] ) ) ) throw InvalidXml();
			_offset = end + 1;
		}

		StringBuilder value = null;
		while ( _offset < text.Length )
		{
			if ( Take( "</" ) )
			{
				var closing = ReadName();
				SkipSpace();
				if ( closing != element.Name || !Take( ">" ) ) throw InvalidXml();
				element.Value = value?.ToString();
				return element;
			}
			if ( Take( "<!--" ) )
			{
				SkipThrough( "-->" );
				continue;
			}
			if ( text[_offset] == '<' )
			{
				element.Add( ReadElement( depth + 1 ) );
				continue;
			}

			var end = text.IndexOf( '<', _offset );
			if ( end < 0 ) throw InvalidXml();
			var segment = text.AsSpan( _offset, end - _offset );
			if ( !segment.IsWhiteSpace() ) (value ??= new()).Append( Decode( segment.ToString() ) );
			_offset = end;
		}
		throw InvalidXml();
	}

	void SkipMisc()
	{
		while ( true )
		{
			SkipSpace();
			if ( Take( "<?" ) ) SkipThrough( "?>" );
			else if ( Take( "<!--" ) ) SkipThrough( "-->" );
			else break;
		}
	}

	void SkipThrough( string terminator )
	{
		var end = text.IndexOf( terminator, _offset, StringComparison.Ordinal );
		if ( end < 0 ) throw InvalidXml();
		_offset = end + terminator.Length;
	}

	void SkipSpace()
	{
		while ( _offset < text.Length && char.IsWhiteSpace( text[_offset] ) ) _offset++;
	}

	bool Take( string token )
	{
		if ( !text.AsSpan( _offset ).StartsWith( token, StringComparison.Ordinal ) ) return false;
		_offset += token.Length;
		return true;
	}

	string ReadName()
	{
		var start = _offset;
		while ( _offset < text.Length && (char.IsAsciiLetterOrDigit( text[_offset] ) || text[_offset] is '_' or '-' or ':' or '.') )
			_offset++;
		if ( _offset == start ) throw InvalidXml();
		return text[start.._offset];
	}

	static string Decode( string value ) => value.Contains( '&' ) ? System.Net.WebUtility.HtmlDecode( value ) : value;

	InvalidDataException InvalidXml() => new( $"Invalid Wwise manifest XML at offset {_offset}." );
}
