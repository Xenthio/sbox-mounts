// Endpoint bit layouts and partition anchors adapted from bcdec by Sergii Kudlai.
// https://github.com/iOrange/bcdec/tree/80859ed3b7afb1c527a2a99d70c61457bea72d0c
// See ThirdParty/bcdec.txt.

namespace UnityMount;

static partial class UnityBc6h
{
	// Each mask bit selects an endpoint pair for that texel. The second subset's anchor
	// stores one fewer interpolation-index bit, as does pixel zero for the first subset.
	static readonly Partition[] Partitions =
	[
		new( 0xcccc, 15 ), new( 0x8888, 15 ), new( 0xeeee, 15 ), new( 0xecc8, 15 ),
		new( 0xc880, 15 ), new( 0xfeec, 15 ), new( 0xfec8, 15 ), new( 0xec80, 15 ),
		new( 0xc800, 15 ), new( 0xffec, 15 ), new( 0xfe80, 15 ), new( 0xe800, 15 ),
		new( 0xffe8, 15 ), new( 0xff00, 15 ), new( 0xfff0, 15 ), new( 0xf000, 15 ),
		new( 0xf710, 15 ), new( 0x008e, 2 ), new( 0x7100, 8 ), new( 0x08ce, 2 ),
		new( 0x008c, 2 ), new( 0x7310, 8 ), new( 0x3100, 8 ), new( 0x8cce, 15 ),
		new( 0x088c, 2 ), new( 0x3110, 8 ), new( 0x6666, 2 ), new( 0x366c, 2 ),
		new( 0x17e8, 8 ), new( 0x0ff0, 8 ), new( 0x718e, 2 ), new( 0x399c, 2 ),
	];

	static readonly Mode[] Modes =
	[
		// Mode 1 (0b00).
		new( 0b00, 10, 5, 5, 5,
		[
			new( G2, 1, 4 ), new( B2, 1, 4 ), new( B3, 1, 4 ), new( R0, 10 ),
			new( G0, 10 ), new( B0, 10 ), new( R1, 5 ), new( G3, 1, 4 ),
			new( G2, 4 ), new( G1, 5 ), new( B3, 1 ), new( G3, 4 ),
			new( B1, 5 ), new( B3, 1, 1 ), new( B2, 4 ), new( R2, 5 ),
			new( B3, 1, 2 ), new( R3, 5 ), new( B3, 1, 3 ),
		] ),
		// Mode 2 (0b01).
		new( 0b01, 7, 6, 6, 6,
		[
			new( G2, 1, 5 ), new( G3, 1, 4 ), new( G3, 1, 5 ), new( R0, 7 ),
			new( B3, 1 ), new( B3, 1, 1 ), new( B2, 1, 4 ), new( G0, 7 ),
			new( B2, 1, 5 ), new( B3, 1, 2 ), new( G2, 1, 4 ), new( B0, 7 ),
			new( B3, 1, 3 ), new( B3, 1, 5 ), new( B3, 1, 4 ), new( R1, 6 ),
			new( G2, 4 ), new( G1, 6 ), new( G3, 4 ), new( B1, 6 ),
			new( B2, 4 ), new( R2, 6 ), new( R3, 6 ),
		] ),
		// Mode 3 (0b00010).
		new( 0b00010, 11, 5, 4, 4,
		[
			new( R0, 10 ), new( G0, 10 ), new( B0, 10 ), new( R1, 5 ),
			new( R0, 1, 10 ), new( G2, 4 ), new( G1, 4 ), new( G0, 1, 10 ),
			new( B3, 1 ), new( G3, 4 ), new( B1, 4 ), new( B0, 1, 10 ),
			new( B3, 1, 1 ), new( B2, 4 ), new( R2, 5 ), new( B3, 1, 2 ),
			new( R3, 5 ), new( B3, 1, 3 ),
		] ),
		// Mode 4 (0b00110).
		new( 0b00110, 11, 4, 5, 4,
		[
			new( R0, 10 ), new( G0, 10 ), new( B0, 10 ), new( R1, 4 ),
			new( R0, 1, 10 ), new( G3, 1, 4 ), new( G2, 4 ), new( G1, 5 ),
			new( G0, 1, 10 ), new( G3, 4 ), new( B1, 4 ), new( B0, 1, 10 ),
			new( B3, 1, 1 ), new( B2, 4 ), new( R2, 4 ), new( B3, 1 ),
			new( B3, 1, 2 ), new( R3, 4 ), new( G2, 1, 4 ), new( B3, 1, 3 ),
		] ),
		// Mode 5 (0b01010).
		new( 0b01010, 11, 4, 4, 5,
		[
			new( R0, 10 ), new( G0, 10 ), new( B0, 10 ), new( R1, 4 ),
			new( R0, 1, 10 ), new( B2, 1, 4 ), new( G2, 4 ), new( G1, 4 ),
			new( G0, 1, 10 ), new( B3, 1 ), new( G3, 4 ), new( B1, 5 ),
			new( B0, 1, 10 ), new( B2, 4 ), new( R2, 4 ), new( B3, 1, 1 ),
			new( B3, 1, 2 ), new( R3, 4 ), new( B3, 1, 4 ), new( B3, 1, 3 ),
		] ),
		// Mode 6 (0b01110).
		new( 0b01110, 9, 5, 5, 5,
		[
			new( R0, 9 ), new( B2, 1, 4 ), new( G0, 9 ), new( G2, 1, 4 ),
			new( B0, 9 ), new( B3, 1, 4 ), new( R1, 5 ), new( G3, 1, 4 ),
			new( G2, 4 ), new( G1, 5 ), new( B3, 1 ), new( G3, 4 ),
			new( B1, 5 ), new( B3, 1, 1 ), new( B2, 4 ), new( R2, 5 ),
			new( B3, 1, 2 ), new( R3, 5 ), new( B3, 1, 3 ),
		] ),
		// Mode 7 (0b10010).
		new( 0b10010, 8, 6, 5, 5,
		[
			new( R0, 8 ), new( G3, 1, 4 ), new( B2, 1, 4 ), new( G0, 8 ),
			new( B3, 1, 2 ), new( G2, 1, 4 ), new( B0, 8 ), new( B3, 1, 3 ),
			new( B3, 1, 4 ), new( R1, 6 ), new( G2, 4 ), new( G1, 5 ),
			new( B3, 1 ), new( G3, 4 ), new( B1, 5 ), new( B3, 1, 1 ),
			new( B2, 4 ), new( R2, 6 ), new( R3, 6 ),
		] ),
		// Mode 8 (0b10110).
		new( 0b10110, 8, 5, 6, 5,
		[
			new( R0, 8 ), new( B3, 1 ), new( B2, 1, 4 ), new( G0, 8 ),
			new( G2, 1, 5 ), new( G2, 1, 4 ), new( B0, 8 ), new( G3, 1, 5 ),
			new( B3, 1, 4 ), new( R1, 5 ), new( G3, 1, 4 ), new( G2, 4 ),
			new( G1, 6 ), new( G3, 4 ), new( B1, 5 ), new( B3, 1, 1 ),
			new( B2, 4 ), new( R2, 5 ), new( B3, 1, 2 ), new( R3, 5 ),
			new( B3, 1, 3 ),
		] ),
		// Mode 9 (0b11010).
		new( 0b11010, 8, 5, 5, 6,
		[
			new( R0, 8 ), new( B3, 1, 1 ), new( B2, 1, 4 ), new( G0, 8 ),
			new( B2, 1, 5 ), new( G2, 1, 4 ), new( B0, 8 ), new( B3, 1, 5 ),
			new( B3, 1, 4 ), new( R1, 5 ), new( G3, 1, 4 ), new( G2, 4 ),
			new( G1, 5 ), new( B3, 1 ), new( G3, 4 ), new( B1, 6 ),
			new( B2, 4 ), new( R2, 5 ), new( B3, 1, 2 ), new( R3, 5 ),
			new( B3, 1, 3 ),
		] ),
		// Mode 10 (0b11110).
		new( 0b11110, 6, 6, 6, 6,
		[
			new( R0, 6 ), new( G3, 1, 4 ), new( B3, 1 ), new( B3, 1, 1 ),
			new( B2, 1, 4 ), new( G0, 6 ), new( G2, 1, 5 ), new( B2, 1, 5 ),
			new( B3, 1, 2 ), new( G2, 1, 4 ), new( B0, 6 ), new( G3, 1, 5 ),
			new( B3, 1, 3 ), new( B3, 1, 5 ), new( B3, 1, 4 ), new( R1, 6 ),
			new( G2, 4 ), new( G1, 6 ), new( G3, 4 ), new( B1, 6 ),
			new( B2, 4 ), new( R2, 6 ), new( R3, 6 ),
		] ),
		// Mode 11 (0b00011).
		new( 0b00011, 10, 10, 10, 10,
		[
			new( R0, 10 ), new( G0, 10 ), new( B0, 10 ), new( R1, 10 ),
			new( G1, 10 ), new( B1, 10 ),
		] ),
		// Mode 12 (0b00111).
		new( 0b00111, 11, 9, 9, 9,
		[
			new( R0, 10 ), new( G0, 10 ), new( B0, 10 ), new( R1, 9 ),
			new( R0, 1, 10 ), new( G1, 9 ), new( G0, 1, 10 ), new( B1, 9 ),
			new( B0, 1, 10 ),
		] ),
		// Mode 13 (0b01011).
		new( 0b01011, 12, 8, 8, 8,
		[
			new( R0, 10 ), new( G0, 10 ), new( B0, 10 ), new( R1, 8 ),
			new( R0, 2, 10, true ), new( G1, 8 ), new( G0, 2, 10, true ), new( B1, 8 ),
			new( B0, 2, 10, true ),
		] ),
		// Mode 14 (0b01111).
		new( 0b01111, 16, 4, 4, 4,
		[
			new( R0, 10 ), new( G0, 10 ), new( B0, 10 ), new( R1, 4 ),
			new( R0, 6, 10, true ), new( G1, 4 ), new( G0, 6, 10, true ), new( B1, 4 ),
			new( B0, 6, 10, true ),
		] ),
	];

	static readonly Dictionary<int, Mode> ModesByEncoding = Modes.ToDictionary( mode => mode.Encoding );
}
