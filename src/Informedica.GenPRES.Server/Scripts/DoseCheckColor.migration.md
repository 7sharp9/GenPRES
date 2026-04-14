# Migration patch: color-coded G-Standaard dose check results

This document describes the source-file changes required to complete the
feature prototyped in `DoseCheckColor.fsx`. Per the project's script-only
policy, these `.fs` edits are left for the user to review and apply.

Scope: three files — one shared type, one shared model default, one server
service. After these are applied, the already-edited Client view
(`src/Informedica.GenPRES.Client/Views/Formulary.fs`) will compile and
render correctly.

---

## 1. `src/Informedica.GenPRES.Shared/Types.fs`

Add `DoseCheck` to the `Formulary` record (currently lines 532–549):

```fsharp
    type Formulary =
        {
            Generics: string[]
            Indications: string[]
            Routes: string[]
            Forms: string[]
            DoseTypes: DoseType[]
            PatientCategories: string[]
            Products: string[]
            Generic: string option
            Indication: string option
            Route: string option
            Form: string option
            DoseType: DoseType option
            PatientCategory: string option
            Patient: Patient option
            Markdown: string
            DoseCheck: TextBlock[]     // NEW
        }
```

`TextBlock` already exists earlier in the file (lines 328–333); no
additional import is required.

---

## 2. `src/Informedica.GenPRES.Shared/Models.fs`

Initialize the new field in `Formulary.empty` (currently lines 2363–2380):

```fsharp
    module Formulary =

        let empty: Formulary =
            {
                Generics = [||]
                Indications = [||]
                Routes = [||]
                Forms = [||]
                DoseTypes = [||]
                PatientCategories = [||]
                Products = [||]
                Generic = None
                Indication = None
                Route = None
                Form = None
                DoseType = None
                PatientCategory = None
                Patient = None
                Markdown = ""
                DoseCheck = [||]           // NEW
            }
```

---

## 3. `src/Informedica.GenPRES.Server/ServerApi.Services.fs`

Two changes inside `module FormularyService`:

### 3a. Replace `checkDoseRules` (lines 37–59)

Replace the existing function with a version that returns the raw
`didNotPass` strings **unformatted** (so the caller can both classify by
the original tab-separated form and produce the display string).

```fsharp
    let checkDoseRules provider pat (dsrs: DoseRule[]) =
        let routeMapping = Api.getRouteMapping provider

        let empt, rs =
            dsrs
            |> Array.distinctBy (fun dr -> dr.Generic, dr.Form, dr.Route, dr.DoseType)
            |> Array.map (Check.checkDoseRule routeMapping pat)
            |> Array.partition (fun c -> c.didPass |> Array.isEmpty && c.didNotPass |> Array.isEmpty)

        rs
        |> Array.filter (_.didNotPass >> Array.isEmpty >> not)
        |> Array.collect _.didNotPass
        |> Array.filter String.notEmpty
        |> Array.distinct
        |> function
            | [||] ->
                [|
                    for e in empt do
                        // sentinel string preserves 4-tab shape so classify falls through to DoseRange
                        $"\t\t\tgeen doseer bewaking gevonden voor {e.doseRule.Generic}"
                |]
                |> Array.distinct

            | xs -> xs
```

Note: only the "no rules matched" sentinel is altered (extra tabs) so that
the downstream classifier receives a uniform 4-field shape and the
classifier's default branch — `DoseRange` — is not accidentally hit for
this case. The aggregate severity handles the empty case separately, so
the tabs are cosmetic; you may also keep the original message and let the
classifier's `_ -> DoseRange` branch handle it — but then the "no rules
found" variant would render red. Using `Valid` for that case is cleaner;
see 3b.

### 3b. Rewrite the Markdown-assembly block in `get` (lines 103–133)

Replace the single `Markdown` assignment with one that splits the result
into `Markdown` (rule details only) and `DoseCheck` (colored blocks).

```fsharp
            |> fun form ->
                match form.Generic, form.Indication, form.Route with
                | Some _, Some _, Some _ ->
                    writeDebugMessage $"start checking {dsrs |> Array.length} rules"

                    let rawLines =
                        dsrs |> checkDoseRules provider filter.Patient

                    // classify by the 4th tab-separated field
                    let isFrequency (s: string) =
                        match s |> String.split "\t" with
                        | [ _; _; _; msg ] -> msg.Contains "frequenties"
                        | _ -> false

                    let singleRule = dsrs |> Array.length = 1

                    let formatted =
                        rawLines
                        |> Array.map (fun s ->
                            match s |> String.split "\t" with
                            | [ s1; _; p; s2 ] ->
                                if singleRule then $"{s1} {s2}"
                                else $"{s1} {p} {s2}"
                            | _ -> s)

                    let wrap =
                        if rawLines |> Array.isEmpty then Valid
                        elif rawLines |> Array.forall isFrequency then Warning
                        else Alert

                    let doseCheck =
                        match formatted with
                        | [||] ->
                            [| "Ok!" |> Mappers.parseTextItem |> Valid |]
                        | xs ->
                            xs |> Array.map (Mappers.parseTextItem >> wrap)

                    writeDebugMessage $"finished checking {dsrs |> Array.length} rules"

                    { form with
                        Markdown = dsrs |> DoseRule.Print.toMarkdown
                        DoseCheck = doseCheck
                    }

                | _ ->
                    { form with
                        Markdown = ""
                        DoseCheck = [||]
                    }
```

`Mappers.parseTextItem` is already exposed in
`src/Informedica.GenPRES.Server/ServerApi.Mappers.fs` (line 428) and is
used elsewhere in the same module.

Opens: ensure `open Shared.Types` at the top of
`ServerApi.Services.fs` still provides `TextBlock`, `Valid`, `Warning`,
`Alert` (it does — already imported, line 12).

---

## Verification after migration

1. `dotnet run build` — must succeed.
2. `dotnet run servertests`.
3. `./debug.sh` (or `dotnet run`) → `http://localhost:5173` → Formulary tab.
   Exercise three cases:
   - Rule match with no violations → green "Ok!".
   - Rule match where only frequency differs → orange (Warning) block(s).
   - Rule match with a dose-range violation → red (Alert) block(s).

Classifier/aggregation logic is validated interactively in
`DoseCheckColor.fsx` (all four cases print `OK`).
