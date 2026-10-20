using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

namespace Kiyote.Simulations.LowFidelity.Thermals.UnitTests;

[TestFixture]
public sealed class ThermalSolarTests {

	private const float Flux = 1000.0f;

	private IThermalSolar _solar;

	[SetUp]
	public void SetUp() {
		_solar = new ThermalSolar();
	}

	[Test]
	public void Illuminate_SunToTheEast_OnlyEastFaceIsLit() {
		using Hull hull = new Hull( (0, 0), (1, 0), (2, 0) );

		Illuminate( hull, new Vector( 1.0f, 0.0f ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( hull.Solar[2, 0], Is.EqualTo( Flux ).Within( 1e-3f ) );
			Assert.That( hull.Solar[1, 0], Is.Zero );
			Assert.That( hull.Solar[0, 0], Is.Zero );
		}
	}

	[Test]
	public void Illuminate_SunToTheNorth_EveryNorthFaceIsLit() {
		using Hull hull = new Hull( (0, 0), (1, 0), (2, 0) );

		Illuminate( hull, new Vector( 0.0f, -1.0f ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( hull.Solar[0, 0], Is.EqualTo( Flux ).Within( 1e-3f ) );
			Assert.That( hull.Solar[1, 0], Is.EqualTo( Flux ).Within( 1e-3f ) );
			Assert.That( hull.Solar[2, 0], Is.EqualTo( Flux ).Within( 1e-3f ) );
		}
	}

	[Test]
	public void Illuminate_CellShadedFromAnotherChunk_IsNotLit() {
		// The upper cell sits in the chunk above and shades the lower one.
		const int Border = AtmosphereSimd.ChunkSize;
		using Hull hull = new Hull( (0, Border - 1), (0, Border + 1) );

		Illuminate( hull, new Vector( 0.0f, -1.0f ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( hull.Solar[0, Border - 1], Is.EqualTo( Flux ).Within( 1e-3f ) );
			Assert.That( hull.Solar[0, Border + 1], Is.Zero );
		}
	}

	[Test]
	public void Illuminate_NoSun_NothingIsLit() {
		using Hull hull = new Hull( (0, 0) );
		hull.Solar[0, 0] = 5.0f;

		Illuminate( hull, new Vector() );

		Assert.That( hull.Solar[0, 0], Is.Zero );
	}

	private void Illuminate(
		Hull hull,
		Vector sun
	) {
		_solar.Illuminate( hull.Layout, sun, Flux, hull.Absorptivity, hull.Vacuum, hull.Solar );
	}

	// Occupied cells, each open to vacuum on every face that does not touch another occupied cell.
	private sealed class Hull : IDisposable {

		private readonly ICompiledGridAssembly<float> _compiled;

		public Hull(
			params (int Column, int Row)[] cells
		) {
			int width = cells.Max( cell => cell.Column ) + 1;
			int height = cells.Max( cell => cell.Row ) + 1;
			DenseGridSource<float> source = new DenseGridSource<float>( width, height );
			foreach( (int column, int row) in cells ) {
				_ = source.TrySetCell( column, row, 0f );
			}
			_compiled = TestGridAssembly.Compile( source );
			Absorptivity = _compiled.CreateLayer<float>();
			Vacuum = _compiled.CreateVacuumLayer();
			Solar = _compiled.CreateLayer<float>();
			foreach( (int column, int row) in cells ) {
				Absorptivity[column, row] = 1.0f;
			}
		}

		public GridLayout Layout => _compiled.ChunkLayout;

		public IGridLayer<float> Absorptivity { get; }

		public IGridLayer<Direction> Vacuum { get; }

		public IGridLayer<float> Solar { get; }

		public void Dispose() {
			_compiled.Dispose();
		}

	}

}
