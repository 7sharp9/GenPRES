# Plan: Universal Layout for Content Overflow

## Context

Multiple views (Nutrition, Prescribe, OrderPlan) manually add `paddingBottom: 220px` to avoid content
being hidden behind the Totals bottom drawer. The drawer height varies with content, making the fixed
220 px unreliable. Table views use hardcoded `calc(100vh - 240px)`. Adding new views requires knowing
about these magic numbers. The root cause: the BottomDrawer is a MUI persistent Drawer
(`position: fixed`), which overlaps content instead of participating in document flow.

**Goal**: Make the layout universal so views don't need to know about the AppBar height, bottom drawer
height, or add their own padding/height compensation.

## Current Layout Structure (as-is)

Understanding what already exists prevents duplicating work in Steps 2–4:

```
React.Fragment
  Box (TitleBar → AppBar position="static", ~64px tall)
  React.Fragment (SideMenu)
  Box (marginLeft for sidebar: 0px mobile / 240px desktop)
    Container  ← sxContainer: height = "calc(100vh - 88px)", marginTop = 3
      Stack    ← sxStack: height = "100%"  (MUI Stack is flex column by default)
        patientBox   (flexBasis = 1)
        page-box     (flexGrow = 1, minHeight = 0, overflowY = per-page match)
          {pageView}
        Box (no sx)
          {totalsView}
            → Views.Totals.View
                → on mobile: returns <React.Fragment />  (no totals shown)
                → on desktop: Components.BottomDrawer.View (isOpen = true, …)
                    → <Drawer anchor="bottom" variant="persistent"> ← position: fixed!
```

The 88px in `calc(100vh - 88px)` equals AppBar Toolbar height (≈ 64 px) plus `marginTop = 3`
(MUI spacing unit 3 = 24 px). Because the Drawer is `position: fixed` it takes zero height in
document flow, which is why views need `paddingBottom: 220px`.

Notable: `sxPageBox` already has `flexGrow = 1` and `minHeight = 0` — the flex groundwork is
partially in place. The per-page `overflowY` match currently covers Prescribe, Nutrition,
OrderPlan, Interactions, Parenteralia, and Formulary (auto), plus a default branch that uses
"hidden" on desktop and "auto" on mobile.

## Approach: Replace Drawer with Inline Box + Pure Flexbox Layout

### Step 1: Convert BottomDrawer from MUI Drawer to inline Box

**File**: `src/Informedica.GenPRES.Client/Components/BottomDrawer.fs`

Replace the MUI `<Drawer anchor="bottom" variant="persistent">` with a plain `<Box>` that carries
the same visual styling (grey background, padding). When `isOpen` is false, return null (existing
API contract preserved). This makes the totals area participate in normal flex layout flow — no
more `position: fixed` overlay.

> **Note**: `Views/Totals.fs` calls `Components.BottomDrawer.View {| isOpen = true; … |}` and
> already returns `<React.Fragment />` on mobile before reaching BottomDrawer (via its own
> `isMobile` check). No change is needed in `Totals.fs`; the component's API is unchanged.

### Step 2: Restructure GenPres.fs to pure flexbox

**File**: `src/Informedica.GenPRES.Client/Pages/GenPres.fs`

The outer `Box` that applies `marginLeft` for the sidebar is not currently a flex container, so
`flex: 1` on its child Container won't work without also making the `Box` flex. The full chain
that needs updating:

- Add `display: flex`, `flexDirection: column`, `height: 100vh`, `overflow: hidden` to the
  **outer** `Box` (the one that applies `marginLeft` for the sidebar). The `overflow: hidden` is
  necessary to prevent flex children from creating a double scrollbar alongside the page-box.
- Replace `sxContainer` (`height = "calc(100vh - 88px)"`, `marginTop = 3`) with
  `flex: 1`, `minHeight: 0`, `display: flex`, `flexDirection: column`.
  The `marginTop = 3` can be removed because the AppBar is `position: static` and already
  occupies space in document flow — the flex chain handles vertical placement automatically.
- Make `sxPageBox.overflowY = "auto"` **unconditional** for all pages (remove the per-page
  match). This also correctly enables scrolling for pages that currently fall into the
  `"hidden"` branch on desktop (e.g., EmergencyList, Settings).
- The totals `Box` stays as-is — it is now inline flex content and takes its natural height,
  causing `page-box` (with `flexGrow = 1`) to shrink accordingly.

### Step 3: Remove per-view paddingBottom workarounds

**Files** (confirmed locations as of current codebase):

- `Views/OrderPlan.fs` (line ~410) — remove `paddingBottom = (if isMobile then "16px" else "220px")`.
  Verify `isMobile` has no other usages in this file before removing the hook call.
- `Views/Prescribe.fs` (line ~480) — remove `paddingBottom = (if isMobile then "16px" else "220px")`.
  Keep the `isMobile` hook — it is used extensively elsewhere in Prescribe.fs for stack
  direction, padding, and conditional rendering.
- `Views/Nutrition.fs` (line ~1612) — remove `paddingBottom = (if isMobile then "16px" else "220px")`.
  Keep the `isMobile` hook — it is used elsewhere in Nutrition.fs.

### Step 4: Remove per-view overflowY workarounds

**Files** (confirmed locations as of current codebase):

- `Views/Interactions.fs` (line 319) — remove `let sxBox = {| overflowY = "auto" |}` and the
  corresponding `sx={sxBox}` attribute on the root `<Box>`.
- `Views/Formulary.fs` (line 312) — remove `overflowY = "auto"` from the root `<Box>` sx.
  Keep the `isMobile` hook — it is used elsewhere in Formulary.fs for responsive layout.

### Step 5: Table heights — keep calc but document the derivation

The `ResponsiveTable` component wraps the DataGrid in:

```fsharp
<Box>
  <div style={{ height = props.height; width = "100%" }}>
    <DataGrid … />
  </div>
</Box>
```

The outer `Box` has no explicit height, so `height: 100%` would not resolve. Therefore explicit
`calc(100vh - …)` values remain necessary for DataGrid containers.

**Keep the existing values**:

- `EmergencyList.fs` — `calc(100vh - 240px)` — no change needed.
- `ContinuousMeds.fs` — `calc(100vh - 240px)` — no change needed.
- `OrderPlan.fs` — `calc(100vh - 240px)` — no change needed.

The 240 px accounts for AppBar (≈ 64 px) + PatientBar (≈ 88 px) + page margins (≈ 88 px). These
views never had the `paddingBottom` problem because they render their table as the primary content
without a Totals bar below. After the flexbox conversion the paddingBottom problem in Prescribe,
Nutrition and OrderPlan disappears, but the DataGrid heights in EmergencyList and ContinuousMeds
continue to work independently.

> **Future improvement**: once the ResponsiveTable outer `Box` is given `flex: 1; min-height: 0`,
> the DataGrid `<div>` could use `height: 100%` instead of a `calc()` expression, removing the
> last magic numbers. This is out of scope for this change.

## Files Modified

| File | Change |
|------|--------|
| `Components/BottomDrawer.fs` | Replace MUI `<Drawer>` with inline `<Box>`; return null when `isOpen` is false |
| `Pages/GenPres.fs` | Outer Box: add flex column + height 100vh + overflow hidden; Container: flex 1 + minHeight 0; remove marginTop 3; unconditional overflowY auto on page-box |
| `Views/OrderPlan.fs` | Remove paddingBottom; check and remove `isMobile` hook if now unused |
| `Views/Prescribe.fs` | Remove paddingBottom only (keep `isMobile` — used elsewhere) |
| `Views/Nutrition.fs` | Remove paddingBottom only (keep `isMobile` — used elsewhere) |
| `Views/Interactions.fs` | Remove `sxBox` with `overflowY = "auto"` (line 319) |
| `Views/Formulary.fs` | Remove `overflowY = "auto"` from root Box sx (line 312) |

**Files confirmed as no-change needed**:

- `Views/Totals.fs` — already handles mobile (returns Fragment) before calling BottomDrawer; BottomDrawer API is unchanged.
- `Views/EmergencyList.fs` / `ContinuousMeds.fs` — no paddingBottom workaround; calc() heights remain valid.
- `Views/Parenteralia.fs` — no paddingBottom workaround.

## Implementation Order

1. `BottomDrawer.fs` — foundational; must be first (converts fixed overlay to inline flex item)
2. `Pages/GenPres.fs` — layout restructure (enables flex chain)
3. `Views/OrderPlan.fs`, `Views/Prescribe.fs`, `Views/Nutrition.fs` — remove paddingBottom
4. `Views/Interactions.fs`, `Views/Formulary.fs` — remove redundant overflowY

## Verification

1. `dotnet run build` — verify full solution compilation (always specify this, not bare
   `dotnet build`; see AGENTS.md).
2. `dotnet run` — start the full application (server + client with hot reload on
   `http://localhost:5173`).
3. Manual test all pages on desktop and mobile viewport:

   | Page | Desktop check | Mobile check |
   |------|---------------|--------------|
   | EmergencyList | Table fills viewport, no double scrollbar | Table scrolls within page |
   | ContinuousMeds | Table fills viewport, no double scrollbar | Table scrolls within page |
   | Prescribe | Content scrolls; Totals bar pinned at bottom without overlap | No Totals bar; content scrolls |
   | Nutrition | Add items until overflow — vertical scrollbar appears; Totals visible below | No Totals bar; content scrolls |
   | OrderPlan | Table + Totals both visible; table does not extend behind Totals bar | No Totals bar; table scrolls |
   | Interactions | Content scrolls inside page-box, not inside own Box | Content scrolls naturally |
   | Formulary | Content scrolls inside page-box, not inside own Box | Content scrolls naturally |
   | Parenteralia | Layout unaffected | Layout unaffected |
   | Settings | Layout unaffected | Layout unaffected |
