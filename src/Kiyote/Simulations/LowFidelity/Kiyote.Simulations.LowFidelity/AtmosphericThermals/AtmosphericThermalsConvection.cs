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
		IAtmosphere atmosphere,
		IThermal thermal
	) {
		IGridLayer<float> gasTemperature = atmosphere.Temperature;
		IGridLayer<float> structureTemperature = thermal.Temperature;
		foreach( ConvectionCell cell in cells ) {
			float total = atmosphere.GetTotalGas( cell.Column, cell.Row );
			if( total <= 0.0f ) {
				continue;
			}
			float gasCapacity = total * gasHeatCapacity;
			float gas = gasTemperature[cell.Column, cell.Row];
			ReadOnlySpan<ConvectionLink> faces = links.Slice( cell.FirstLink, cell.LinkCount );
			foreach( ConvectionLink face in faces ) {
				float difference = structureTemperature[face.Column, face.Row] - gas;
				float equilibrium = difference * gasCapacity * face.Capacity / ( gasCapacity + face.Capacity ) / cell.LinkCount;
				float joules = face.Conductance * difference * dt;
				if( MathF.Abs( joules ) > MathF.Abs( equilibrium ) ) {
					joules = equilibrium;
				}
				float applied = atmosphere.AddEnergy( cell.Column, cell.Row, joules );
				thermal.AddEnergy( face.Column, face.Row, -applied );
			}
		}
	}

}
