# Low-Fidelity Composite Multi-Gas Atmosphere Simulation
## Architectural Reference Document

This document serves as an engineering and architectural reference for the topological, low-fidelity cellular automata atmosphere simulation designed for multi-grid game environments.

---

## 1. Core Physics & Mathematical Concepts

True Computational Fluid Dynamics (CFD) packages solve the continuous **Navier-Stokes equations**, tracking complex fluid variables like vorticity, turbulence, viscosity tensors, and boundary layer friction. This is far too computationally expensive for a real-time game loop running alongside AI, physics, and rendering pipelines.

Instead, this system uses a simplified, discrete approximation based on two fundamental principles:

1. **The Conservation of Mass:** Gas within a sealed environment cannot be created or destroyed. It can only transfer between adjacent nodes.
2. **The Diffusion Equation (Laplacian Smoothing):** Fluids naturally move from areas of high concentration (high pressure) to areas of low concentration (low pressure). 

### The Pressure Gradient Equation
For standard room equalization, the mass transfer ($\Delta M$) across a connecting node during a simulation tick is modeled using a linear discrete Laplacian filter:

$$\Delta M = (P_{\text{source}} - P_{\text{target}}) \times R_{\text{diffusion}}$$

Where $P$ is the total pressure of a cell (the sum of all constituent gas masses) and $R_{\text{diffusion}}$ is a dampening coefficient that dictates how fast the atmosphere spreads.

### Choked Flow & Vacuum Breaches
When gas flows from a highly pressurized room into an empty void (such as space or an unpressurized chamber), standard linear diffusion breaks down. In real-world aerodynamics, gas accelerating into a vacuum encounters a physical limit known as **Choked Flow**, capping its velocity at the local speed of sound ($Mach 1$).

To model this without expensive fluid expansion calculations, the system checks for a severe pressure drop:

$$\text{If } P_{\text{target}} == 0 \quad \text{or} \quad \frac{P_{\text{source}} - P_{\text{target}}}{P_{\text{source}}} > 0.5$$

When this criterion is met, the system bypasses standard diffusion and executes an aggressive **asymmetric decompression rate**:

$$\Delta M = P_{\text{source}} \times R_{\text{decompression}}$$

This rapidly depletes the room mass and generates massive flow scalars, providing the visual system with dramatic, high-velocity data points.

---

## 2. Structural Memory Layout & Performance Goals

To avoid causing micro-stutters or garbage collection (GC) stalls in standard game engines, the data layout strictly follows **Data-Oriented Design (DOD)** principles.

### Flat 1D Cache-Locality
Standard multi-dimensional arrays (`GasCell[,]` or nested objects) create a fragmented web of memory pointers. The CPU must jump around memory to retrieve data, causing severe cache misses.

This manager packs all data into contiguous, sequential arrays (`GasCell[]`). Global 2D grid locations map directly to a single 1D index utilizing absolute bounding box dimensions:

$$\text{Index} = (\text{GlobalColumn} - \text{Grid.Column}) + (\text{GlobalRow} - \text{Grid.Row}) \times \text{Grid.Width}$$

### Allocation-Free Jagged Representation
Because cells can have dynamic connectivity states (e.g., doors opening or closing), a naive system might use a dynamic `List<CellConnection>` per cell. This generates garbage collection pressure every time connections change.

To remain entirely allocation-free, the simulation employs a pooled, jagged-flat array model:
* `_connectionsPool[]`: A single, giant, contiguous array containing all connection structs for every cell in the entire bounding space.
* `_connectionOffsets[]`: An integer array mapping a cell index to its starting memory slot inside the global pool.
* `_connectionCounts[]`: Tracks exactly how many active connection links a cell has.

When a structural asset changes (like a blast door shutting), the framework overwrites these indices inside the flat pool without creating or destroying object instances.

---

## 3. Double-Buffering & Simulation Lifecycle

### Eliminating Directional Bias
In a Cellular Automata system, a cell cannot read and write to the exact same array simultaneously during a single step. If a cell at index 0 shifts gas to index 1, and the loop immediately processes index 1, that gas will cascade across the entire grid in a single frame. This introduces severe update bias based entirely on loop directions.

To eliminate this, the system implements a **Double-Buffering** pattern:
1. The simulation reads the global atmosphere state exclusively from `_currentGasGrid`.
2. A separate array, `_nextGasGrid`, is initialized as a clone of the current state.
3. As gas mass transfers are calculated, deductions and additions are written *only* to the `_nextGasGrid` buffer.
4. At the end of the simulation tick, the reference pointers to the arrays are swapped. The previous frame's back-buffer seamlessly becomes the next frame's read source.

### The Execution Pipeline
Every simulation tick processes the architecture in the following sequential order:

```
[ Step 1: Copy Buffers ] ──> Clones _currentGasGrid state onto _nextGasGrid.
[ Step 2: Outflow Pass ] ──> Iterates flat cell indexes; calculates gradient deltas.
[ Step 3: Gas Sifting  ] ──> Extracts gases proportionally; applies vacuum absorption.
[ Step 4: Flow Logging ] ──> Records scalar mass displacement onto _flowPool links.
[ Step 5: Pointer Swap ] ──> Swaps _currentGasGrid and _nextGasGrid arrays natively.
```

---

## 4. Mathematical Stability (The Convergence Limit)

Because this simulation uses explicit forward Euler integration rather than a continuous derivative solver, it is susceptible to **numerical explosion** if configured incorrectly.

If a cell vents gas into its neighbors too aggressively, it can over-deplete its own mass pool, entering negative numbers. On the following frame, it tries to pull gas back to compensate, resulting in massive, oscillating waves that crash the simulation.

To guarantee perfect mathematical convergence to a flat, equalized equilibrium pressure across all sealed rooms, the configuration parameters must strictly adhere to the mathematical stability criteria for discrete diffusion systems:

$$R_{\text{diffusion}} \times \text{Max Neighbors} < 1.0$$

Since the grid topography natively supports an 8-way cardinal and diagonal configuration, a cell can possess up to **8 neighbors**. The absolute maximum stability value is:

$$R_{\text{diffusion}} < \frac{1}{8} \quad (0.125)$$

Setting the value between `0.05` and `0.10` ensures stable, smooth convergence. Gas fills a closed corridor topology perfectly, flattening pressure drops until the system safely drops into a stagnant state.

---

## 5. Topological Flow and Spatial Vector Mapping

The simulation engine is **completely geometry-blind**. It operates entirely within a topological domain, processing connection vectors without knowing where a sub-grid resides in physical space or how it is rotated. 

A connection to a room next door on the same ship hull looks exactly like a connection across an active docking clamp to an entirely separate outpost.

### Resolving World-Space Airflow Vectors
When the visual game loop wants to draw high-velocity wind streaks or debris forces, it queries `ComputeGlobalVelocity()`. The system reconstructs spatial information on-demand via a two-step translation layer:

#### 1. Accumulate Local Grid Forces
The cell searches its connection offsets and reads the scalar values recorded inside `_flowPool`. It matches the connection's `DirectionId` (0-7) against a static lookup table of pre-calculated normalized 2D direction vectors:

$$\vec{V}_{\text{local}} = \sum_{i=1}^{\text{count}} \vec{D}_{\text{unit}}[\text{DirectionId}_i] \times \text{NetFlowMass}_i$$

#### 2. Affine Rotation Transformation
Because individual sub-grids can be rotated (e.g., a spinning ship or a tilted platform), this resultant local vector is multiplied against an inline 2D rotation matrix using the sub-grid's global heading ($\theta$):

$$\vec{V}_{\text{global}}.X = \vec{V}_{\text{local}}.X \cos(\theta) - \vec{V}_{\text{local}}.Y \sin(\theta)$$
$$\vec{V}_{\text{global}}.Y = \vec{V}_{\text{local}}.X \sin(\theta) + \vec{V}_{\text{local}}.Y \cos(\theta)$$

The final vector accurately reflects the atmospheric movement relative to consistent, top-level world coordinates, bypassing any physics processing overhead when a sub-grid moves through space without an internal pressure change.
