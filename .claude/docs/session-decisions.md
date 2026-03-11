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
