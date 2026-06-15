# Plan: guard incompatible-unit comparisons in Check.fs

## Problem

`Check.checkInRangeOf` (Check.fs:269) compares a test `MinMax` against a
reference `MinMax` via `MinMax.inRange` → `ValueUnit.cmp`. When the two ranges
carry units from **different groups**, `cmp` throws:

```text
System.Exception: cannot compare ValueUnit
  ([|50N|],  Count/kg/day)   with   ([|150N|], IU/kg/week)
System.Exception: cannot compare ValueUnit
  ([|1N|],   Droplet)        with   ([|1/10N|], MilliGram)
```

The throw is swallowed in `inRangeOf` (Check.fs:644-648) and turned into a
**fake pass** (`Some true`) plus a console write — a check that crashed is
reported as "in range". Two defects: (1) no group compatibility guard before
comparing; (2) exception masked as a PASS.

## Root cause

- `cmp` requires `eqsGroup`-compatible units. Check.fs never tests this.
- `gradeAdjust` only gates on the *adjust* unit (kg/m²/g via `checkAdjustUnit`),
  not the full unit group. So `Count/kg/day` vs `IU/kg/week` slips through
  (kg matches kg) and the substance/time groups still differ → throw.
- Plain `grade` (quantity, no adjust) has no gate at all → `Droplet` vs `mg`
  throws.

## Decisions (confirmed)

- New `Severity` case `IncomparableUnits` (distinct from `NotComparable` =
  missing dose type, and `UnitMismatch` = kg-vs-m² adjust mismatch).
- Scope: **unit guard only** + fix the fake-pass swallow. No IO/pure refactor.
- Tests live directly in `tests/Informedica.GenFORM.Tests/Tests.fs`.

## Changes

### 1. Check.fs — add Severity case (Check.fs:29-37)

```fsharp
| UnitMismatch        // kg vs m2 — cannot compare
| IncomparableUnits   // unit groups differ (e.g. Count/kg/day vs IU/kg/week,
                      // Droplet vs mg) — cannot compare
| NotComparable       // missing dose type / both ranges empty
```

### 2. Check.fs — pure guard helpers (add near `checkInRangeOf`, ~Check.fs:251)

```fsharp
/// Full unit (incl. combi units) of a MinMax, taken from its min or max limit.
let rangeUnit (mm: MinMax) =
    match mm.Min |> Option.map Limit.getValueUnit, mm.Max |> Option.map Limit.getValueUnit with
    | Some vu, _
    | _, Some vu -> vu |> ValueUnit.getUnit |> Some
    | _ -> None


/// IR safety: two ranges are comparable only when their unit groups match
/// (e.g. mg/kg/day vs mg/kg/day). Empty / unit-less ranges are treated as
/// comparable so the existing empty-range short-circuits still apply.
let rangesComparable (refRange: MinMax) (testRange: MinMax) =
    match rangeUnit refRange, rangeUnit testRange with
    | Some ru, Some tu -> ru |> ValueUnit.Group.eqsGroup tu
    | _ -> true
```

Both are pure → public (no `private`).

### 3. Check.fs — guard inside `grade` (Check.fs:659)

Front-load the group check before any `inRangeOf` call. Covers plain `grade`
*and* `gradeAdjust` (which delegates to `grade`), so both crash paths are
caught.

```fsharp
let grade msg (normRef: MinMax) (absRef: MinMax) (test: MinMax) : Severity option * string =
    let incomparable ref =
        ref |> MinMax.isEmpty |> not && not (rangesComparable ref test)

    if (test |> MinMax.isEmpty |> not) && (incomparable normRef || incomparable absRef) then
        Some IncomparableUnits,
        $"{gstand.doseLimitTarget}\t{r}\t{p}\t{msg}: eenheden niet vergelijkbaar (kan niet worden gecontroleerd)"
    else
        let nb, nmsg = inRangeOf msg normRef test
        let ab, amsg = inRangeOf msg absRef test
        // ... existing match body unchanged ...
```

### 4. Check.fs — kill the fake pass in `inRangeOf` catch (Check.fs:644-648)

The proactive guard now prevents the throw; the catch becomes a true backstop
and must no longer report a crash as PASS.

```fsharp
let inRangeOf msg refRange testRange =
    try
        checkInRangeOf $"{gstand.doseLimitTarget}\t{r}\t{p}\t{msg}: " refRange testRange
    with e ->
        writeErrorMessage $"{e}"
        None, ""        // was: (Some true, "...kan niet... foutmelding")  ← fake pass
```

`None, ""` is dropped by `grade`'s `None, None` branch; the real warning is
emitted by the step-3 guard. (Removes the hidden error-as-pass; console write
left as defensive logging, unchanged.)

### 5. ServerApi.Services.fs — extend `severityWrap` (REQUIRED, Services.fs:67-76)

Adding a DU case makes this exhaustive match incomplete → compile error.
`IncomparableUnits` is a "cannot compare" signal → Caution (blue), grouped with
`UnitMismatch`/`NotComparable`.

```fsharp
| Check.UnitMismatch
| Check.NotComparable
| Check.IncomparableUnits
| Check.NoMonitoring -> Caution
```

Update doc comment line 65 accordingly. (Only exhaustive `match` on
`Check.Severity` in the repo; `didPass`/`didNotPass` use `= Within`, the rate
label uses a wildcard — neither needs touching.)

## Tests (tests/Informedica.GenFORM.Tests/Tests.fs, module `CheckTests`)

`grade` is a closure inside `checkDoseRuleWith`, not exposed — unit-test the
exposed pure helpers `rangeUnit` / `rangesComparable` (the actual guard logic).
`mmOf vmin vmax u` helper already exists; build combi units with
`ValueUnit.per`.

```fsharp
// helpers for combi units (confirm exact constructors against Units module:
//   IU  -> Units.International.iu (or equivalent)
//   droplet -> Units.Volume.droplet)
let private perKgDay u = u |> ValueUnit.per Units.Weight.kiloGram |> ValueUnit.per Units.Time.day
let private perKgWeek u = u |> ValueUnit.per Units.Weight.kiloGram |> ValueUnit.per Units.Time.week

test "rangesComparable true for same unit group (mg/kg/day)" {
    Check.rangesComparable
        (mmOf 1N 5N (Units.Mass.milliGram |> perKgDay))
        (mmOf 2N 3N (Units.Mass.milliGram |> perKgDay))
    |> Expect.isTrue "same group comparable"
}

test "rangesComparable false for Count/kg/day vs IU/kg/week" {
    Check.rangesComparable
        (mmOf 50N 50N  (Units.Count.times |> perKgDay))
        (mmOf 150N 150N (Units.International.iu |> perKgWeek))
    |> Expect.isFalse "different groups not comparable"
}

test "rangesComparable false for droplet vs mg" {
    Check.rangesComparable
        (mmOf 1N 1N Units.Volume.droplet)
        (mmOf 1N 1N Units.Mass.milliGram)
    |> Expect.isFalse "droplet vs mass not comparable"
}

test "rangesComparable true when a range is empty" {
    Check.rangesComparable MinMax.empty (mmOf 1N 5N Units.Mass.milliGram)
    |> Expect.isTrue "empty ref treated as comparable (empty short-circuit applies)"
}

test "rangeUnit None on empty, Some on populated" {
    Check.rangeUnit MinMax.empty |> Expect.isNone "empty"
    Check.rangeUnit (mmOf 1N 5N Units.Mass.milliGram) |> Expect.isSome "populated"
}
```

Optional integration test: if `tests/.../fixtures/doserules.json` holds a rule
whose GenFORM unit group differs from its G-Standaard match, assert
`checkDoseRule` returns an `IncomparableUnits` signal instead of throwing.
Heavier (needs ZIndex fixtures) — list as follow-up, not blocking.

## Verification

1. `dotnet run build` — confirms the new DU case + Services.fs match compile.
2. `dotnet run servertests` (or `dotnet test tests/Informedica.GenFORM.Tests/`).
3. Reproduce the two original crashing selections → expect `IncomparableUnits`
   warning rows, no exception.

## Risk / notes

- `ValueUnit.Group.eqsGroup` has a known TODO (ValueUnit.fs:816) about
  nested-combi nuances; for these cases (Count/IU, Volume/Mass) it returns
  false correctly. No change needed.
- Per script-only policy: source edits (Check.fs, Services.fs) are listed for
  the user to migrate. If a script prototype is preferred first, the pure
  helpers (`rangeUnit`, `rangesComparable`) drop straight into a `Check.fsx`
  shadow module.
