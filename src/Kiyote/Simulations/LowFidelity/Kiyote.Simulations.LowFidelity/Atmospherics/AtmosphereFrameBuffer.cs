using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereFrameBuffer {

	public AtmosphereFrameBuffer(
		ChunkedGridLayout layout,
		int[] sourceChunks,
		IGridLayer<float>?[] condensate,
		bool structuralTemperature
	) {
		Layout = layout;
		SourceChunks = sourceChunks;
		Pressure = new ChunkedGridLayer<float>( layout );
		Temperature = new ChunkedGridLayer<float>( layout );
		WindX = new ChunkedGridLayer<float>( layout );
		WindY = new ChunkedGridLayer<float>( layout );
		Gas = new ChunkedGridLayer<float>[condensate.Length];
		Condensate = new ChunkedGridLayer<float>?[condensate.Length];
		for( int g = 0; g < condensate.Length; g++ ) {
			Gas[g] = new ChunkedGridLayer<float>( layout );
			if( condensate[g] is not null ) {
				Condensate[g] = new ChunkedGridLayer<float>( layout );
			}
		}
		if( structuralTemperature ) {
			StructuralTemperature = new ChunkedGridLayer<float>( layout );
		}
	}

	public ChunkedGridLayout Layout { get; }
	public int[] SourceChunks { get; }
	public ChunkedGridLayer<float> Pressure { get; }
	public ChunkedGridLayer<float> Temperature { get; }
	public ChunkedGridLayer<float> WindX { get; }
	public ChunkedGridLayer<float> WindY { get; }
	public ChunkedGridLayer<float>[] Gas { get; }
	public ChunkedGridLayer<float>?[] Condensate { get; }
	public ChunkedGridLayer<float>? StructuralTemperature { get; }
	public long StepCount { get; set; }
	public float Vented { get; set; }

}
