# Development

## Visualizers

Run either visualizer from the repository root:

```powershell
dotnet run --project test\Kiyote\Simulations\Kiyote.Simulations.Visualizer -c Release
dotnet run --project test\Kiyote\Simulations\LowFidelity\Kiyote.Simulations.LowFidelity.Visualizer -c Release
```

The core visualizer writes 16 GIF animations to
`%TEMP%\Kiyote.Simulations.Visualizer`. The LowFidelity visualizer writes six to
`%TEMP%\Kiyote.Simulations.LowFidelity.Visualizer`. Each animation contains
100 frames.

The three standalone thermal scenarios use a separate thermal service provider
with a 10-second `ThermalVisualizerClock`. Each frame still advances one simulated
hour, now in 360 steps rather than 36,000. The visualizer checks the returned step
count to detect dropped simulation time. Atmosphere and coupled scenarios retain
the default 0.1-second clock; the production defaults are unchanged.

All six LowFidelity animations completed in approximately 5.4 seconds in a local
Release run. The thermal scenarios' solver comparison took 1.87 seconds versus
137.76 seconds at the old timestep. Output is not byte-identical: the old tiny
single-precision steps accumulated substantial drift over 100 simulated hours.
The 10-second result stayed within 0.3 K and one grayscale level of a separate
double-precision reference. See the
[thermal timestep experiment](EXPERIMENT.md#standalone-thermal-visualizer-timestep)
for the full comparison and limitations.

Both entry points register `IFileSystem` and `IAnimationWriter` with the public
`FileSystem` and `GifAnimationWriter` implementations directly. Do not replace
these registrations with `AddGifImaging()` from Kiyote.Imaging 0.0.12: that helper
calls the removed `Kiyote.Buffers.AddBuffers()` method and fails at startup with
the current Buffers dependency. The GIF writer itself supports the current buffer
API; no dependency downgrade or local package is needed.

## Clean up filemodes

```bash
find . -type f -not -path '*/.git' -exec chmod 644 {} +
find . -type d -not -path '*/.git*' -exec chmod 755 {} +
```
