# Design History: Universal Layout for Content Overflow

## Problem

Multiple views (Nutrition, Prescribe, OrderPlan) manually added `paddingBottom: 220px` to avoid
content being hidden behind the Totals bottom drawer. The drawer height varies with content, making
the fixed 220 px unreliable. Table views used hardcoded `calc(100vh - 240px)` that didn't account
for dynamic elements like the totals bar. Adding new views required knowing about these magic
numbers. The root cause: the BottomDrawer was a MUI persistent Drawer (`position: fixed`), which
overlapped content instead of participating in document flow.

**Goal**: Make the layout universal so views don't need to know about the AppBar height, bottom
drawer height, or add their own padding/height compensation.

## Principles

### 1. Unbroken flex chain from root to scrollable content

Every element from the viewport root to the scrollable page-box must be a flex container with
`display: flex; flexDirection: column; flex: 1; minHeight: 0`. If any element in the chain is
missing `minHeight: 0`, its flex children can overflow their parent instead of shrinking. If any
element is missing `display: flex`, its children fall back to percentage-based height semantics
instead of flex sizing.

### 2. Single point of scrolling

Only one element should produce a scrollbar: the page-box (`overflowY: auto`). Views inside the
page-box should not add their own overflow handling — they should either take their natural height
(letting page-box scroll) or fill the page-box using `height: 100%` (scrolling internally via
DataGrid). Double scrollbars indicate a broken flex chain or competing overflow declarations.

### 3. No viewport-relative magic numbers

Heights like `calc(100vh - 240px)` encode assumptions about sibling element sizes. When those
siblings change (e.g., adding a totals bar), the calc becomes wrong. Instead, use `height: 100%`
to fill available flex space, letting the flex chain determine the actual size.

### 4. Layout concerns belong in the layout, not in views

Individual views should not compensate for layout elements (AppBar, sidebar, totals bar). The
layout in GenPres.fs owns all structural concerns. Views receive their available space through
the flex chain and fill it.

## Previous Layout Structure

```text
React.Fragment                                    <- root (cannot take sx styles)
  Box (TitleBar -> AppBar position="static", ~64px tall)
  React.Fragment (SideMenu)
  Box (marginLeft for sidebar: 0px mobile / 240px desktop)
    Container  <- sxContainer: height = "calc(100vh - 88px)", marginTop = 3
      Stack    <- sxStack: height = "100%"
        patientBox   (flexBasis = 1)
        page-box     (flexGrow = 1, minHeight = 0, overflowY = per-page match)
          {pageView}
        Box (no sx)
          {totalsView}
            -> Views.Totals.View
                -> on mobile: returns <React.Fragment />
                -> on desktop: Components.BottomDrawer.View (isOpen = true)
                    -> <Drawer anchor="bottom" variant="persistent"> <- position: fixed!
  Modal (position: fixed)
```

Issues with this structure:

- The Drawer is `position: fixed`, taking zero height in document flow, which is why views
  needed `paddingBottom: 220px`.
- The `calc(100vh - 88px)` hardcoded the AppBar height (64 px) + marginTop (24 px).
- The per-page `overflowY` match required each new view to be added explicitly.
- Table views used `calc(100vh - 240px)` that didn't account for the totals bar height.

## Implemented Layout Structure

```text
Box (sxRoot: height 100vh, flex column, overflow hidden)        <- viewport constraint
  Box (sxTitleBarBox: flexShrink 0, flexGrow 0)                 <- neutralizes TitleBar's flexGrow
    TitleBar (AppBar position="static", ~64px)
  sideMenu                                                      <- rendered directly, no wrapper
  Box (sxMarginBox: flex 1, overflow hidden, flex column)       <- fills remaining height
    Container (sxContainer: flex 1, minHeight 0, flex column, paddingTop 3)
      Stack (sxStack: flex 1, minHeight 0, flex column)
        patientBox (flexBasis 1)
        page-box (flexGrow 1, minHeight 0, overflowY auto, paddingBottom 3)
          {pageView}
  Box (sxTotalsBar: flexShrink 0, sidebar marginLeft, bgcolor, marginTop 2)  <- full-width bottom bar
    Container                                                    <- centers content with main area
      {totalsView}
  Modal (position: fixed)
```

### Key Design Decisions

**Complete flex chain** (Principle 1): Every element from root to page-box is a flex container.
Without `minHeight: 0` on each level, flex children overflow their parent instead of shrinking.
Without `display: flex` on the marginBox, the Container falls back to percentage-based height
semantics. The chain was initially broken at two points (marginBox missing `display: flex`, Stack
using `height: 100%` instead of flex properties) which caused the totals bar to overlap content.

**TitleBar wrapper with `flexShrink: 0; flexGrow: 0`**: The TitleBar component internally returns
a `<Box sx={{flexGrow: 1}}>` wrapping the AppBar. In the previous non-flex root (`React.Fragment`),
this had no effect. In the new flex column root, `flexGrow: 1` caused the TitleBar to expand and
fill all available space. The wrapper Box neutralizes this without modifying the reusable TitleBar
component.

**Totals bar at root level with sidebar-aligned Container** (Principle 4): The totals bar wrapper
sits outside the marginBox at root level, with `flexShrink: 0` and the grey background. It has
the same `marginLeft` as the content area (via a shared `sxSidebarMargin` binding). Inside it, a
MUI `<Container>` centers the totals content to align with the main content area. The background
spans full width while the content matches the main layout alignment. The bar uses `marginTop: 2`
(not `paddingTop`) to create a visual gap above it — margin creates space *outside* the element
so the page-box scrollbar ends cleanly at the marginBox boundary without extending into the gap.

**Unconditional `overflowY: auto` on page-box** (Principle 2): All pages scroll uniformly inside
the page-box. The previous per-page match required each new view to be listed explicitly and used
`"hidden"` for unlisted pages on desktop, which prevented scrolling.

**Views with totals bars must not use `height: 100%`** (Principle 2): OrderPlan and Prescribe
previously had `height: 100%` on their root Box, which constrained them to the page-box height.
Combined with `calc(100vh - 240px)` on the DataGrid, content extended behind the totals bar
without triggering a scrollbar. Removing `height: 100%` lets the content take its natural height,
and the page-box scrolls when content overflows.

**`paddingTop: 3` on Container**: The original layout used `marginTop` on the Container to create
space below the AppBar. With the flex chain in place, padding keeps the spacing inside the flex
flow.

**`paddingBottom: 3` on page-box**: Provides breathing room between scrollable content and the
totals bar. The padding is inside the scrollable area, so it scrolls with the content.

## Changes Made

### Step 1: Eliminate BottomDrawer, inline totals in Totals.fs

**File**: `src/Informedica.GenPRES.Client/Views/Totals.fs`

The BottomDrawer component (`Components/BottomDrawer.fs`) wrapped the MUI persistent Drawer.
Since the totals are now an inline flex item, the Drawer is unnecessary. The styling is inlined
directly in `Totals.fs`:

- On mobile (`isMobile`): return `Unchecked.defaultof<JSX.Element>` (intentional null)
- On desktop: render a `<Box>` with padding, and a centered `<Stack direction="row">`
  containing the totals tables

The `bgcolor` is on the wrapper in GenPres.fs (full-width), not on the Totals Box (which is
inside a Container).

`Components/BottomDrawer.fs` is now dead code (no longer imported by any view).

### Step 2: Restructure GenPres.fs to pure flexbox

**File**: `src/Informedica.GenPRES.Client/Pages/GenPres.fs`

- Replace the root `<React.Fragment>` with `<Box sx={sxRoot}>`.
- Wrap `{titleBar}` in `<Box sx={sxTitleBarBox}>` to prevent flex growth.
- Render `{sideMenu}` directly as a child (no `<React.Fragment>` wrapper).
- Make `sxMarginBox` a flex container (`display: flex; flexDirection: column`).
- Replace `sxContainer` (`height: calc(100vh - 88px)`, `marginTop: 3`) with flex properties
  and `paddingTop: 3`.
- Replace `sxStack` (`height: 100%`) with `flex: 1; minHeight: 0; display: flex;
  flexDirection: column`.
- Make `sxPageBox.overflowY = "auto"` unconditional (remove the per-page match).
- Add `paddingBottom: 3` to `sxPageBox` for spacing above the totals bar.
- Wrap `{totalsView}` in `<Box sx={sxTotalsBar}><Container>...</Container></Box>` at root
  level, with shared sidebar margin and background color.
- Extract `sxSidebarMargin` as a shared binding used by both `sxMarginBox` and `sxTotalsBar`.
- Replace all bare `null` returns with `Unchecked.defaultof<JSX.Element>`.

### Step 3: Handle Recalculating state for totals

**File**: `src/Informedica.GenPRES.Client/Pages/GenPres.fs`

The `totalsView` match previously only matched `Resolved`, causing the totals bar to disappear
during recalculation. The `Deferred<'t>` type has a `Recalculating of 't` case that carries
previous data. Following the existing pattern used in `OrderPlan.fs` and `Order.fs`, the match
now includes both cases:

```fsharp
| Resolved pr
| Recalculating pr -> Views.Totals.View {| intake = pr.Intake |}
```

### Step 4: Remove per-view paddingBottom workarounds

- `Views/OrderPlan.fs` — removed `paddingBottom = (if isMobile then "16px" else "220px")` and
  the unused `isMobile` hook (only used for paddingBottom).
- `Views/Prescribe.fs` — removed `paddingBottom` (kept `isMobile` — used elsewhere).
- `Views/Nutrition.fs` — removed `paddingBottom` (kept `isMobile` — used elsewhere).

### Step 5: Remove per-view overflowY workarounds

- `Views/Interactions.fs` — removed `let sxBox = {| overflowY = "auto" |}` and the `sx={sxBox}`
  on the root `<Box>`.
- `Views/Formulary.fs` — removed `overflowY = "auto"` from the root `<Box>` sx.

### Step 6: Remove per-view height workarounds from totals-bar views

- `Views/OrderPlan.fs` — removed `height = "100%"` from root Box (was preventing page-box
  scrollbar from appearing when DataGrid overflowed behind totals bar).
- `Views/Prescribe.fs` — removed `height = "100%"` from root Box (same issue).

### Step 7: Consistent table layout — eliminate calc magic numbers

**Principle 3 applied**: Replace `calc(100vh - 240px)` with `height: 100%` in all table views,
and make ResponsiveTable propagate height through a flex chain.

**File**: `src/Informedica.GenPRES.Client/Components/ResponsiveTable.fs`

The ResponsiveTable previously rendered `<Box><div style={{ height: props.height }}>DataGrid</div></Box>`
with a filter Box above the div. When `height: 100%` was passed, the outer Box had no height
constraint, so the div resolved to zero. When `height: 100%` was set on the outer Box and
the inner div, the filter Box caused the combined height to exceed 100%, producing a double
scrollbar.

Fix: make the outer Box a flex column container with the passed height, the filter gets
`flexShrink: 0`, and the DataGrid div uses `flex: 1; minHeight: 0`:

```text
Box (height: props.height, display: flex, flexDirection: column)
  Box (filter, flexShrink: 0, marginBottom: 3)
  div (flex: 1, minHeight: 0, width: 100%)
    DataGrid (scrolls internally)
```

**Files**: `Views/EmergencyList.fs`, `Views/ContinuousMeds.fs`, `Views/OrderPlan.fs`

All three table views now use the same consistent pattern:

- Root wrapper: `<Box sx={{ height: "100%" }}>` (fills page-box)
- ResponsiveTable height prop: `"100%"` (fills root wrapper)
- No `calc(100vh - 240px)` anywhere

For views with a totals bar (OrderPlan), the page-box is smaller because the totals bar
consumes space at root level. The `height: 100%` chain automatically adapts — no magic numbers.

## Coding Conventions Applied

- **`Unchecked.defaultof<JSX.Element>`** instead of bare `null` for intentional empty JSX returns.
- **Named `let` bindings** for all sx anonymous records — no inline anonymous records in JSX
  interpolated strings.
- **No unnecessary nesting**: no `<React.Fragment>` or `<Box>` wrapping a single child.
- **Fable JSX output verification**: after editing Client `.fs` files, compile with
  `dotnet fable -o output -s -e .jsx` and inspect the `.jsx` files in `Client/output/` for
  nesting, structure, and correct sx properties.

## Files Modified

| File | Change |
|------|--------|
| `Views/Totals.fs` | Inline Box+Stack replacing BottomDrawer call; `Unchecked.defaultof<JSX.Element>` for mobile; centered Stack; padding only (bgcolor moved to GenPres.fs wrapper) |
| `Pages/GenPres.fs` | Root Box flex layout; TitleBar wrapper; marginBox as flex container; full flex chain through Container and Stack; totalsView at root level in sidebar-aligned Container with bgcolor; unconditional overflowY; paddingBottom on page-box; Recalculating match; shared sxSidebarMargin; `Unchecked.defaultof<JSX.Element>` |
| `Components/ResponsiveTable.fs` | Outer Box gets `height: props.height` with flex column layout; filter gets `flexShrink: 0`; DataGrid div uses `flex: 1; minHeight: 0` instead of `height: props.height` |
| `Views/EmergencyList.fs` | Root `React.Fragment` -> `Box height: 100%`; height prop `100%` |
| `Views/ContinuousMeds.fs` | Height prop `calc(100vh - 240px)` -> `100%` |
| `Views/OrderPlan.fs` | Remove paddingBottom + unused isMobile hook; root Box `height: 100%`; height prop `100%` |
| `Views/Prescribe.fs` | Remove paddingBottom and root `height: 100%` |
| `Views/Nutrition.fs` | Remove paddingBottom only |
| `Views/Interactions.fs` | Remove sxBox with overflowY |
| `Views/Formulary.fs` | Remove overflowY from root Box |

**Dead code**: `Components/BottomDrawer.fs` — no longer imported.

**Unchanged**: `Views/Parenteralia.fs` (no workaround existed).

## Verification

1. `dotnet run build` — verify full solution compilation.
2. `dotnet fable -o output -s -e .jsx` in `src/Informedica.GenPRES.Client/` — inspect JSX output.
3. Manual test all pages on desktop and mobile viewport:

   | Page | Desktop check | Mobile check |
   |------|---------------|--------------|
   | EmergencyList | Table fills viewport, single internal scrollbar | Table scrolls within page |
   | ContinuousMeds | Table fills viewport, single internal scrollbar | Table scrolls within page |
   | Prescribe | Content scrolls in page-box; Totals bar pinned at bottom without overlap | No Totals bar; content scrolls |
   | Nutrition | Add items until overflow — page-box scrollbar appears; Totals visible below | No Totals bar; content scrolls |
   | OrderPlan | Table scrolls internally; Totals bar visible at bottom without overlap | No Totals bar; table scrolls |
   | Interactions | Content scrolls inside page-box | Content scrolls naturally |
   | Formulary | Content scrolls inside page-box | Content scrolls naturally |
   | Parenteralia | Layout unaffected | Layout unaffected |
   | Settings | Layout unaffected | Layout unaffected |

4. Recalculation test: on Nutrition or Prescribe, change a dose — totals bar stays visible
   with previous values during recalculation, then updates when results arrive.
5. No double scrollbars on any page.
6. No `calc(100vh - ...)` in any view JSX output.
