# ADR-0010: Analysis — User Actions That Trigger SolveOrder

**Date**: 2026-01-01
**Status**: Accepted

## Context

The `SolveOrder` command in the `OrderProcessor` pipeline is the critical path for all user-driven dose edits. A complete map of which UI actions trigger `SolveOrder` is needed to understand the scope of any change to the solving pipeline and to guide integration testing.

## Decision

Document all 15 UI dropdown selections that route through `UpdateOrderScenario` → `SolveOrder`, and distinguish them from navigation button actions (which use `ChangeProperty`) and scenario selection (which uses `CalcValues`).

## Consequences

- Developers can predict which UI actions will exercise `SolveOrder` and write targeted tests.
- Changes to `processPipeline` or `SolveOrder` must be tested against all 15 dropdown interactions.
- The treatment plan server path (`UpdateTreatmentPlan` → `SolveOrder`) is identified as a separate entry point requiring its own test coverage.

---

# Analysis: User Actions That Trigger `SolveOrder`

## Overview

`SolveOrder` is a case of the `OrderCommand` discriminated union defined in `src/Informedica.GenORDER.Lib/Types.fs`. It is one of five order commands:

```fsharp
type OrderCommand =
    | CalcMinMax of Order
    | IncreaseIncrements of Order
    | CalcValues of Order
    | ReCalcValues of Order
    | SolveOrder of Order
    | ChangeProperty of Order * ChangePropertyCommand
```

`SolveOrder` is exclusively produced by the `UpdateOrderScenario` API command. The other commands map to different order commands:

| API Command | Order Command | Purpose |
| --- | --- | --- |
| `SelectOrderScenario` | `CalcValues` | Initial scenario selection |
| **`UpdateOrderScenario`** | **`SolveOrder`** | **User edits a value in the order** |
| `ResetOrderScenario` | `ReCalcValues` | Reset to recalculated values |

## Pipeline Steps

When `SolveOrder` executes in `OrderProcessor.processPipeline` (`src/Informedica.GenORDER.Lib/OrderProcessor.fs`, line 706), it runs three guarded steps:

| Step | Guard | Purpose |
| --- | --- | --- |
| `process-cleared` | `DoseIsSolved && IsCleared` | Re-processes a cleared order (frequency/dose/rate cleared) |
| `ensure-values-1` | `HasValues` is false | Calculates initial values via `minIncrMaxToValues` |
| `final-solve` | `OrderIsSolved` is false | Runs the constraint solver to produce a fully solved order |

## Entry Points from the UI

There are **two independent paths** from the UI that ultimately trigger `SolveOrder`.

### Path 1: Order Editing Modal (Prescribe & TreatmentPlan views)

The Order modal (`src/Informedica.GenPRES.Client/Views/Order.fs`) presents dropdown/select controls for various order variables. When the user **selects a value from any dropdown**, the corresponding `Change*` Elmish message is dispatched, which:

1. Modifies the `Order` record with the selected value
2. Wraps it in `UpdateOrderScenario ord` (internal Elmish message)
3. Calls the parent's `updateOrderScenario` callback

#### Wiring in Prescribe.fs

```fsharp
// src/Informedica.GenPRES.Client/Views/Prescribe.fs, line 635
updateOrderScenario = Api.UpdateOrderScenario >> props.updateOrderContext
```

#### Wiring in TreatmentPlan.fs

```fsharp
// src/Informedica.GenPRES.Client/Views/TreatmentPlan.fs, line 331
updateOrderScenario = updateOrderScenario  // local function that calls props.updateTreatmentPlan
```

#### Full call chain (Prescribe path)

```text
User selects a value in a dropdown
  → Order.fs dispatches Change* message (e.g. ChangeFrequency)
    → Order.fs update sets the value on the Order, dispatches UpdateOrderScenario ord
      → Calls props.updateOrderScenario (= Api.UpdateOrderScenario >> props.updateOrderContext)
        → App.fs: OrderContextCommand (Api.UpdateOrderScenario ctx)
          → CommandHandlers.updateOrderScenario state ctx
            → Dispatches LoadUpdatedOrderScenario Started
              → Wraps ctx in Api.UpdateOrderScenario, sends to server
                → Server: Api.evaluate logger provider (UpdateOrderScenario ctx)
                  → processScenarioOrder logger SolveOrder ctx
                    → SolveOrder ord |> OrderProcessor.processPipeline logger
```

### Path 2: Treatment Plan Update (Server-side)

```text
User clicks "Voorschrijven" button in Prescribe view
  → updateTreatmentPlan() adds scenario to TreatmentPlan
    → Api.UpdateTreatmentPlan tp sent to server
      → ServerApi.TreatmentPlan.updateTreatmentPlan logger provider tp
        → Wraps selected OrderScenario in Api.UpdateOrderScenario
          → OrderContext.evaluate logger provider
            → processScenarioOrder logger SolveOrder ctx
              → SolveOrder ord |> OrderProcessor.processPipeline logger
```

## Complete List of User Actions

The following **16 dropdown selections** in the Order modal each trigger `SolveOrder`:

| # | UI Label | Elmish Message | Order Field Modified |
| --- | --- | --- | --- |
| 1 | Frequentie | `ChangeFrequency` | `Schedule.Frequency` |
| 2 | Inloop tijd | `ChangeTime` | `Schedule.Time` |
| 3 | Keer Dosis | `ChangeSubstanceDoseQuantity` | `Component[0].Item[selected].Dose.Quantity` |
| 4 | Keer Dosis (adjusted) | `ChangeSubstanceDoseQuantityAdjust` | `Component[0].Item[selected].Dose.QuantityAdjust` |
| 5 | Dosering | `ChangeSubstancePerTime` | `Component[0].Item[selected].Dose.PerTime` |
| 6 | Dosering (adjusted) | `ChangeSubstancePerTimeAdjust` | `Component[0].Item[selected].Dose.PerTimeAdjust` |
| 7 | Dosering (rate) | `ChangeSubstanceRate` | `Component[0].Item[selected].Dose.Rate` |
| 8 | Dosering (rate adjusted) | `ChangeSubstanceRateAdjust` | `Component[0].Item[selected].Dose.RateAdjust` |
| 9 | Product Sterkte | `ChangeSubstanceComponentConcentration` | `Component[cmp].Item[itm].ComponentConcentration` |
| 10 | Concentratie | `ChangeSubstanceOrderableConcentration` | `Component[0].Item[selected].OrderableConcentration` |
| 11 | Hoeveelheid (substance) | `ChangeSubstanceOrderableQuantity` | `Component[0].Item[selected].OrderableQuantity` |
| 12 | Toedien Hoeveelheid | `ChangeOrderableDoseQuantity` | `Orderable.Dose.Quantity` |
| 13 | Pompsnelheid | `ChangeOrderableDoseRate` | `Orderable.Dose.Rate` |
| 14 | Totale Hoeveelheid | `ChangeOrderableQuantity` | `Orderable.OrderableQuantity` |
| 15 | Bereiding Hoeveelheid | `ChangeComponentOrderableQuantity` | `Component[selected].OrderableQuantity` |

### Visibility conditions

Not all dropdowns are visible at the same time. Visibility depends on:

- **Schedule type**: Continuous orders show rate fields; non-continuous show quantity/per-time fields
- **Component count**: Multi-component orders show component quantity and concentration selects
- **`useAdjust` flag**: When true, adjusted variants (QuantityAdjust, PerTimeAdjust, RateAdjust) are shown instead of the base variants
- **Once/OnceTimed**: Dose quantity adjust is only shown for once/once-timed schedules

## What Does NOT Trigger `SolveOrder`

The **navigation buttons** (min ⏮ / decrease ◀ / median ⏺ / increase ▶ / max ⏭) for frequency, dose quantity, dose rate, and component quantity follow a **different path**. They dispatch property-specific messages (e.g., `SetMinFrequencyProperty`, `IncreaseDoseQuantityProperty`) which route through `ChangeProperty` commands — not `SolveOrder`.

Similarly:

- **`SelectOrderScenario`** (clicking a scenario card's "bewerken" button) → uses `CalcValues`
- **`ResetOrderScenario`** (clicking the "Reset" button in the Order modal) → uses `ReCalcValues`
- **Component/Item selection** (`ChangeComponent`, `ChangeItem`) → local state only, no server call

## Key Source Files

| File | Role |
| --- | --- |
| `src/Informedica.GenORDER.Lib/Types.fs` | `OrderCommand` DU definition |
| `src/Informedica.GenORDER.Lib/OrderProcessor.fs` | `processPipeline` with `SolveOrder` case |
| `src/Informedica.GenORDER.Lib/Api.fs` | `evaluate` function mapping `UpdateOrderScenario` → `SolveOrder` |
| `src/Informedica.GenPRES.Client/Views/Order.fs` | Order editing modal with all `Change*` messages |
| `src/Informedica.GenPRES.Client/Views/Prescribe.fs` | Prescribe view wiring `updateOrderScenario` |
| `src/Informedica.GenPRES.Client/Views/TreatmentPlan.fs` | Treatment plan view wiring `updateOrderScenario` |
| `src/Informedica.GenPRES.Client/App.fs` | Elmish update routing `UpdateOrderScenario` → `LoadUpdatedOrderScenario` |
| `src/Informedica.GenPRES.Server/ServerApi.fs` | Server-side `updateTreatmentPlan` |
