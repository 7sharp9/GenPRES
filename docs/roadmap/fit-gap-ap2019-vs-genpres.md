# Fit-Gap Analysis: AfsprakenProgramma 2019 vs GenPRES

## Context

The AfsprakenProgramma 2019 (AP2019) is the PICU/NICU clinical workflow application at UMC Utrecht / WKZ, launched November 2019. This analysis maps its medication, feeding, and fluids requirements against the current GenPRES Prescribe, Nutrition, and TreatmentPlan views to identify what GenPRES already covers, what partially exists, and what is missing.

**Source**: <https://picuwkz.nl/protocollen/werkafspraken/afsprakenprogramma-2019/>

---

## 1. Continuous Medication (PICU)

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 1.1 | Standard-solution-based prescribing (concentration + rate) | **Fit** | Prescribe | GenPRES supports continuous dose type with rate-based dosing and solution rules |
| 1.2 | Weight-based AND absolute dose display | **Fit** | Prescribe | Adjusted dose (per kg/m²) and absolute dose both shown in scenario details |
| 1.3 | Manual entry: generic name, syringe amount, unit, total volume, dosing unit | **Partial** | Prescribe | Generic/route/form selection present; syringe-specific fields (syringe volume, total syringe amount) not explicit — mapped to component/orderable quantities |
| 1.4 | Epidural solution protocols (weight-based volumes, pump settings) | **Gap** | — | No epidural-specific workflow; would need dedicated solution rules and weight-band logic |
| 1.5 | Dosing monitoring signals (red/yellow/blue) | **Fit** | Prescribe | Severity color coding: Valid/Caution/Warning/Alert maps to AP2019's blue/yellow/red |
| 1.6 | Concentration-based calculation from desired dose + rate | **Fit** | Prescribe | GenSOLVER bidirectional calculation supports deriving concentration from dose+rate |

## 2. Continuous Medication (NICU)

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 2.1 | Infusion letter (infuusbrief) — integrated continuous med overview | **Gap** | — | No infusion letter concept; GenPRES uses individual order scenarios |
| 2.2 | Pre-configured syringe volume (12 ml), solution fluid (glucose), rate (0.5 ml/hr) | **Partial** | Prescribe | Solution rules can encode defaults, but NICU-specific syringe presets not implemented |
| 2.3 | Dosing from desired dose + rate → concentration calculation | **Fit** | Prescribe | Bidirectional solver handles this |
| 2.4 | Divisibility and original medication concentration checks | **Partial** | Prescribe | Constraint system validates ranges; explicit divisibility UI not present |
| 2.5 | NICU epidural protocol (single formula) | **Gap** | — | Same as 1.4 |
| 2.6 | NICU monitoring: rate deviation, solution fluid, volume, max concentration | **Partial** | Prescribe | Constraint violations shown as warnings; specific "blue = deviation from standard" not distinguished from errors |
| 2.7 | 17:00 infusion letter workflow (current → 17:00 transfer → pharmacy email → sign → transfer back) | **Gap** | — | No time-based transfer workflow; no pharmacy email integration |

## 3. Discontinuous Medication

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 3.1 | Generic selection via autocomplete | **Fit** | Prescribe | Autocomplete for generic name (desktop), dropdown (mobile) |
| 3.2 | Form/route selection | **Fit** | Prescribe | Route and pharmaceutical form selection present |
| 3.3 | Indication selection | **Fit** | Prescribe | Indication filter available |
| 3.4 | Frequency selection | **Fit** | Prescribe | Frequency navigation with min/median/max controls |
| 3.5 | Correction type (none, weight, BSA, per dose) | **Fit** | Prescribe | Adjustment unit (kg, m²) built into dose rules |
| 3.6 | Dose per administration with min/max limits | **Fit** | Prescribe | Dose quantity navigation with min/max constraints |
| 3.7 | Dosing help: G-Standaard, Kinderformularium, FTK, Handboek Parenteralia | **Partial** | — | Rules sourced from Kinderformularium and FTK; no direct link-out to external references from UI |
| 3.8 | Medication without dosing (ointments, eye drops — frequency only) | **Gap** | Prescribe | Current workflow requires dose rule match; no "frequency-only" mode |
| 3.9 | Medication in solution (pre-configured solution amount, fluid, infusion time) | **Fit** | Prescribe | Solution rules + diluent selection + preparation display |
| 3.10 | Multiple active substances (e.g., cotrimoxazol) | **Fit** | Prescribe | Multi-component orders with per-item dose calculations |
| 3.11 | PRN (zo nodig) medication | **Gap** | Prescribe | No PRN toggle; no "max frequency" mode; no PRN comments |
| 3.12 | Comments/remarks per medication | **Gap** | Prescribe/TreatmentPlan | No free-text comment field on orders |
| 3.13 | Sorting by indication | **Partial** | TreatmentPlan | Table sortable by medication and route; no indication column |
| 3.14 | TallMan lettering for look-alike medications | **Gap** | — | No TallMan capitalization in medication names |
| 3.15 | Substance divisibility display | **Gap** | Prescribe | Not explicitly shown; constraint system handles valid increments internally |

## 4. Discontinuous Medication Monitoring

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 4.1 | Yellow: underdosing / unchecked | **Fit** | Prescribe | Caution severity level |
| 4.2 | Red: overdosing / exceeds max | **Fit** | Prescribe | Alert severity level |
| 4.3 | Red: concentration exceeds max or disallowed solution | **Fit** | Prescribe | Constraint violations shown |
| 4.4 | Red: infusion time faster than minimum | **Fit** | Prescribe | Rate/time constraints enforced |
| 4.5 | Blue: non-standard frequency | **Partial** | Prescribe | No distinct "informational" severity separate from caution |

## 5. TPN / Parenteral Nutrition

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 5.1 | Independent protein solution selection (weight-dependent) | **Fit** | Nutrition | TPN context with generic selection; solution rules weight-dependent |
| 5.2 | Independent infusion rate control | **Fit** | Nutrition | Rate navigation with min/median/max |
| 5.3 | Independent volume control | **Fit** | Nutrition | Component quantity navigation |
| 5.4 | Up to 5 infusions (TPN + electrolytes + phosphate + lipids + extra) | **Partial** | Nutrition | TPN (1) + Lipid (1) + ElectrolyteGlucose (multiple) supported; max 5 not enforced; no dedicated phosphate category |
| 5.5 | CVL vs peripheral access (affects electrolyte dilution) | **Partial** | Prescribe | Solution rules can encode access-dependent constraints; no explicit CVL toggle in Nutrition view |
| 5.6 | Electrolyte entry with arrow keys / click input | **Fit** | Nutrition | Component quantity controls with increase/decrease navigation |
| 5.7 | Pump stand calculation (24-hour) | **Gap** | — | No automatic 24-hour pump stand calculation |
| 5.8 | TPN Day Choice (automatic protocol progression) | **Gap** | — | No day-based TPN protocol escalation |
| 5.9 | Decoupled volume and rate (independent of 24-hour timeline) | **Fit** | Nutrition | Volume and rate are independently navigable |
| 5.10 | Infusion time > 24h warning | **Gap** | — | No specific 24-hour infusion time validation |
| 5.11 | Max 5 infusions enforcement | **Gap** | Nutrition | No hard limit; ElectrolyteGlucose can be added without bound |
| 5.12 | Pharmacy mail / print for TPN letter | **Gap** | — | No print or email integration |

## 6. Enteral Nutrition / Feeding

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 6.1 | Enteral feeding selection | **Fit** | Nutrition | EnteralFeeding category with generic selection |
| 6.2 | Supplement management (multiple) | **Fit** | Nutrition | EnteralSupplement category, multiple instances allowed |
| 6.3 | Safety: removing feed warns about dependent supplements | **Fit** | Nutrition | Confirmation dialog implemented |
| 6.4 | Frequency and dose per administration | **Fit** | Nutrition | Frequency + dose quantity controls for discontinuous/timed |
| 6.5 | Continuous enteral feeding (rate-based) | **Fit** | Nutrition | Continuous dose type with rate navigation |

## 7. Totals / Intake Calculation

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 7.1 | Fluid (ml/kg/day) | **Fit** | Totals | Volume tracked |
| 7.2 | Energy (kCal/kg/day) | **Fit** | Totals | Energy tracked |
| 7.3 | Glucose (g/kg/day and mg/kg/min) | **Partial** | Totals | Carbohydrate tracked; mg/kg/min display not confirmed |
| 7.4 | Protein (g/kg/day) | **Fit** | Totals | Protein tracked |
| 7.5 | Vitamin D (IE/day) | **Fit** | Totals | VitaminD tracked |
| 7.6 | Iron (mmol/kg/day) | **Fit** | Totals | Iron tracked |
| 7.7 | Na, K, Cl, Ca, Mg, PO₄ (mmol/kg/day) | **Fit** | Totals | All 6 electrolytes tracked |
| 7.8 | Age/weight-appropriate intake recommendations | **Partial** | Totals | Warning levels (Normal/Caution/Warning/Alert) present; unclear if full age-based reference ranges shown |
| 7.9 | Relevant lab values alongside totals | **Gap** | — | No lab data integration in GenPRES |
| 7.10 | Totals across ALL orders (nutrition + continuous + discontinuous) | **Partial** | Totals | Nutrition and TreatmentPlan each show own totals; unclear if cross-view aggregation happens |
| 7.11 | Fat (g/kg/day) | **Fit** | Totals | Fat tracked |

## 8. Cross-Cutting / Workflow

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 8.1 | Patient data management (demographics, weight, age) | **Fit** | All | Patient context with age, weight, BSA, GA, PMA, gender |
| 8.2 | Version control / save history | **Gap** | — | No patient-data versioning in GenPRES |
| 8.3 | Multi-user conflict detection (patient open elsewhere) | **Gap** | — | No concurrent access detection |
| 8.4 | MetaVision integration (data sync, signing) | **Gap** | — | No EHR integration |
| 8.5 | HIX medication import | **Gap** | — | No external system import |
| 8.6 | Pharmacy communication (email, print) | **Gap** | — | No print or email |
| 8.7 | Renal function warning + dosing adjustment link | **Partial** | Prescribe | Renal rules exist in GenFORM; GFR-based dose adjustment available; no lab-driven auto-calculation |
| 8.8 | Batch delete of selected orders | **Fit** | TreatmentPlan | Multi-select + batch delete implemented |
| 8.9 | PICU vs NICU workflow separation | **Gap** | — | GenPRES has single unified workflow; no unit-specific menus |

---

## Summary

| Status | Count | Percentage |
|---|---|---|
| **Fit** | 30 | 48% |
| **Partial** | 13 | 21% |
| **Gap** | 19 | 31% |
| **Total** | 62 | 100% |

### Strengths (Fit)

GenPRES covers the core prescribing workflow well: generic/indication/route/form selection, constraint-based dosing with min/max navigation, continuous and discontinuous dose types, multi-component orders, solution/preparation rules, severity-based monitoring, enteral + parenteral nutrition categories, and comprehensive intake totals (17 metrics including excipient warnings).

### Partial Fits (require enhancement)

- NICU-specific presets and syringe conventions
- Explicit CVL/peripheral toggle in Nutrition view
- Glucose display in mg/kg/min alongside g/kg/day
- Indication-based sorting in TreatmentPlan
- Direct links to external dosing references (G-Standaard, Kinderformularium)
- Informational (blue) severity level distinct from caution (yellow)

### Gaps (not yet implemented)

- **Workflow**: Infusion letter, 17:00 transfer cycle, pharmacy email/print, PICU/NICU separation
- **Clinical features**: Epidural protocols, PRN medication, TPN day progression, medication without dosing, TallMan lettering, comments/remarks
- **Integration**: MetaVision sync, HIX import, lab data display, version control, multi-user conflict detection
- **Validation**: Max 5 infusions enforcement, 24-hour infusion time warning, pump stand calculation
