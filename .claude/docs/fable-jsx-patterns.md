# Fable JSX Patterns in GenPRES

## How JSX.jsx Works

Fable 4+ supports JSX via F# interpolated strings. `JSX.jsx $"""..."""` compiles
the template to actual JSX that the bundler (Vite) processes. Interpolation holes
(`{value}`) can only appear in **attribute value** or **child** positions.

Fable auto-extracts `import` statements from the template and hoists them to the
top of the generated `.jsx` file.

## Two Ways to Pass JavaScript Objects as Props

### 1. F# Anonymous Records (preferred in this codebase)

F# anonymous records (`{| ... |}`) are compiled by Fable directly to plain
JavaScript objects (POJOs). When used inside a JSX interpolation hole, the
F# anonymous record becomes a JS object prop:

```fsharp
<Box sx={ {| height="100%"; paddingBottom="120px" |} }>
```

Compiles to roughly: `<Box sx={{ height: "100%", paddingBottom: "120px" }}>`

**Rules:**
- Use `=` for assignment (F# anonymous record syntax), not `:`
- Use `;` to separate fields
- Property names must be valid F# identifiers (e.g., `paddingBottom`, not `pb`)
- Backtick-quoted names work for CSS selectors: `` {| ``& .MuiDrawer-paper`` = ... |} ``
- Can be defined outside the template and interpolated by name:
  ```fsharp
  let cellSx = {| minWidth = 350 |}
  // then in JSX:
  <Box sx={cellSx}>
  ```

### 2. Raw JavaScript Object Literal (double-brace escaping)

Use quadruple braces `{{{{ }}}}` to produce a literal JS object `{{ }}` in the
interpolated string (F# `$"..."` uses `{{` to escape a single `{`):

```fsharp
<Box sx={{{{ pb: "120px" }}}}>
```

Compiles to: `<Box sx={{ pb: "120px" }}>`

**Rules:**
- Use `:` for assignment (JavaScript object syntax), not `=`
- Use `,` to separate fields
- MUI shorthand property names like `pb`, `mt`, `mx` work here
- This is a raw JS literal — no F# type checking on property names

### When to Use Which

| Aspect | Anonymous Record `{| |}` | Raw JS `{{{{ }}}}` |
|--------|--------------------------|---------------------|
| Type safety | F# checks field names | No checking |
| MUI shorthands (`pb`, `mt`) | Must use full names (`paddingBottom`, `marginTop`) | Shorthands work |
| Reusability | Can bind to a `let` and reuse | Inline only |
| Nesting | Natural F# nesting | Manual JS nesting |
| Consistency | Matches rest of codebase | Ad-hoc |

**Codebase convention:** Prefer anonymous records for consistency. Use full CSS
property names (`paddingBottom`) rather than MUI shorthands (`pb`) when using
anonymous records.

## Common Patterns in GenPRES

```fsharp
// Simple styling
<Box sx={ {| mt = 5; display = "flex"; p = 20 |} }>

// Responsive Grid breakpoints
let halfSize = {| xs = 12; md = 6 |}
<Grid size={halfSize}>

// MUI color constants
<AccordionSummary sx={ {| bgcolor=Mui.Colors.Grey.``100`` |} }>

// Nested CSS selectors (backtick-quoted)
let drawerSx = {| ``& .MuiDrawer-paper`` = {| bgcolor = Mui.Colors.Grey.``100`` |} |}
<Drawer sx={drawerSx}>
```

## Reference
- Fable blog: https://fable.io/blog/2022/2022-10-12-react-jsx.html
- Fable docs (anonymous records -> POJOs): https://fable.io/docs/javascript/features.html
