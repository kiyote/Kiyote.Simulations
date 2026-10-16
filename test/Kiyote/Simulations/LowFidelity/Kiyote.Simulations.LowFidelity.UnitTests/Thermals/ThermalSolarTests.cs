using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;

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
		Hull hull = new Hull( (0, 0), (1, 0), (2, 0) );

		Illuminate( hull, new Vector( 1.0f, 0.0f ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( hull.Solar.GetReference( 2, 0 ), Is.EqualTo( Flux ).Within( 1e-3f ) );
			Assert.That( hull.Solar.GetReference( 1, 0 ), Is.Zero );
			Assert.That( hull.Solar.GetReference( 0, 0 ), Is.Zero );
		}
	}

	[Test]
	public void Illuminate_SunToTheNorth_EveryNorthFaceIsLit() {
		Hull hull = new Hull( (0, 0), (1, 0), (2, 0) );

		Illuminate( hull, new Vector( 0.0f, -1.0f ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( hull.Solar.GetReference( 0, 0 ), Is.EqualTo( Flux ).Within( 1e-3f ) );
			Assert.That( hull.Solar.GetReference( 1, 0 ), Is.EqualTo( Flux ).Within( 1e-3f ) );
			Assert.That( hull.Solar.GetReference( 2, 0 ), Is.EqualTo( Flux ).Within( 1e-3f ) );
		}
	}

	[Test]
	public void Illuminate_CellShadedFromAnotherChunk_IsNotLit() {
		// The upper cell sits in the chunk above and shades the lower one.
		const int Border = AtmosphereSimd.ChunkSize;
		Hull hull = new Hull( (0, Border - 1), (0, Border + 1) );

		Illuminate( hull, new Vector( 0.0f, -1.0f ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( hull.Solar.GetReference( 0, Border - 1 ), Is.EqualTo( Flux ).Within( 1e-3f ) );
			Assert.That( hull.Solar.GetReference( 0, Border + 1 ), Is.Zero );
		}
	}

	[Test]
	public void Illuminate_NoSun_NothingIsLit() {
		Hull hull = new Hull( (0, 0) );
		hull.Solar.GetReference( 0, 0 ) = 5.0f;

		Illuminate( hull, new Vector() );

		Assert.That( hull.Solar.GetReference( 0, 0 ), Is.Zero );
	}

	private void Illuminate(
		Hull hull,
		Vector sun
	) {
		_solar.Illuminate( hull.Layout, sun, Flux, hull.Absorptivity, hull.Vacuum, hull.Solar );
	}

	// Occupied cells, each open to vacuum on every face that does not touch another occupied cell.
	private sealed class Hull {

		public Hull(
			params (int Column, int Row)[] cells
		) {
			Layout = new ChunkedGridLayout( AtmosphereSimd.ChunkSize );
			foreach( (int column, int row) in cells ) {
				_ = Layout.SetPopulated( column, row );
			}
			Absorptivity = new ChunkedGridLayer<float>( Layout );
			Vacuum = new ChunkedGridLayer<Direction>( Layout );
			Solar = new ChunkedGridLayer<float>( Layout );
			foreach( (int column, int row) in cells ) {
				Absorptivity.GetReference( column, row ) = 1.0f;
				Direction open = Direction.None;
				if( !Layout.IsPopulated( column, row - 1 ) ) {
					open |= Direction.North;
				}
				if( !Layout.IsPopulated( column + 1, row ) ) {
					open |= Direction.East;
				}
				if( !Layout.IsPopulated( column, row + 1 ) ) {
					open |= Direction.South;
				}
				if( !Layout.IsPopulated( column - 1, row ) ) {
					open |= Direction.West;
				}
				Vacuum.GetReference( column, row ) = open;
			}
		}

		public ChunkedGridLayout Layout { get; }

		public ChunkedGridLayer<float> Absorptivity { get; }

		public ChunkedGridLayer<Direction> Vacuum { get; }

		public ChunkedGridLayer<float> Solar { get; }

	}

}
