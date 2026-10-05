namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereLimiterTests {

	private const int X = 2;
	private const int Y = 3;

	private IAtmosphereLimiter _limiter;
	private ChunkLayer<float> _total;
	private ChunkLayer<float> _flowEast;
	private ChunkLayer<float> _flowSouth;
	private ChunkLayer<float> _vent;
	private ChunkLayer<float> _scale;

	[SetUp]
	public void SetUp() {
		_limiter = new AtmosphereLimiter();
		_total = new ChunkLayer<float>();
		_total.Fill( 10.0f );
		_flowEast = new ChunkLayer<float>();
		_flowSouth = new ChunkLayer<float>();
		_vent = new ChunkLayer<float>();
		_scale = new ChunkLayer<float>();
	}

	[TestCase( true )]
	[TestCase( false )]
	public void ComputeScale_OutflowExceedsTotal_ScalesToTotal(
		bool full
	) {
		_flowEast[X, Y] = 15.0f;
		_vent[X, Y] = 5.0f;

		_limiter.ComputeScale( 0, Mask( full ), 1.0f, _total, _flowEast, _flowSouth, _vent, _scale );

		Assert.That( _scale[X, Y], Is.EqualTo( 0.5f ).Within( 1e-5f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void ComputeScale_OutflowWithinTotal_IsOne(
		bool full
	) {
		_flowEast[X, Y] = 4.0f;

		_limiter.ComputeScale( 0, Mask( full ), 1.0f, _total, _flowEast, _flowSouth, _vent, _scale );

		Assert.That( _scale[X, Y], Is.EqualTo( 1.0f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void ComputeScale_InflowFromWest_DoesNotCount(
		bool full
	) {
		_flowEast[X - 1, Y] = 50.0f;

		_limiter.ComputeScale( 0, Mask( full ), 1.0f, _total, _flowEast, _flowSouth, _vent, _scale );

		Assert.That( _scale[X, Y], Is.EqualTo( 1.0f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Limit_UsesScaleOfDrainingCell(
		bool full
	) {
		_scale.Fill( 1.0f );
		_scale[X, Y] = 0.5f;
		_scale[X + 1, Y] = 0.25f;
		_flowEast[X, Y] = -8.0f;
		_flowSouth[X, Y] = 8.0f;
		_vent[X, Y] = 4.0f;

		_limiter.Limit( 0, Mask( full ), _flowEast, _flowSouth, _vent, _scale );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _flowEast[X, Y], Is.EqualTo( -2.0f ).Within( 1e-5f ) );
			Assert.That( _flowSouth[X, Y], Is.EqualTo( 4.0f ).Within( 1e-5f ) );
			Assert.That( _vent[X, Y], Is.EqualTo( 2.0f ).Within( 1e-5f ) );
		}
	}

	private static ulong[] Mask(
		bool full
	) {
		return full ? ChunkMask.Full() : ChunkMask.Cell( X, Y );
	}

}
