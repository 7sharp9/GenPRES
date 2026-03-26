# Plan: Universal Layout for Content Overflow

## Context

Multiple views (Nutrition, Prescribe, OrderPlan) manually add `paddingBottom: 220px` to avoid content being hidden behind the Totals bottom drawer. The drawer height varies with content, making the fixed 220px unreliable. Table views use hardcoded `calc(100vh - 240px)`. Adding new views requires knowing about these magic numbers. The root cause: the BottomDrawer is a MUI persistent Drawer (`position: fixed`), which overlaps content instead of participating in document flow.

**Goal**: Make the layout universal so views don't need to know about the AppBar height, bottom drawer height, or add their own padding/height compensation.

## Approach: Replace Drawer with Inline Box + Pure Flexbox Layout

### Step 1: Convert BottomDrawer from MUI Drawer to inline Box

**File**: `src/Informedica.GenPRES.Client/Components/BottomDrawer.fs`

Replace the MUI `<Drawer anchor="bottom" variant="persistent">` with a simple `<Box>` that carries the same visual styling (grey background, padding). When `isOpen` is false, return null. This makes the totals area participate in normal flex layout flow — no more fixed positioning.

### Step 2: Restructure GenPres.fs to pure flexbox

**File**: `src/Informedica.GenPRES.Client/Pages/GenPres.fs`

- Wrap the entire page in a `Box` with `height: 100vh`, `display: flex`, `flexDirection: column`
- Give the content area (below AppBar) `flex: 1`, `minHeight: 0`, `display: flex`, `flexDirection: column`
- Replace `sxContainer` height from `calc(100vh - 88px)` to `flex: 1`, `display: flex`, `flexDirection: column`, `minHeight: 0`
- Make `sxPageBox.overflowY = "auto"` **unconditional** for all pages (remove the per-page match)
- The totals Box stays as-is — it's now inline content that takes its natural height

### Step 3: Remove per-view paddingBottom workarounds

**Files**:
- `Views/OrderPlan.fs` — remove `paddingBottom = (if isMobile then "16px" else "220px")`, remove `isMobile` hook if now unused
- `Views/Prescribe.fs` — remove `paddingBottom = (if isMobile then "16px" else "220px")`
- `Views/Nutrition.fs` — remove `paddingBottom = (if isMobile then "16px" else "220px")`

### Step 4: Remove per-view overflowY workarounds

**Files**:
- `Views/Interactions.fs` (line 314) — remove `overflowY = "auto"` from own Box
- `Views/Formulary.fs` (line 312) — remove `overflowY = "auto"` from own Box

### Step 5: Table heights — keep calc but simplify

The `ResponsiveTable` wraps the DataGrid in `<Box><div style={{ height: props.height }}>`. The outer Box has no height, so `height: 100%` won't resolve. **Keep `calc(100vh - ...)`** for table heights but derive from a simpler formula now that the layout is flexbox-based. The key improvement is that these views no longer need paddingBottom.

No change needed for EmergencyList.fs and ContinuousMeds.fs — their `calc(100vh - 240px)` still works and they never had the paddingBottom problem.

For OrderPlan.fs, keep `calc(100vh - 240px)` (already set).

## Files Modified

| File | Change |
|------|--------|
| `Components/BottomDrawer.fs` | Drawer -> inline Box |
| `Pages/GenPres.fs` | Flexbox layout, remove calc height, unconditional overflowY |
| `Views/OrderPlan.fs` | Remove paddingBottom + unused isMobile |
| `Views/Prescribe.fs` | Remove paddingBottom |
| `Views/Nutrition.fs` | Remove paddingBottom |
| `Views/Interactions.fs` | Remove redundant overflowY |
| `Views/Formulary.fs` | Remove redundant overflowY |

## Implementation Order

1. BottomDrawer.fs (foundational — must be first)
2. GenPres.fs (layout restructure)
3. OrderPlan.fs, Prescribe.fs, Nutrition.fs (remove paddingBottom)
4. Interactions.fs, Formulary.fs (remove overflowY)

## Verification

1. `dotnet run build` — verify compilation
2. `cd src/Informedica.GenPRES.Client && dotnet fable -o output -s -e .jsx` — verify Fable output
3. Check compiled JSX in `Client/output/` for correct styles
4. Manual test all pages: EmergencyList, ContinuousMeds, OrderPlan, Prescribe, Nutrition, Interactions, Formulary
   - Desktop: content scrolls within page-box, totals bar visible at bottom without overlap
   - Mobile: totals hidden, content scrolls naturally
   - Nutrition: add items until content overflows — vertical scrollbar should appear
   - OrderPlan: table + totals both visible, table doesn't extend behind totals
