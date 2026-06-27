# Session Decisions - 2026-03-10

## Nutrition Plan Init Fix

### DemoVersion Bug
- **Finding**: Not a bug. `setDemoVersion` in `ServerApi.fs:794-800` correctly returns `false` when `GENPRES_PROD=1`.
- **Root cause of previous confusion**: The `.env` file has `GENPRES_PROD=0`, but the script sets it to `"1"` before `loadDotEnv()`, and `loadDotEnv` only sets vars that aren't already set. So the override works correctly.

### Empty Scenarios Root Cause
- **Finding**: `getRules` in `GenOrder.Lib/Api.fs:534-621` ignores the `Indications`/`Generics` pick list arrays set on the Filter. It rebuilds them from all matching prescription rules.
- **The cascade requires**: `Indication`, `Generic`, `Route`, and `DoseType` all set to `Some` before scenarios are generated (line 606-608 match pattern).
- **The old `initNutritionPlan`** only set the pick list arrays (`Indications`, `Generics`) but not the individual selections — so `getRules` returned all 742 indications and 542 generics, nothing was auto-selected, and no scenarios were produced.

### Fix Approach
- **Two-phase initialization** in `initNutritionPlan`:
  1. **Discovery phase**: Set `Indication` explicitly (from single-element array), evaluate to discover available `Generics` and `DoseTypes`, intersect with configured generics.
  2. **Evaluation phase**: For each matching generic, evaluate with `Indication + Generic + DoseType` (first available) all set → produces scenarios.
- For the TPV rule set with a 1-year-old patient: only "Samenstelling C" matches (of B/C/D/E), with 3 dose types (dag 1/2/3). Using first dose type produces 2 scenarios.

### Key URL ID
- The script uses `1rfOo5UjGoVHT5h-bJxR7FS-Qgz4faRrNGLeu2Yj8SS8` (correct production URL).
- The `.env` file has `1JHOrasAZ_2fcVApYpt1qT2lZBsqrAxN-9SvBisXkbsM` (different spreadsheet, missing `Type` column → errors).

### Deferred: UI Select Boxes
- When multiple `DoseTypes` are available, the nutrition UI will need select boxes.
- Pattern to follow: `Prescribe.fs` filter selects using `SimpleSelect` component.

---

# Session Decisions - 2026-06-27

Three client-UI features this session. All changes are in `src/Informedica.GenPRES.Client/`
(the only `.fs` area the script-only policy permits direct edits). Builds clean via
`dotnet build src/Informedica.GenPRES.Client/Informedica.GenPRES.Client.fsproj`.

## 1. Patient view auto-close should not strand open dropdowns

- **File**: `Views/Patient.fs` (the 5s inactivity timer that collapses the patient accordion).
- **Problem**: collapsing the accordion reflows the whole page (it's the first child of the
  page Stack in `Pages/GenPres.fs`), detaching any open MUI dropdown popup (its own selects
  **or** a sibling child view's DataGrid COLUMNS/FILTERS popup) from its anchor → dangling list.
- **Decision**: gate the timer on a DOM check at fire time and **re-arm a fresh 5s** while any
  overlay is open (full-grace, user-chosen), rather than collapsing.
  - `anyOverlayOpen ()` = `document.querySelectorAll(".MuiModal-root:not(.MuiModal-hidden), .MuiPopper-root").length > 0`.
  - **Critical**: must exclude `.MuiModal-hidden`. The language menu (`Components/Localization.fs`)
    uses `keepMounted`, so its closed Popover/Modal root stays in the DOM permanently; without
    the `:not(.MuiModal-hidden)` the timer never fired (regression we hit and fixed).
  - Do NOT use an `[aria-expanded="true"]` selector — the accordion summary itself has it.
- DOM check chosen over open/close callbacks because MUI's own DataGrid toolbar popups can't be
  hooked; one DOM query covers patient selects + child-view popups + future popups.

## 2. Optimistic step value not reset when server returns the SAME value

- **Files**: `Views/Order.fs`, `Views/Nutrition.fs` (compute `revision`); `Views/ViewHelpers.fs`
  (`createNav` carries it); `Components/SimpleSelect.fs` (consumes it).
- **Problem (PR #372 follow-up)**: `SimpleSelect` reset its optimistic step delta only when the
  displayed `valueKey` changed. When a step overflowed and the server returned the *same* value,
  `valueKey` didn't change → the stale optimistic value stuck in the OrderView (background
  Prescribe view showed the correct value).
- **Decision**: add a monotonic `revision` counter, bumped on every new `Resolved` orderContext
  (Order.fs) / new `ctx` reference (Nutrition.fs), computed **during render via a ref compare**
  (`obj.ReferenceEquals`) so the new value is present on the response frame (no one-frame flash).
  Threaded through the `navigate` record (only stepping selects need it; `navigate=None` callers
  untouched). `SimpleSelect` adds `box revision` to its reset `useLayoutEffect` deps.
- **Note**: `navigate` records are built in 3+ places — `ViewHelpers.createNav` and **inline**
  records in Order.fs *and* Nutrition.fs (the `doseQtyNav`). All must carry every field or the
  structural anon-record type fails to compile. Easy to miss the inline Nutrition one.

## 3. Multi-component dose quantity: feasibility ceiling + saturate at max

- **Scope (important, narrowed twice with the user)**: applies ONLY to `orderable.dose.quantity`,
  and ONLY for **multi-component** orderables. Single component → orderable quantity follows the
  dose, no ceiling. The existing `canIncr` (`Components.Length = 1 || all DoseCount > 1`) and the
  "totale hoeveelheid" field being gated on `Components.Length > 1` both already encode this.
- **Server semantics (confirmed, drove terminology)**:
  - `OrderVariable.step` (`OrderVariable.fs:980`) moves **freely** along the increment grid with
    NO upper bound (comment: "may fall outside min/max … intentional"), then the order re-solves.
  - The downward correction on overflow is the **constraint re-solve being infeasible**, then
    `Api.fs:856` `Result.defaultValue sc.Order` **reverts to the previous order**. NOT a clamp.
  - Therefore "clamp" was the wrong word and the client should NOT bound the optimistic value to
    `DefinedConstraints.Max/Min` (that makes it *undershoot* the server). Only genuine bound =
    the structural feasibility ceiling.
- **Decisions / implementation** (all in `ViewHelpers.fs` + the two views):
  - `ovarStepTo (ceiling: decimal option) format ovar` — free stepping; applies only the
    feasibility `ceiling` + a one-increment floor (mirrors server non-zero-positive). `ovarStep`
    is the no-ceiling delegate. Replaced the old `DefinedConstraints`-range "clamp".
  - `orderableDoseQuantityCeiling ord` → `Some (max OrderableQuantity vals)` when `Components > 1`,
    else `None`. (For multi-component, max dose qty = orderable qty because `DoseCount_min = 1`,
    set by `setToMinIsOne` in `OrderProcessor.fs:93`.)
  - `incrementStepsToCeiling ceiling ovar` → how many defined-increment steps fit below the
    ceiling. Used by `saturateInc n` (in both views' dose-qty `increase` callback) to cap the
    DISPATCHED step count so an overflow **lands on the max** (feasible → no revert) instead of
    overshooting and reverting.
  - **Delta-saturation fix** (`SimpleSelect.fs`): `bumpInner`/`bumpOuter` now only accumulate a
    click if it actually changes the predicted value (`changesValue` compares step output
    before/after). Without this the raw delta grew past the visible ceiling, so reversing had a
    "dead zone" until the delta unwound below the max. General fix (also helps the floor).
- **Terminology retired**: "clamp" → "feasibility ceiling" / "saturate" / "revert" / "free
  stepping". `clampInc` renamed `saturateInc`. Swept all comments (Order/Nutrition revision
  comments, SimpleSelect, ViewHelpers) for residual "clamp"/"clamped back" wording.

### Open / deferred
- The outer `last` button (solved branch) dispatches `IncreaseDoseQuantityProperty(n, true)`
  **uncapped** — saturation only applied to the inner `increase`. Outer uses the calculated
  increment × server multiplier (`stepQuantity`), trickier to mirror client-side. Left as-is.
- A fully general server-side fix (saturate on infeasible step instead of `Result.defaultValue`
  revert at `Api.fs:856`) would cover fields where the client can't know the true max — but
  that's `.fs` server code → would need the `.fsx` prototype-and-migrate workflow, not done.
- Unit assumption in the ceiling `min`: dose qty and orderable qty share a unit (mL volume);
  holds for the orderable dose quantity, would be wrong if ever different-unit.
