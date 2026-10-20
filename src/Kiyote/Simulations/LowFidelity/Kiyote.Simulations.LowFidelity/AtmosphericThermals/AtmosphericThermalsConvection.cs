using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

internal sealed class AtmosphericThermalsConvection : IAtmosphericThermalsConvection {

	// Moves conductance * dT * dt joules across each face, capped so that, with every face of the
	// cell taking an equal share, the gas never passes the temperature it would share with the face.
	void IAtmosphericThermalsConvection.Exchange(
		ReadOnlySpan<ConvectionCell> cells,
		ReadOnlySpan<ConvectionLink> links,
		float dt,
		float gasHeatCapacity,
		IAtmosphereConvectionState atmosphere,
		IThermalConvectionState thermal
	) {
		IGridLayer<float> gasTemperature = atmosphere.Temperature;
		IGridLayer<float> structureTemperature = thermal.Temperature;
		int gasChunk = -1;
		int structureChunk = -1;
		ReadOnlySpan<float> gasValues = [];
		ReadOnlySpan<float> structureValues = [];
		foreach( ConvectionCell cell in cells ) {
			float total = atmosphere.GetTotalGasAt( cell.Chunk, cell.Local );
			if( total <= 0.0f ) {
				continue;
			}
			float gasCapacity = total * gasHeatCapacity;
			if( gasChunk != cell.Chunk ) {
				gasChunk = cell.Chunk;
				gasValues = gasTemperature.GetValues( gasChunk );
			}
			float gas = gasValues[cell.Local];
			ReadOnlySpan<ConvectionLink> faces = links.Slice( cell.FirstLink, cell.LinkCount );
			foreach( ConvectionLink face in faces ) {
				if( structureChunk != face.Chunk ) {
					structureChunk = face.Chunk;
					structureValues = structureTemperature.GetValues( structureChunk );
				}
				float difference = structureValues[face.Local] - gas;
				float equilibrium = difference * gasCapacity * face.Capacity / ( gasCapacity + face.Capacity ) / cell.LinkCount;
				float joules = face.Conductance * difference * dt;
				if( MathF.Abs( joules ) > MathF.Abs( equilibrium ) ) {
					joules = equilibrium;
				}
				float applied = atmosphere.AddEnergyAt( cell.Chunk, cell.Local, gasCapacity, joules );
				thermal.AddEnergyAt( face.Chunk, face.Local, face.Capacity, -applied );
			}
		}
	}

}
