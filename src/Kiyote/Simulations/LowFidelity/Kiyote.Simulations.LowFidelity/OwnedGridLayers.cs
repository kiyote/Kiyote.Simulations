using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity;

internal sealed class OwnedGridLayers<TCell> : IDisposable {

	private readonly ICompiledGridAssembly<TCell> _compiled;
	private readonly List<IGridLayer> _layers = [];
	private bool _disposed;

	public OwnedGridLayers(
		ICompiledGridAssembly<TCell> compiled,
		int chunkSize
	) {
		ArgumentNullException.ThrowIfNull( compiled );
		_compiled = compiled;
		EnsureUsable();
		if( compiled.ChunkLayout.ChunkSize != chunkSize ) {
			throw new ArgumentException( $"The simulation requires {chunkSize}-cell chunks.", nameof( compiled ) );
		}
	}

	public void EnsureUsable() {
		ObjectDisposedException.ThrowIf( _disposed, this );
		if( _compiled.IsStale ) {
			throw new InvalidOperationException( "The compilation is stale. Commit before structural edits, then dispose and recreate the simulation with a new compilation." );
		}
	}

	public IGridLayer<T> Add<T>(
		IGridLayer<T> layer
	) where T : unmanaged {
		_layers.Add( layer );
		return layer;
	}

	public void Remove(
		IGridLayer layer
	) {
		if( !_layers.Contains( layer ) ) {
			throw new InvalidOperationException( "The layer is not owned by this simulation." );
		}
		if( !_compiled.RemoveLayer( layer ) ) {
			throw new InvalidOperationException( "A simulation-owned layer was removed externally." );
		}
		_ = _layers.Remove( layer );
	}

	public void Dispose() {
		if( _disposed ) {
			return;
		}
		if( !_compiled.IsStale ) {
			for( int i = _layers.Count - 1; i >= 0; i-- ) {
				if( !_compiled.RemoveLayer( _layers[i] ) ) {
					throw new InvalidOperationException( "A simulation-owned layer was removed externally." );
				}
				_layers.RemoveAt( i );
			}
		}
		_layers.Clear();
		_disposed = true;
	}

}
