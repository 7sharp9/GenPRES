# ADR-0014: Staged Value Expansion for Timed Orders

**Date**: 2026-04-07

**Status**: Proposed

**References**:

- [Core Domain Model](../../domain/core-domain.md)
- [GenSOLVER: Order Scenarios to Quantitative Solutions](../../domain/gensolver-from-orders-to-quantitative-solutions.md)
- [GenORDER: Operational Rules to Orders](../../domain/genorder-operational-rules-to-orders.md)

## Summary

Introduce a two-phase staged value expansion in the order processing pipeline for OnceTimed and Timed orders. In the first phase, only dose quantity variables are expanded from min/incr/max ranges to discrete value sets, while rate variables are kept as ranges. In the second phase, after the dose quantity is pinned (by the user or by setting the median), rate variables are expanded. This prevents combinatorial explosion when solving the equation `dos_qty = dos_rte * sch_tme`.

## Context

### The Problem

When processing an OnceTimed order (e.g., IV kaliumchloride with schedule time 60-120 min for a 31 kg patient), the existing `minIncrMaxToValues` function expands all variables (component quantities, dose quantity, dose rate) from min/incr/max ranges to discrete value sets in a single pass.

For OnceTimed and Timed orders, the equation `dos_qty = dos_rte * sch_tme` creates a three-way relationship between dose quantity, dose rate, and schedule time. When the solver expands rate values, each rate value interacts with each possible time value to produce dose quantity combinations. Across multiple substances (e.g., kaliumchloride concentrate with kalium, chloor, kaliumchloride substances plus glucose 5% diluent with glucose, energie, koolhydraat substances), the cross-product explodes.

**Observed failure**: 746,839 values generated, exceeding the max calc count threshold (500 x 500 = 250,000). The order could not be calculated.

### Affected Order Types

The relationship `dos_qty = dos_rte * sch_tme` exists in:

- **OnceTimed**: Single administration over a time period (e.g., IV infusion given once over 60-120 min)
- **Timed**: Repeated administrations each over a time period (e.g., IV infusion given 4x/day, each over 15-20 min)

The following order types are NOT affected:

- **Once**: No rate or time variables
- **Discontinuous**: Has frequency but no time/rate interaction
- **Continuous**: Rate IS the primary dose variable; dose quantity is not independently expanded

### Why the Explosion Occurs

Given the kaliumchloride scenario after CalcMinMax + increaseIncrements:

| Variable | Range | Approx. Count |
|---|---|---|
| `sch_tme` | 60 min..120 min | Many (continuous range with fine increments) |
| `dos_qty` (orderable) | 160 mL..0.5 mL..236 mL | ~153 values |
| `dos_rte` (orderable) | 80 mL/uur..20 mL/uur..220 mL/uur | ~8 values |
| Component qty | 14 mL..0.5 mL..17 mL | ~7 values |

When `minIncrMaxToValues` expands component qty (7 values) then solves, rate variables acquire derived values from the time range. When rate is subsequently expanded, each rate x time combination propagates through all substance variables. The total product across all equation variables exceeds the 250,000 limit.

## Decision

### Two-Phase Staged Expansion

Modify the order processing pipeline to expand variables in two phases for OnceTimed and Timed orders:

**Phase 1 — Expand dose quantities only (skip rate)**

The `minIncrMaxToValues` function receives a `skipRate` flag. When `skipRate = true`, the orderable dose rate variable is excluded from the expansion loop. Only component orderable quantities and the orderable dose quantity are expanded to discrete values. The rate remains as a min/incr/max range.

This is safe because the constraint solver can still propagate min/max bounds through rate equations — it just does not enumerate all discrete rate values yet.

**Phase 2 — Expand rate after quantity is constrained**

After Phase 1, one of two things happens:

1. **CalcMinMax path**: `setMedianDoseValue` pins the dose quantity to a single median value. Then `solveNormDose` propagates this through the equations, naturally constraining the rate range.
2. **CalcValues path**: The system runs Phase 1 (quantity expansion), then immediately runs Phase 2 (rate expansion with `skipRate = false`). Because dose quantities are already discrete values from Phase 1, the rate x time cross-product produces far fewer values.

In both cases, the dose quantity is constrained before rate expansion occurs, preventing the combinatorial explosion.

### Condition for Staged Expansion

Staged expansion (`skipRate = true`) applies when:

```text
Schedule.hasTime = true AND Schedule.isContinuous = false
```

This matches exactly OnceTimed and Timed orders. For all other order types, `skipRate = false` (no change in behavior).

### Changes to Pipeline Steps

#### CalcMinMax Pipeline

The `ensure-dose-values-1` step passes `skipRate = true` for timed orders. The subsequent `set-normdose` step (which calls `solveNormDose` with min/max solving, not all-values solving) works correctly with rate still as min/incr/max.

#### CalcValues Pipeline

Split into two sequential steps for timed orders:

1. `calc-qty-values`: Expands quantities only (`skipRate = true`)
2. `calc-rate-values`: Expands rate too (`skipRate = false`)

For non-timed orders, a single step with `skipRate = false` preserves existing behavior.

#### SolveOrder Pipeline

No change needed. By the time SolveOrder runs, the user has already selected a dose quantity value, so rate expansion is naturally constrained.

#### ReCalcValues Pipeline

Same treatment as CalcValues — two-phase expansion for timed orders.

## Consequences

### Positive

- **Eliminates value overflow**: The kaliumchloride OnceTimed scenario now completes successfully instead of failing with 746,839 values
- **No impact on other order types**: Once, Discontinuous, and Continuous orders are unaffected
- **Preserves constraint guarantees**: All GenSOLVER properties (soundness, completeness, monotonic convergence) are maintained — the change only affects the order of variable expansion, not the constraint logic
- **Minimal code change**: A single boolean parameter (`skipRate`) in `minIncrMaxToValues` plus pipeline step adjustments

### Negative

- **Pre-existing `set-normdose` error remains**: For the kaliumchloride scenario, the `setMedianDoseValue` step encounters a conflict where the computed median (14 mL) falls outside the component's value set (14.5-17 mL). This is a pre-existing issue unrelated to the staged expansion and should be addressed separately
- **Slightly more complex pipeline**: The CalcValues and ReCalcValues commands now have conditional two-step logic for timed orders

### Risks

- **CanSetNormDose guard interaction**: With `skipRate = true`, the Rate variable will not have discrete values, so `CanSetNormDose` returns `false`. This causes the `ensure-dose-values-1` guard (`_.CanSetNormDose >> not`) to trigger correctly. If this guard logic changes in the future, the staged expansion behavior must be re-validated
- **Continuous orders must NOT use skipRate**: The condition `Schedule.isContinuous |> not` is critical because for Continuous orders, rate IS the primary dose variable. If this condition were accidentally removed, Continuous orders would fail

## Verification

### Prototype Script

The approach was validated in `src/Informedica.GenORDER.Lib/Scripts/StagedExpansion.fsx` with the following test cases:

| Test | Scenario | Result |
|---|---|---|
| 1 | Kaliumchloride OnceTimed CalcMinMax | No overflow. Pre-existing `set-normdose` error (unrelated). |
| 2 | Paracetamol OnceTimed CalcMinMax | Succeeded (regression check). |
| 3 | Paracetamol Once CalcMinMax | Succeeded (non-timed order unaffected). |
| 4 | Kaliumchloride CalcMinMax + CalcValues | CalcValues succeeded after CalcMinMax. Rate resolved to reasonable values. |

All existing server tests (5402 tests) continue to pass.

### Key Observations from Test Results

After staged expansion, the kaliumchloride order shows:

- Orderable dose qty: 31.5 mL (median, single value)
- Orderable dose rate: 20 mL/uur..5 mL/uur..30 mL/uur (3 values, manageable)
- Component dose qty: 14.5; 15; 15.5; 16; 16.5; 17 mL (6 values)
- Substance dose qty adj: 0.47; 0.48; 0.5; 0.52; 0.53; 0.55 mmol/kg (within 0.45-0.55 mmol/kg constraint)

## Implementation

### Files Modified

| File | Change |
|---|---|
| `Order.fs` | Add `skipRate: bool` parameter to `minIncrMaxToValues`. When true, omit dose rate from variable expansion list. |
| `OrderProcessor.fs` | Thread `skipRate` through `calcValuesStep` and `reCalcValuesStep`. Split CalcValues and ReCalcValues into two-phase steps for timed orders. |

### Function Signature Change

```fsharp
// Before
let minIncrMaxToValues useMaxNumberOfValues minTime logger ord =

// After
let minIncrMaxToValues useMaxNumberOfValues minTime (skipRate: bool) logger ord =
```

### Variable List Change

```fsharp
// Before
let ovars =
    [
        yield! ord.Orderable.Components |> List.map (_.OrderableQuantity >> Quantity.toOrdVar)
        ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
        ord.Orderable.Dose.Rate |> Rate.toOrdVar
    ]

// After
let ovars =
    [
        yield! ord.Orderable.Components |> List.map (_.OrderableQuantity >> Quantity.toOrdVar)
        ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
        if not skipRate then
            ord.Orderable.Dose.Rate |> Rate.toOrdVar
    ]
```
