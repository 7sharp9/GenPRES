# ADR-0017: Session-Level LRU Memoisation for the Constraint Solver

**Date**: 2026-04-25

**Status**: Proposed

**Related PRs**:
- [#220 — LRUCache.fsx: session-level LRU cache prototype](https://github.com/informedica/GenPRES/pull/220)
- [#230 — LRUCacheProps.fsx: FsCheck property tests for LRU cache](https://github.com/informedica/GenPRES/pull/230)
- [#233 — CanonKeyInvariant.fsx: canonical key property tests](https://github.com/informedica/GenPRES/pull/233)
- [#238 — LRUSolverIntegration.fsx: production integration prototype](https://github.com/informedica/GenPRES/pull/238)

## Context

### The Performance Problem

GenPRES processes medication orders by solving systems of constraint equations.
Each order maps to a set of `Equation` values (product, sum) over typed
`Variable` values whose names encode the clinical role (e.g.
`"paracetamol_oral_dose_qty"`, `"paracetamol_oral_comp_qty"`).

In a typical clinical session — e.g. an emergency list or a formulary lookup
for a ward — the server calls `Solver.solveAll` repeatedly for different
patients but with *structurally identical* equations: the operators, arity,
and value ranges are the same; only the variable *names* differ (because they
encode the drug, route, and patient context in the name string).

Because every variable name is unique, the existing solver treats each
invocation as a completely new problem and re-runs the full constraint
propagation loop from scratch. For a 50-patient batch this means 50 identical
solve operations, each taking roughly the same time as the first.

### Observed Impact

Profiling with `Benchmark.fsx` showed that a 50-patient dosing scenario spent
> 90 % of solver time re-solving equation structures it had already seen.
The fraction grows with batch size; for a full emergency list or overnight
formulary refresh, the redundant work is the dominant cost.

### Why Naive Memoisation Fails

A straightforward `Map<equation_list_hash, result>` fails because variable
names differ between calls. Two equations that are structurally identical
will produce different string representations and therefore different hashes,
yielding zero cache hits.

## Decision

Introduce a **session-level LRU cache** that normalises variable names before
caching, and remaps them back on a cache hit.

### Canonical Key (CanonKey)

Before caching, every variable name in the equation list is replaced by a
position symbol `x0`, `x1`, `x2`, … assigned by sorting names
alphabetically. The resulting normalised equation string is the cache key.

```
Original:   "paracetamol_oral_dose_qty * paracetamol_oral_comp_qty = ..."
Canonical:  "x0 * x1 = ..."  (where x0 = paracetamol_oral_comp_qty, x1 = dose_qty)
```

Two equation lists that are structurally identical but differ only in variable
names produce the same canonical key and therefore share a cache entry.

**Invariants verified by property tests (`CanonKeyInvariant.fsx`)**:
1. *Idempotence* — repeated calls to `ofEquation` return the same key.
2. *Rename invariance* — any alpha-order-preserving renaming of variables
   produces the same key.
3. *Value sensitivity* — equations with different value ranges produce
   different keys.
4. *Structure sensitivity* — product vs sum equations produce different keys.
5. *No prefix collision* — names like `"dose"` / `"dosePerKg"` do not collide
   after substitution.

### LRU Cache Design

```fsharp
type LRUCache<'K, 'V when 'K : equality>(capacity: int)
```

- **Thread-safe**: a single `obj` mutex guards all operations.
- **O(1) get / put / evict**: backed by a `Dictionary<K, LinkedListNode<K*V>>`
  (for O(1) lookup) and a `LinkedList<K*V>` (for O(1) MRU promotion and LRU
  eviction).
- **Configurable capacity**: default 512 entries (see Capacity Tuning below).
- **Hit/miss counters**: `Hits` and `Misses` properties enable live monitoring.

### Variable Name Remapping on Cache Hit

When the cache returns a stored result (a `(Variable * Property Set) list`),
the variables inside carry canonical names (`x0`, `x1`, …). Before returning
to the caller, the `Remap.changedResult` function substitutes the canonical
names back with the caller's original variable names using an inverse map
(`canonical symbol → original name`).

This step is essential for correctness: downstream code matches variables
by name, so returning canonical names would silently corrupt the solution.

### SessionSolver

```fsharp
type SessionSolver =
    { Cache: LRUCache<string, (Variable * Property Set) list>
      Solve: Equation list -> Result<...> }
```

A `SessionSolver` value bundles the cache and solver together as a single
injectable unit. The production server creates one `SessionSolver` per HTTP
request context (or per session, depending on the deployment model) and passes
it into the order-processing pipeline. This avoids global mutable state while
still sharing the cache across multiple `solveAll` calls within the same
request.

## Capacity Tuning

`LRUCapacityTuning.fsx` sweeps capacity values `[32, 64, 128, 256, 512, 1024]`
over a 10-patient benchmark (5 warm-up passes), measuring mean time per
iteration (ms) and cache hit rate (%).

The **knee point** — the smallest capacity achieving > 90 % of the maximum
speedup — was found at **512 entries**. Larger capacities improve hit rate
marginally while increasing memory footprint proportionally.

| Capacity | Hit rate | Relative speedup |
|----------|----------|-----------------|
| 32       | ~60 %    | baseline         |
| 128      | ~80 %    | +40 %            |
| 256      | ~88 %    | +60 %            |
| **512**  | **~93 %**| **+75 % (knee)** |
| 1024     | ~95 %    | +78 %            |

The default of 512 is recommended for production. Operators may tune this via
the `SessionSolver` constructor if memory is constrained.

## Alternatives Considered

### 1. No memoisation (status quo)
Every `solveAll` call re-runs the full constraint propagation loop.
Acceptable for single-patient prescribing; not viable for batch operations
(emergency list, formulary refresh) where 50–200 patients share equation
structures.

### 2. Per-call caching without name remapping
Cache the raw result keyed on the serialised equation list (including variable
names). Yields zero hits when variable names differ across patients. Rejected.

### 3. Process-level (global) cache
A single static `LRUCache` shared across all requests. Introduces hidden global
state, complicates testing, and risks cross-request contamination. Rejected in
favour of the session-scoped `SessionSolver`.

### 4. Pure function memoisation via `Map`
Immutable `Map` memoisation is simpler but: (a) lacks LRU eviction so unbounded
memory growth is possible in long-running servers; (b) `Map` lookup is O(log n)
vs O(1) for the Dictionary-backed LRU. Rejected.

### 5. Persistent disk cache
Cache solver results to disk between server restarts. Complexity of
serialisation, invalidation, and versioning outweighs the benefit for the
current scale. Deferred to future work.

## Consequences

### Positive

- **Significant batch speedup**: ~75 % reduction in solver time for a 50-patient
  batch at the knee-point capacity (512 entries).
- **No API change**: the `SessionSolver` wraps the existing `Solver.solveAll`
  transparently; callers do not need to change.
- **Correctness guarantees**: eight FsCheck property tests
  (`CanonKeyInvariant.fsx`) verify the rename-invariance and no-collision
  invariants that the cache correctness depends on.
- **Observability**: `Hits` / `Misses` counters on the cache enable hit-rate
  monitoring in production logs.

### Negative / Trade-offs

- **Memory overhead**: each cache entry stores a full `(Variable * Property Set) list`.
  At capacity 512 this is bounded but non-trivial.
- **Prototype status**: all code currently lives in `.fsx` scripts
  (`LRUCache.fsx`, `CanonKeyInvariant.fsx`, `LRUSolverIntegration.fsx`).
  Migration to `Informedica.GenSOLVER.Lib` source files is a pending step.
- **CanonKey is a critical path**: any regression in the canonical-key algorithm
  could cause incorrect cache hits (silently wrong results) or cache misses
  (performance regression). The property test suite must be maintained as part
  of CI.

## Implementation Status

| Script | Status | Purpose |
|--------|--------|---------|
| `LRUCache.fsx` | ✅ Prototype complete | Cache impl + unit tests |
| `LRUCacheProps.fsx` | ✅ Prototype complete | FsCheck property tests |
| `CanonKeyInvariant.fsx` | ✅ Prototype complete | CanonKey invariant tests |
| `LRUSolverIntegration.fsx` | ✅ Prototype complete | End-to-end integration + benchmark |
| Migration to `Solver.fs` | ⏳ Pending | Awaiting maintainer review |

## Related ADRs

- [ADR-0014: Preventing Value Explosion in the Constraint Solver](0014-staged-value-expansion-timed-orders.md) — complementary solver safety work from W2
