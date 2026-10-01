using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Advection;
using Kiyote.Simulations.Topology.Pressure;
using Kiyote.Simulations.Topology.Projection;

namespace Kiyote.Simulations.Topology.Airflow;

public sealed class GridAirflow : IGridAirflow {

	// How strongly the species' pressure gradient accelerates the bulk velocity.
	private const float PressureForce = 0.05f;
	private const int Block = 16;

	private readonly IGridPressure _pressure;
	private readonly IGridProjection _projection;
	private readonly IGridAdvection _advection;
	private readonly float _forceScale;

	public GridAirflow(
		IGridPressure pressure,
		IGridProjection projection,
		IGridAdvection advection,
		ISimulationClock clock
	) {
		_pressure = pressure;
		_projection = projection;
		_advection = advection;
		_forceScale = clock.FixedTimeStep * PressureForce * 0.5f;
	}

	AirflowNeighbourhood IGridAirflow.CreateNeighbourhood<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		IGridLayer<Direction> connectivity
	) {
		ProjectionNeighbourhood projection = _projection.CreateNeighbourhood( compiled, connectivity );
		AdvectionNeighbourhood advection = _advection.CreateNeighbourhood( compiled, connectivity );
		// Pressure and projection both use the combined connectivity | vacuum layer.
		return new AirflowNeighbourhood( projection.Combined, projection, advection );
	}

	void IGridAirflow.Update(
		AirflowNeighbourhood neighbourhood,
		IGridLayer<float> pressureSource,
		IGridLayer<float> pressureDestination,
		IGridLayer<float> sourceVelocityX,
		IGridLayer<float> sourceVelocityY,
		IGridLayer<float> destinationVelocityX,
		IGridLayer<float> destinationVelocityY,
		IGridLayer<float> intermediateVelocityX,
		IGridLayer<float> intermediateVelocityY,
		IGridLayer<float> projectionPressure,
		IGridLayer<float> projectionPressureScratch,
		IGridLayer<float> projectionDivergence,
		IGridLayer<float> concentrationSource,
		IGridLayer<float> concentrationDestination
	) {
		_pressure.Update( neighbourhood.Pressure, pressureSource, pressureDestination );

		// Carry the velocity field along itself so jets travel away from their source.
		_advection.Update( neighbourhood.Advection, sourceVelocityX, sourceVelocityY, sourceVelocityX, intermediateVelocityX );
		_advection.Update( neighbourhood.Advection, sourceVelocityX, sourceVelocityY, sourceVelocityY, intermediateVelocityY );

		ApplyPressureForce( neighbourhood.Pressure, pressureDestination, intermediateVelocityX, intermediateVelocityY );

		_projection.Update(
			neighbourhood.Projection,
			intermediateVelocityX,
			intermediateVelocityY,
			destinationVelocityX,
			destinationVelocityY,
			projectionPressure,
			projectionPressureScratch,
			projectionDivergence
		);

		_advection.Update( neighbourhood.Advection, destinationVelocityX, destinationVelocityY, concentrationSource, concentrationDestination );
	}

	// Subtracts dt * PressureForce * grad(p) using central differences. A wall is zero
	// gradient (the neighbour reads as self); vacuum is zero ambient pressure, which the
	// layer already holds for missing cells, so flagged directions read straight through.
	private void ApplyPressureForce(
		IGridLayer<Direction> neighbourhood,
		IGridLayer<float> pressure,
		IGridLayer<float> velocityX,
		IGridLayer<float> velocityY
	) {
		ArgumentOutOfRangeException.ThrowIfLessThan( pressure.Halo, 1 );

		pressure.ExchangeHalos();

		IGridChunkLayout layout = pressure.Space;
		int size = layout.ChunkSize;
		int stride = pressure.Stride;
		float scale = _forceScale;
		Vector256<float> scaleVector = Vector256.Create( scale );
		Vector256<int> eastBit = Vector256.Create( (int)Direction.East );
		Vector256<int> westBit = Vector256.Create( (int)Direction.West );
		Vector256<int> southBit = Vector256.Create( (int)Direction.South );
		Vector256<int> northBit = Vector256.Create( (int)Direction.North );

		ReadOnlySpan<Direction> flags = neighbourhood.Cells;
		ReadOnlySpan<float> p = pressure.Cells;
		Span<float> vx = velocityX.Cells;
		Span<float> vy = velocityY.Cells;
		ref byte flagsRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetReference( flags ) );
		ref float pRef = ref MemoryMarshal.GetReference( p );
		ref float vxRef = ref MemoryMarshal.GetReference( vx );
		ref float vyRef = ref MemoryMarshal.GetReference( vy );

		for( int slot = 0; slot < layout.SlotCount; slot++ ) {
			if( layout.GetState( slot ) == ChunkState.Empty ) {
				continue;
			}
			for( int row = 0; row < size; row++ ) {
				int flagStart = neighbourhood.IndexOf( slot, 0, row );
				int pStart = pressure.IndexOf( slot, 0, row );
				int xStart = velocityX.IndexOf( slot, 0, row );
				int yStart = velocityY.IndexOf( slot, 0, row );

				int column = 0;
				for( ; column + Block <= size; column += Block ) {
					(Vector256<int> include0, Vector256<int> include1) = LoadFlags( ref flagsRef, flagStart + column );
					int i = pStart + column;
					ApplyBlock( ref pRef, ref vxRef, ref vyRef, i, xStart + column, yStart + column, stride, include0, eastBit, westBit, southBit, northBit, scaleVector );
					ApplyBlock( ref pRef, ref vxRef, ref vyRef, i + 8, xStart + column + 8, yStart + column + 8, stride, include1, eastBit, westBit, southBit, northBit, scaleVector );
				}
				for( ; column < size; column++ ) {
					Direction cell = flags[flagStart + column];
					int i = pStart + column;
					float self = p[i];
					float east = ( cell & Direction.East ) != 0 ? p[i + 1] : self;
					float west = ( cell & Direction.West ) != 0 ? p[i - 1] : self;
					float south = ( cell & Direction.South ) != 0 ? p[i + stride] : self;
					float north = ( cell & Direction.North ) != 0 ? p[i - stride] : self;
					vx[xStart + column] -= ( east - west ) * scale;
					vy[yStart + column] -= ( south - north ) * scale;
				}
			}
		}
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static void ApplyBlock(
		ref float pRef,
		ref float vxRef,
		ref float vyRef,
		int i,
		int x,
		int y,
		int stride,
		Vector256<int> include,
		Vector256<int> eastBit,
		Vector256<int> westBit,
		Vector256<int> southBit,
		Vector256<int> northBit,
		Vector256<float> scale
	) {
		Vector256<float> self = Vector256.LoadUnsafe( ref pRef, (nuint)i );
		Vector256<float> east = Select( include, eastBit, Vector256.LoadUnsafe( ref pRef, (nuint)( i + 1 ) ), self );
		Vector256<float> west = Select( include, westBit, Vector256.LoadUnsafe( ref pRef, (nuint)( i - 1 ) ), self );
		Vector256<float> south = Select( include, southBit, Vector256.LoadUnsafe( ref pRef, (nuint)( i + stride ) ), self );
		Vector256<float> north = Select( include, northBit, Vector256.LoadUnsafe( ref pRef, (nuint)( i - stride ) ), self );
		( Vector256.LoadUnsafe( ref vxRef, (nuint)x ) - ( ( east - west ) * scale ) ).StoreUnsafe( ref vxRef, (nuint)x );
		( Vector256.LoadUnsafe( ref vyRef, (nuint)y ) - ( ( south - north ) * scale ) ).StoreUnsafe( ref vyRef, (nuint)y );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static Vector256<float> Select(
		Vector256<int> include,
		Vector256<int> bit,
		Vector256<float> neighbour,
		Vector256<float> self
	) {
		Vector256<float> mask = ( ~Vector256.Equals( include & bit, Vector256<int>.Zero ) ).AsSingle();
		return Vector256.ConditionalSelect( mask, neighbour, self );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static (Vector256<int>, Vector256<int>) LoadFlags(
		ref byte flags,
		int index
	) {
		Vector128<byte> include = Vector128.LoadUnsafe( ref flags, (nuint)index );
		(Vector128<ushort> low, Vector128<ushort> high) = Vector128.Widen( include );
		(Vector128<uint> a, Vector128<uint> b) = Vector128.Widen( low );
		(Vector128<uint> c, Vector128<uint> e) = Vector128.Widen( high );
		return (Vector256.Create( a, b ).AsInt32(), Vector256.Create( c, e ).AsInt32());
	}

}
