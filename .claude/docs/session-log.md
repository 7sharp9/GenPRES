# Session Log

## 2026-03-08: Click-Counting Navigation Buttons — PLANNING

### Plan Created
- Task: Add click-counting/debounce behavior to increase/decrease navigation buttons
- Plan file: `.claude/plans/composed-wishing-truffle.md`

### Key Decisions
1. **React-side state** (not Elmish) for transient click counts — `React.useState`/`useRef`/`useEffect`
2. **New `ClickCountingButton.fs`** — self-contained component with debounce (700ms) + hold-to-repeat (150ms)
3. **Navigate prop type change**: `decrease`/`increase` become `int -> unit` (from `unit -> unit`)
4. **No Msg DU changes** — existing `ntimes: int` parameter already supports this
5. **Condition**: debounce only when `navigable=false AND solved=true`
6. **Scope**: only `decrease`/`increase`; `first`/`last`/`median` remain immediate
7. **Visual feedback**: MUI Badge showing count when > 1

### Critical Code Locations
- `ViewHelpers.createNav`: lines 59-84 in ViewHelpers.fs
- `SimpleSelect.View` navigate rendering: lines 79-104 in SimpleSelect.fs
- Inline dose qty navigate: lines 1501-1538 in Order.fs
- Timer pattern reference: lines 135-147 in Patient.fs
- fsproj: SimpleSelect.fs is line 20

### Files to Change
- `Components/ClickCountingButton.fs` — **new**
- `Components/SimpleSelect.fs` — update navigate prop + rendering
- `Views/ViewHelpers.fs` — change `createNav` return type
- `Views/Order.fs` — update inline navigate record (~line 1519)
- `Informedica.GenPRES.Client.fsproj` — add new file

---

## Previous: Add Interactive Controls to Nutrition View — COMPLETE

### Phase A: Shared Infrastructure
- Added `getWarning`, `orderSelect`, `createNav` to ViewHelpers.fs
- Added `NavigateNutritionOrderContext` to Api.fs + server handler

### Phase B+C: Elmish State + Interactive Controls
- Full Nutrition.fs rewrite with Elmish, navigation, interactive controls
- Row-based component display with MUI Grid
