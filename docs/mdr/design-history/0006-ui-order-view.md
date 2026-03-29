# ADR-0006: Quantitative Order Constraint Navigation

**Date**: 2024-01-01
**Status**: Proposed

## Context

Quantitative variables in a medication order (dose, frequency, rate, volume) are not freely editable: they are governed by solver-derived feasible domains, defined constraints, and equation-based dependencies. A formal model is needed to determine the navigability state of each variable and to provide appropriate navigation controls.

## Decision

Define a deterministic, solver-driven navigation model with four navigability states (`NON_NAVIGABLE`, `STEPABLE`, `SELECTABLE`, `NAVIGABLE`) and three navigation operations (value selection, min–median–max navigation, single-value increment/decrement). State transitions are governed by the formal state tables in Sections 7, 8, and 9 of this document.

## Consequences

- The UI always presents controls appropriate to the variable's current feasible domain.
- Re-anchoring after navigation is deterministic and consistent with the constraint solver.
- The `OrderProcessor` is the single authoritative source for state recomputation after any navigation action.

---

- [Quantitativen Order Constraint Navigation](#quantitativen-order-constraint-navigation)
  - [1. Purpose and Scope](#1-purpose-and-scope)
  - [2. Core Domain Model](#2-core-domain-model)
    - [2.1 Effective Domain](#21-effective-domain)
    - [2.2 Domain Invariant](#22-domain-invariant)
  - [3. Lifecycle of an OrderVariable](#3-lifecycle-of-an-ordervariable)
    - [3.1 First-Pass Domain Caching](#31-first-pass-domain-caching)
  - [4. Navigation Model](#4-navigation-model)
  - [5. Navigation States](#5-navigation-states)
    - [5.1 NON\_NAVIGABLE](#51-non_navigable)
    - [5.2 STEPABLE](#52-stepable)
    - [5.3 SELECTABLE](#53-selectable)
    - [5.4 NAVIGABLE](#54-navigable)
  - [6. Navigation State Recognition](#6-navigation-state-recognition)
  - [7. State Table — Value Selection](#7-state-table--value-selection)
  - [8. State Table — Min–Median–Max Navigation](#8-state-table--minmedianmax-navigation)
  - [9. State Table — Single Value Increment–Decrement](#9-state-table--single-value-incrementdecrement)
  - [10. Re-Anchoring Strategy](#10-re-anchoring-strategy)
  - [11. OrderProcessor Responsibilities](#11-orderprocessor-responsibilities)
  - [12. Acceptance Criteria](#12-acceptance-criteria)
  - [13. Practical implementation](#13-practical-implementation)
    - [13.1 UI Order View](#131-ui-order-view)
      - [UI Order View fields and their visibility conditions](#ui-order-view-fields-and-their-visibility-conditions)
      - [UI Order View navigation options](#ui-order-view-navigation-options)

## 1. Purpose and Scope

This proposal defines a deterministic, solver-driven implementation for **quantitative order constraint navigation** in a constraint-based ordering system.

Quantitative values are not freely editable. They are governed by:

- **Defined constraints** (design-time or scenario restrictions)
- **Equation-based dependencies**
- **Solver-derived feasible domains**

The objective is to:

1. Determine the **navigability state** of each quantitative `OrderVariable`.
2. Provide appropriate navigation controls.
3. Apply controlled **re-anchoring** when navigation occurs.
4. Guarantee a consistent, recomputed **new order state**.

Navigation semantics are formally defined by the state tables in Sections 7, 8 and 9.

---

## 2. Core Domain Model

An `OrderVariable` represents the combination of:

- **Variable Value** — representing the current calculated or selected value (`ValueRange`).
- **Defined constraints** — externally imposed domain restrictions.
- **Calculated constraints** — solver-derived feasible domain information.

Both Variable Value, Defined and Calculated constraints describe a domain that corresponds conceptually to a `ValueRange`, which may be:

| Type              | Represented | Value                                                             | Constraints correspondence    |
| ----------------- | ----------- | ----------------------------------------------------------------- | ----------------------------- |
| `Unrestricted`.   | -           | N/A as all values are set to `NonZeroPositive` at initialisation  | -                             |
| `NonZeroPositive` | x           | Initial state of the Value for an OrderVariable                   | -                             |
| `Min`             | x           | OrderVariable as an explicit Min                                  | Some Min                      |
| `Max`             | -           | N/A as an OrderVariable always has a Min or is `NonZeroPositive`  | -                             |
| `MinMax`          | x           | `MinMax`                                                          | Some Min, Some Max            |
| `Incr`            | -           | N/A as there always is a Min                                      | -                             |
| `MinIncr`         | x           | `MinIncr`                                                         | Some Min, Some Incr           |
| `IncrMax`         | -           | N/A as there always is a Min                                      | -                             |
| `MinIncrMax`      | x           | `MinIncrMax`                                                      | Some Min, Some Incr, Some Max |
| `ValSet`          | x           | `ValSet`                                                          | Some ValSet                   |

**Note**

- A `ValSet` with one value is considered a special case, `Solved`.
- Not all possible ranges are represented in the medication order.

### 2.1 Effective Domain

The **effective domain** is the domain against which the variable is validated and navigation decisions are made.

Conceptually:

- Defined constraints restrict what is allowed by scenario/rules.
- Calculated constraints restrict what is feasible after defined constraints are applied and equations are solved (first pass).
- The current variable value must lie within the effective domain.

If `Defined` is absent, the effective domain is `Calculated`.
If `Defined` is present, the effective domain is `Defined` before any calculations and `Calculated` after the first pass calculations.
If both are absent, the default domain is `NonZeroPositive`.

### 2.2 Domain Invariant

When set, the `Variable Value` must:

- Be **strictly greater than zero**
- Belong to the effective domain

---

## 3. Lifecycle of an OrderVariable

1. Defined constraints are loaded from the order scenario.
2. Defined constraints are applied to the variable value.
3. The solver evaluates all equation dependencies computing the value for each variable.
4. The calculated value is written back into Calculated constraints (only first pass, so it effectively caches the computed variable value).

### 3.1 First-Pass Domain Caching

After the first successful solve, the resulting Calculated domain represents the **baseline feasible domain** for that scenario.

If Defined constraints exist:

```text
Calculated ⊆ Defined
```

After the first solver pass, each OrderVariable has a value consistent with its Calculated constraints. The baseline Calculated domain is thus the cached version of the first pass solving result for an OrderVariable and can be reused during re-anchoring.

---

## 4. Navigation Model

Navigation operates strictly on the **effective feasible domain**.

Navigation:

- Modifies only the selected variable (the **anchor**) in a *new* computed state
- Never directly edits dependent variables
- Always triggers solver recomputation for dependent variables

This means that only one variable can be changed at which moment the rest of the variables are immediately re-calculated.

Navigation is offered only when the effective domain is:

- **Finite enumerable** (`ValSet`), or
- **Bounded and increment-stepped** (`MinIncrMax`)

All other domains are considered non-navigable.

There are generally two paths to navigate the quantitative order options:

1. By selecting a specific value from the effective domain, or
2. By stepping up or down by a defined increment from a solved value.

The first option implies that the selected value belongs to the effective domain of the variable, so it always complies with the defined and calculated constraints. The second option implies that the new anchor value (current value ± increment) may transgress the effective domain, but is followed by a re-solving step that restores consistency. This means the second option can temporarily violate these constraints.

When a specific order variable has a value that has transgressed the effective domain, the UI should show a warning indicator. When the user subsequently clears an order variable value — triggering a recalculation — any transgressed variable value should be reset to its effective calculated domain to restore option 1 navigation.

- Stepping is gated on whether the order is fully solved — increment/decrement buttons are disabled until all relevant order variables have a single resolved value, preventing discrepancies with prior calculated options.
- Clearing selectively resets only preparation variables that have gone out of bounds, using the calculated constraints as the reference, while leaving in-bound values untouched.

**Single-variable change invariant:** The system operates on the premise that after each change of a single variable, the solver runs to reconcile all dependent variables. Only at the very beginning are defined constraints set simultaneously — if these are incompatible, the solver will return an error. This represents an incompatibility in user-defined constraints that the system cannot and should not try to fix.

---

## 5. Navigation States

### 5.1 NON_NAVIGABLE

The effective domain is not enumerable and not bounded-step-enumerable, i.e., has no min, incr and max.

Examples:

- `Unrestricted`
- `NonZeroPositive`
- `Min`
- `Max`
- `MinMax`
- `ValSet` with one value and no defined increment

Behavior:

- No navigation controls are shown.

---

### 5.2 STEPABLE

The effective domain contains exactly one value and there is a defined increment.

Occurs when:

- `ValSet` contains one element
- a defined increment exists

If a defined increment constraint exists, stepping ±Δ is permitted as a **new anchor value**, followed by re-solving.

Behavior:

- UI shows stepwise increment/decrement

---

### 5.3 SELECTABLE

The effective domain is a finite `ValSet`. In case the `ValSet` is considered `Solved` and there is a defined increment, the state becomes `STEPABLE`.

Selecting a value produces a new order state and triggers solver recomputation.

Behavior:

- UI shows a selectable list of value options

---

### 5.4 NAVIGABLE

The effective domain is bounded and increment-stepped (`MinIncrMax`) and there is a defined increment.

Behavior:

The UI may expose:

- Navigation to a Minimum
- Navigation to a Maximum
- Navigation to a Median
- Navigation to a Percentage

---

## 6. Navigation State Recognition

```fsharp
recognizeNavigationState : OrderVariable -> NavigationDescriptor
```

Determines:

- NON_NAVIGABLE
- STEPABLE
- SELECTABLE
- NAVIGABLE

Based on the structure of the effective (Calculated) value domain and/or existence of a defined increment.

| Value                   | Defined      | Navigation State |
| ----------------------- | ------------ | ---------------- |
| Non-Zero-Positive       |              | NON_NAVIGABLE    |
| `MinIncrMax`            | Increment    | NAVIGABLE        |
| Multiple Value `ValSet` |              | SELECTABLE       |
| Single Value `ValSet`   | No Increment | NON_NAVIGABLE    |
| Single Value `ValSet`   | Increment    | STEPABLE         |

- All `OrderVariables` can start as NON_NAVIGABLE.
- All `OrderVariables` can become SELECTABLE.
- Only `OrderVariables` with a defined increment can be NAVIGABLE.
- Only `OrderVariables` with a defined increment can be STEPABLE.

---

## 7. State Table — Value Selection

Applies to bounded value-set domains, i.e. OrderVariable where the Value ValueRange is `ValSet` with more than one value.

| Phase               | Variable                   | Calculated              | Defined   |
| ------------------- | -------------------------- | ----------------------- | ----------- |
| Initial             | NonZeroPositive            | Empty                   | ValSetOpt |
| Defined Applied     | Range from Defined         | Empty                   | ValSetOpt |
| First Solve         | Range from Calculations    | ValSetOpt (First Solve) | ValSetOpt |
| Navigation          | SELECTABLE                 | ValSetOpt (First Solve) | ValSetOpt |
| Re-Anchor Dependent | `ValSet` (First Solve)     | ValSetOpt (First Solve) | ValSetOpt |
| Update Solve        | `ValSet` from Calculations | ValSetOpt (First Solve) | ValSetOpt |

Invariant:

- All Variable values remain within Calculated and/or Defined constraints.

---

## 8. State Table — Min–Median–Max Navigation

Applies to bounded increment-stepped domains, i.e. OrderVariable where the Value ValueRange is `MinIncrMax` and the defined constraints define an increment.

| Phase               | Variable                       | Calculated                          | Defined               |
| ------------------- | ------------------------------ | ----------------------------------- | --------------------- |
| Initial             | NonZeroPositive                | Empty                               | MinOpt IncrOpt MaxOpt |
| Defined Applied     | Range from Defined             | Empty                               | MinOpt IncrOpt MaxOpt |
| First Solve         | `MinIncrMax` from Calculations | MinOpt IncrOpt MaxOpt (First Solve) | MinOpt IncrOpt MaxOpt |
| Navigation          | NAVIGABLE                      | MinOpt IncrOpt MaxOpt (First Solve) | MinOpt IncrOpt MaxOpt |
| Re-Anchor Dependent | `MinIncrMax` (First Solve)     | MinOpt IncrOpt MaxOpt (First Solve) | MinOpt IncrOpt MaxOpt |
| Update Solve        | `MinIncrMax` from Calculations | MinOpt IncrOpt MaxOpt (First Solve) | MinOpt IncrOpt MaxOpt |

Invariant:

- All Variable values remain within Calculated and/or Defined constraints.

---

## 9. State Table — Single Value Increment–Decrement

Applies when variables are solved to STEPABLEs, i.e. OrderVariable with a single value `ValSet` and the defined constraints define an increment

| Phase                 | Variable                   | Calculated                          | Defined               |
| --------------------- | -------------------------- | ----------------------------------- | --------------------- |
| Initial               | NonZeroPositive            | Empty                               | MinOpt IncrOpt MaxOpt |
| Defined Applied       | Range from Defined         | Empty                               | MinOpt IncrOpt MaxOpt |
| First Solve           | `ValSet` from Calculations | MinOpt IncrOpt MaxOpt (First Solve) | MinOpt IncrOpt MaxOpt |
| Increment / Decrement | STEPABLE                   | MinOpt IncrOpt MaxOpt (First Solve) | MinOpt IncrOpt MaxOpt |
| Re-Anchor Dependent   | NonZeroPositive            | MinOpt IncrOpt MaxOpt (First Solve) | MinOpt IncrOpt MaxOpt |
| Update Solve          | Range from Calculations    | MinOpt IncrOpt MaxOpt (First Solve) | MinOpt IncrOpt MaxOpt |

Important:

- During increment/decrement, the anchor value may transgress previous Calculated and/or Defined constraints.
- Dependent variables reset to `NonZeroPositive` before re-solving and can also transgress the Calculated and/or Defined constraints.

Implementation note — `useCalc` safety guard:

- `useCalc = false` (first step, `n = 1`): allows the anchor to transgress **Calculated** constraints, per the spec above. Defined constraints remain visible to the user for violation feedback.
- `useCalc = true` (subsequent steps, `n > 1`): constrains stepping to the Calculated domain as a safety guard against unbounded drift.
- Note: The `n` parameter and exact stepping semantics are work in progress — the UI does not yet supply the exact `n` value.

---

## 10. Re-Anchoring Strategy

Re-anchoring computes a new order state.

**ValueRange consistency model:** After the first solve, every OrderVariable's ValueRange is consistent with every other's. The solver operates on ValueRanges (MinIncrMax, ValSet, etc.), not single values. This consistency determines whether dependent variables need resetting during re-anchoring:

- **NAVIGABLE/SELECTABLE** (SetMin/Max/Median, value selection): The anchor stays within its current domain, so dependent ValueRanges remain valid. No explicit dependent reset is needed — the `calcMinMax` constraint propagation is sufficient.
- **STEPABLE** (Increase/Decrease): The anchor may leave its current domain, so dependent ValueRanges must be widened to NonZeroPositive to allow the solver to find new feasible values.

**Two-phase pipeline:** Re-anchoring is split across two pipeline dispatches:

1. **ChangeProperty pipeline** (2 steps): sets the anchor value, resets dependents (STEPABLE only), then propagates min/max constraints via `calcMinMax`.
2. **SolveOrder pipeline** (dispatched separately): performs a full solve to derive concrete values from the updated constraint system.

This split separates constraint narrowing from value resolution.

---

## 11. OrderProcessor Responsibilities

The OrderProcessor must:

1. Apply state-table transitions exactly.
2. Preserve first-pass Calculated constraints.
3. Reset dependent variables according to navigation mode.
4. Invoke solver.
5. Return a new order state.

---

## 12. Acceptance Criteria

1. Every OrderVariable resolves to exactly one navigation state.
2. Navigation is offered only for enumerable or bounded-step domains.
3. Re-anchoring produces an updated order.
4. Baseline Calculated constraints are preserved.
5. Solver recomputation restores domain consistency.

---

## 13. Practical implementation

Given the order model and the defined constraints that exist in real life, particularly the increment constraint, only a limited set of OrderVariables can be used for direct navigation:

- Schedule Frequency: always has an increment of 1 (whatever the unit) as it implies a count
- Schedule Frequency: always has a defined constraints value set.
- Orderable Dose Quantity: always defaults to the largest pharmaceutical form increment (inverse of the divisibility).
- Orderable Dose Rate: always defaults to the smallest infusion rate (which is a hardware spec), for example 0.1 mL/hour.
- Component Orderable Quantity: the individual quantities that make up an Orderable, where the increment is the pharmaceutical form increment.

### 13.1 UI Order View

Shows an order for editing.

#### UI Order View fields and their visibility conditions

| #  | Group          | Element                           | substIndx | comp > 1  | itms > 0 | itms > 1 | useAdjust | Continuous | Timed | OnceTimed | Once | Discont. | Additional data guard                            |
| -- | -------------- | --------------------------------- | :-------: | :-------: | :------: | :------: | :-------: | :--------: | :---: | :-------: | :--: | :------: | ------------------------------------------------ |
| 1  |                | Component name                    |     —     |     ✓     |    —     |    —     |     —     |     ✓      |   ✓   |     ✓     |  ✓   |    ✓     | —                                                |
| 2  |                | Substance name                    |     —     | not empty |    —     |    ✓     |     —     |     ✓      |   ✓   |     ✓     |  ✓   |    ✓     | —                                                |
| 3  | Prescription   | Substance Dose Quantity           |     ✓     |     —     |    ✓     |    —     |     —     |     —      |   ✓   |     ✓     |  ✓   |    ✓     | has Vals                                         |
| 4  | Prescription   | Substance Dose Quantity Adjust    |     ✓     |     —     |    ✓     |    —     |     ✓     |     —      |   —   |     ✓     |  ✓   |    —     | has Vals                                         |
| 5  | Prescription   | Substance Dose PerTime            |     ✓     |     —     |    ✓     |    —     |     —     |     —      |   ✓   |     ✓     |  ✓   |    ✓     | PerTimeAdjust if useAdjust, else PerTime         |
| 6  | Prescription   | Substance Dose PerTime Adjust     |     ✓     |     —     |    ✓     |    —     |     ✓     |     —      |   ✓   |     ✓     |  ✓   |    ✓     | PerTimeAdjust if useAdjust, else PerTime         |
| 7  | Prescription   | Substance Dose Rate               |     ✓     |     —     |    ✓     |    —     |     —     |     ✓      |   —   |     —     |  —   |    —     | RateAdjust if useAdjust, else Rate               |
| 8  | Prescription   | Substance Dose Rate Adjust        |     ✓     |     —     |    ✓     |    —     |     ✓     |     ✓      |   —   |     —     |  —   |    —     | RateAdjust if useAdjust, else Rate               |
| 9  | Preparation    | Component Orderable Quantity      |     —     |     ✓     |    —     |    —     |     —     |     ✓      |   ✓   |     ✓     |  ✓   |    ✓     | —                                                |
| 10 | Preparation    | Substance Component Concentration |     ✓     |     —     |    —     |    —     |     —     |     ✓      |   ✓   |     ✓     |  ✓   |    ✓     | DefinedConstraints.Vals.Length > 1 (≤1 → hidden) |
| 11 | Preparation    | Substance Orderable Quantity      |     ✓     |     ✓     |    ✓     |    —     |     —     |     ✓      |   —   |     —     |  —   |    —     | has Vals                                         |
| 12 | Preparation    | Substance Orderable Concentration |     ✓     |     ✓     |    ✓     |    —     |     —     |     —      |   ✓   |     ✓     |  ✓   |    ✓     | has Vals                                         |
| 13 | Preparation    | Orderable Quantity                |     —     |     ✓     |    —     |    —     |     —     |     ✓      |   ✓   |     ✓     |  ✓   |    ✓     | has Vals                                         |
| 14 | Administration | Schedule Frequency                |     —     |     —     |    —     |    —     |     —     |     —      |   ✓   |     —     |  —   |    ✓     | has Vals                                         |
| 15 | Administration | Orderable Dose Quantity           |     —     |     —     |    —     |    —     |     —     |     —      |   ✓   |     ✓     |  ✓   |    ✓     | has Vals                                         |
| 16 | Administration | Orderable Dose Rate               |     —     |     —     |    —     |    —     |     —     |     ✓      |   ✓   |     ✓     |  —   |    —     | —                                                |
| 17 | Administration | Schedule Time                     |     —     |     —     |    —     |    —     |     —     |     ✓      |   ✓   |     ✓     |  ✓   |    ✓     | has Vals                                         |

**Note** Rows 5 and 6 and rows 7 and 8 are mutually exclusive: if useAdjust is true, then the Adjust fields are shown, otherwise the non-Adjust fields are shown.

#### UI Order View navigation options

| #  | Group          | Element                           | Label                 | Defined Increment | Selectable | Navigable | Stepable | Clearable |
| -- | -------------- | --------------------------------- | --------------------- | ----------------- | ---------- | --------- | -------- | --------- |
| 1  |                | Component name                    | componenten           | —                 | ✓          | —         | —        | —         |
| 2  |                | Substance name                    | stoffen               | —                 | ✓          | —         | —        | —         |
| 3  | Prescription   | Substance Dose Quantity           | keer dosis            | —                 | ✓          | —         | —        | —         |
| 4  | Prescription   | Substance Dose Quantity Adjust    | keer dosis            | —                 | ✓          | —         | —        | ✓         |
| 5  | Prescription   | Substance Dose PerTime            | dosering              | —                 | ✓          | —         | —        | ✓         |
| 6  | Prescription   | Substance Dose PerTime Adjust     | dosering              | —                 | ✓          | —         | —        | ✓         |
| 7  | Prescription   | Substance Dose Rate               | dosering              | —                 | ✓          | —         | —        | ✓         |
| 8  | Prescription   | Substance Dose Rate Adjust        | dosering              | —                 | ✓          | —         | —        | ✓         |
| 9  | Preparation    | Component Orderable Quantity      | bereiding hoeveelheid | ✓                 | —          | ✓         | ✓        | —         |
| 10 | Preparation    | Substance Component Concentration | product sterkte       | —                 | ✓          | —         | —        | —         |
| 11 | Preparation    | Substance Orderable Quantity      | {stof} hoeveelheid    | —                 | ✓          | —         | —        | —         |
| 12 | Preparation    | Substance Orderable Concentration | {stof} concentratie   | —                 | ✓          | —         | —        | —         |
| 13 | Preparation    | Orderable Quantity                | totale hoeveelheid    | ✓                 | ✓          | —         | —        | —         |
| 14 | Administration | Schedule Frequency                | frequentie            | ✓                 | ✓          | —         | ✓        | —         |
| 15 | Administration | Orderable Dose Quantity           | toedien hoeveelheid   | ✓                 | —          | ✓         | ✓        | —         |
| 16 | Administration | Orderable Dose Rate               | inloop snelheid       | ✓                 | —          | ✓         | ✓        | —         |
| 17 | Administration | Schedule Time                     | inloop tijd           | —                 | ✓          | —         | —        | ✓         |

**Note**

- Order variables with the same label specify that only one order variable at a time are visible in the order UI view.
- All order variables could be, theoretically, Selectable. However, Component Orderable Quantity, Orderable Dose Quantity and Orderable Dose Rate are restricted to be only Navigable.
- Schedule Frequency could be Navigable, only is restricted to be only Selectable and Stepable
- The server provides SetMin/Max/MedianScheduleFrequency commands (NAVIGABLE) for Frequency for flexibility, but the UI should not expose them per the current spec. The server intentionally does not enforce navigation-state restrictions — the UI is the gatekeeper.
