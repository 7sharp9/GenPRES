# ADR: Template-Based Navigation to Prescribe View

**Date**: 2026-03-29

**Status**: Accepted

**References**:

- [Architecture Overview](architecture.md)
- [Core Domain Model](../../domain/core-domain.md)

## Summary

Multiple views in GenPRES allow users to click an item (emergency medication, continuous medication, nutrition component) and navigate to the Prescribe view with a pre-filled OrderContext filter. This ADR documents the common pattern, current inconsistencies, and the recommended shared approach.

## Context

The Prescribe view uses an `OrderContext` with a `Filter` containing selection fields (`Generic`, `Indication`, `Route`, `DoseType`) and available-option arrays (`Generics`, `Indications`, `Routes`, `DoseTypes`). When a user selects an item from a list view, the application must:

1. Identify which item was clicked
2. Map the item's data to OrderContext filter selections
3. Navigate to the Prescribe view
4. Send the filter to the server for validation
5. Handle the case where the selections don't match any dose rules

Three views currently implement this pattern:

| View | Source Data | Template Mechanism |
|------|-----------|-------------------|
| EmergencyList | `BolusMedication` (from Google Sheets CSV) | Explicit `Template*` fields |
| ContinuousMeds | `ContinuousMedication` (from Google Sheets CSV) | Direct fields on the record |
| Nutrition | `OrderContext` (from server) | Not selection-based (detail view) |

## Current Implementation

### EmergencyList

The `BolusMedication` type carries four explicit template fields parsed from CSV columns (`template-generic`, `template-route`, `template-dose-type`, `template-indication`). These allow the spreadsheet author to decouple the display medication from the prescribe filter values.

**Row ID format**: `"{index}.{hospital}.{category}.{name}"`

**Matching**: `item.EndsWith($".{m.Hospital}.{m.Category}.{m.Generic}")`

**Filter construction** (App.fs `OnSelectEmergencyListItem`):

- `Generic`: `TemplateGeneric` if non-empty, else `Generic`
- `Route`: `TemplateRoute` if non-empty, else `"INTRAVENEUS"`
- `Indication`: `TemplateIndication` if non-empty, else `None`
- `DoseType`: `TemplateDoseType` parsed via `DoseType.doseTypeFromString` if non-empty, else `None`

### ContinuousMeds

The `ContinuousMedication` type uses its own fields directly (no separate template fields). The CSV columns `indication`, `dosetype`, `generic` serve as both display and filter values.

**Row ID format**: `"{index}.{name}"`

**Matching**: `item.EndsWith($".{m.Medication}")`

**Filter construction** (App.fs `OnSelectContinuousMedicationItem`):

- `Generic`: `selected.Generic`
- `Route`: hardcoded `"INTRAVENEUS"`
- `Indication`: `selected.Indication` if non-empty, else `None`
- `DoseType`: `selected.DoseType` parsed via `DoseType.doseTypeFromString` if non-empty, else `None`

### Nutrition

The Nutrition view is fundamentally different. It is a detail/editor view that operates within an existing `OrderContext`, not a selection view that constructs one. It does not participate in the template pattern.

## Problems with the Current Approach

### Inconsistent row identification

Each view constructs row IDs differently. EmergencyList includes hospital and category for disambiguation; ContinuousMeds uses only the medication name. This means ContinuousMeds can select the wrong item when duplicate medication names exist across categories.

### Duplicated filter construction logic

Both `OnSelectEmergencyListItem` and `OnSelectContinuousMedicationItem` contain nearly identical code to build an `OrderContext` from filter fields. The only differences are which record fields are read and whether template overrides exist.

### No shared "template" abstraction

EmergencyList has explicit `Template*` fields on `BolusMedication`; ContinuousMeds uses direct fields. A ContinuousMedication entry cannot override its generic name for prescribing (e.g., display "morfine pomp" but prescribe as "morfine").

### Hardcoded route

ContinuousMeds hardcodes `"INTRAVENEUS"`. EmergencyList defaults to it but allows override. If a continuous medication needs a different route, it cannot be configured.

## Decision

### Common template record

Introduce a shared `PrescribeTemplate` record that captures the fields needed to navigate to the Prescribe view:

```fsharp
type PrescribeTemplate =
    {
        Generic: string
        Route: string
        Indication: string
        DoseType: string
    }
```

Both `BolusMedication` and `ContinuousMedication` should expose a function that produces a `PrescribeTemplate` from their data. For `BolusMedication`, the template fields take precedence over display fields. For `ContinuousMedication`, direct fields are used (with the option to add template override columns to the CSV later).

### Shared filter construction

A single function builds an `OrderContext` from a `PrescribeTemplate`:

```fsharp
let fromTemplate (template: PrescribeTemplate) : OrderContext =
    { OrderContext.empty with
        Filter =
            { OrderContext.empty.Filter with
                Generic = if template.Generic = "" then None else Some template.Generic
                Route = if template.Route = "" then None else Some template.Route
                Indication = if template.Indication = "" then None else Some template.Indication
                DoseType =
                    if template.DoseType = "" then None
                    else Some (DoseType.doseTypeFromString template.DoseType)
            }
    }
```

This eliminates the duplicated empty-string checks in both handlers.

### Consistent row identification

All list views that support template navigation should use a row ID format that includes enough fields to uniquely identify the source record:

```
"{index}.{hospital}.{category}.{name}"
```

When a field is empty (e.g., no hospital), it still appears as an empty segment in the ID, keeping the format predictable.

### Server-side validation

The server validates template selections in `getScenarios` (GenORDER.Lib/Api.fs). When the input filter has selections but no matching dose rules exist, it raises an error: `"Geen doseerregels gevonden voor het geselecteerde filter"`. The client detects this specific error and automatically retries with an empty filter, which populates the Prescribe view with all available options for the patient.

This validation is shared across all template-based navigation paths and should not be duplicated.

### Error handling flow

```
Click item → build OrderContext from template → send UpdateOrderContext to server
    ↓
Server validates → dose rules found?
    YES → return scenarios → Prescribe view shows results
    NO  → return Error "Geen doseerregels..."
              → client shows warning snackbar
              → client retries with empty filter
              → Prescribe view shows all available options
    OTHER ERROR → client shows error snackbar, no retry
```

## Consequences

- Template override capability becomes available to all list views, not just EmergencyList
- Filter construction logic is defined once, reducing risk of divergence
- Row identification is consistent and robust against duplicate names
- Adding template columns to the ContinuousMeds CSV becomes a data-only change (no code changes needed)
- The Nutrition view remains excluded from this pattern as it is architecturally different

## Migration Path

1. Add `PrescribeTemplate` type to `Informedica.GenPRES.Shared/Types.fs`
2. Add `fromTemplate` function to the `OrderContext` module in `Informedica.GenPRES.Shared/Models.fs`
3. Add `toTemplate` functions to `BolusMedication` and `ContinuousMedication` modules
4. Refactor `OnSelectEmergencyListItem` and `OnSelectContinuousMedicationItem` in `App.fs` to use the shared function
5. Update ContinuousMeds row ID format to include hospital and category
6. Optionally add `template-*` columns to the ContinuousMeds CSV for override capability
