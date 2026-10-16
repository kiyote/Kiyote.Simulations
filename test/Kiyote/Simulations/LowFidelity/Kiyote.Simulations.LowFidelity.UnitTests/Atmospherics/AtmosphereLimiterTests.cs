namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereLimiterTests {

	private const int X = 2;
	private const int Y = 3;

	private IAtmosphereLimiter _limiter;
	private ChunkBuffer<float> _total;
	private ChunkBuffer<float> _flowEastPadded;
	private ChunkBuffer<float> _flowSouthPadded;
	private ChunkBuffer<float> _flowEast;
	private ChunkBuffer<float> _flowSouth;
	private ChunkBuffer<float> _vent;
	private ChunkBuffer<float> _scale;
	private ChunkBuffer<float> _scalePadded;

	[SetUp]
	public void SetUp() {
		_limiter = new AtmosphereLimiter();
		_total = ChunkBuffer<float>.Chunk();
		_total.Fill( 10.0f );
		_flowEastPadded = ChunkBuffer<float>.Padded();
		_flowSouthPadded = ChunkBuffer<float>.Padded();
		_flowEast = ChunkBuffer<float>.Chunk();
		_flowSouth = ChunkBuffer<float>.Chunk();
		_vent = ChunkBuffer<float>.Chunk();
		_scale = ChunkBuffer<float>.Chunk();
		_scalePadded = ChunkBuffer<float>.Padded();
	}

	[TestCase( true )]
	[TestCase( false )]
	public void ComputeScale_OutflowExceedsTotal_ScalesToTotal(
		bool full
	) {
		_flowEastPadded[X, Y] = 15.0f;
		_vent[X, Y] = 5.0f;

		ComputeScale( ChunkMask.Create( full, X, Y ) );

		Assert.That( _scale[X, Y], Is.EqualTo( 0.5f ).Within( 1e-5f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void ComputeScale_OutflowWithinTotal_IsOne(
		bool full
	) {
		_flowEastPadded[X, Y] = 4.0f;

		ComputeScale( ChunkMask.Create( full, X, Y ) );

		Assert.That( _scale[X, Y], Is.EqualTo( 1.0f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void ComputeScale_InflowFromWest_DoesNotCount(
		bool full
	) {
		_flowEastPadded[X - 1, Y] = 50.0f;

		ComputeScale( ChunkMask.Create( full, X, Y ) );

		Assert.That( _scale[X, Y], Is.EqualTo( 1.0f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void ComputeScale_OutflowWestAcrossBorder_Counts(
		bool full
	) {
		// The western chunk's last column pulls 20 from this chunk's first column.
		_flowEastPadded[-1, Y] = -20.0f;

		ComputeScale( ChunkMask.Create( full, 0, Y ) );

		Assert.That( _scale[0, Y], Is.EqualTo( 0.5f ).Within( 1e-5f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Limit_UsesScaleOfDrainingCell(
		bool full
	) {
		_scalePadded.Fill( 1.0f );
		_scalePadded[X, Y] = 0.5f;
		_scalePadded[X + 1, Y] = 0.25f;
		_flowEast[X, Y] = -8.0f;
		_flowSouth[X, Y] = 8.0f;
		_vent[X, Y] = 4.0f;

		Limit( ChunkMask.Create( full, X, Y ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _flowEast[X, Y], Is.EqualTo( -2.0f ).Within( 1e-5f ) );
			Assert.That( _flowSouth[X, Y], Is.EqualTo( 4.0f ).Within( 1e-5f ) );
			Assert.That( _vent[X, Y], Is.EqualTo( 2.0f ).Within( 1e-5f ) );
		}
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Limit_DrainingFromSouthChunk_UsesScaleAcrossBorder(
		bool full
	) {
		const int Last = AtmosphereSimd.ChunkSize - 1;
		_scalePadded.Fill( 1.0f );
		_scalePadded[X, Last + 1] = 0.25f;
		_flowSouth[X, Last] = -8.0f;

		Limit( ChunkMask.Create( full, X, Last ) );

		Assert.That( _flowSouth[X, Last], Is.EqualTo( -2.0f ).Within( 1e-5f ) );
	}

	private void ComputeScale(
		ulong[] mask
	) {
		_limiter.ComputeScale( mask, 1.0f, _total.Span, _flowEastPadded.Span, _flowSouthPadded.Span, _vent.Span, _scale.Span );
	}

	private void Limit(
		ulong[] mask
	) {
		_limiter.Limit( mask, _flowEast.Span, _flowSouth.Span, _vent.Span, _scalePadded.Span );
	}

}
