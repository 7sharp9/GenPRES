# ADR-0004: UI Wireframes

**Date**: 2024-01-01
**Status**: Updated 2026-04-02 (see issue #270)

## Context

Early in the project, the user interface flows and screen layouts needed to be sketched to guide front-end development and communicate the intended user experience to stakeholders.

## Decision

Produce a set of wireframes that illustrate the main UI screens and navigation flows. The initial design (2024) has been superseded by the implemented SAFE Stack web application, which uses a persistent side-drawer navigation pattern rather than the originally envisioned simple medication-search flow.

## Consequences

- Developers have a shared reference for the current UI structure.
- The wireframes reflect the implemented application as of April 2026.
- Screen-specific designs for nutrition and order views are documented separately (see [ADR-0005](0005-ui-nutrition-view.md), [ADR-0006](0006-ui-order-view.md)).

---

These wireframes describe the main user interface of GenPRES, a clinical decision support system for medication prescribing built on the SAFE Stack (Saturn + Azure + Fable + Elmish). The application is a single-page responsive web app with a persistent side-drawer navigation.

---

## Overall Layout

```
+--+------------------------------------------------------+
|☰ | GenPRES — Emergency List    [🏥 Hospital] [🌐 NL] [Login]|
+--+------------------------------------------------------+
|  |                                                       |
|  |  [Patient Panel — visible on all pages except Settings]|
|  |  Age: 4 yr | Weight: 20 kg | Height: 105 cm          |
|  |  Department: ICU | Indication: Pain                   |
|  |                                                       |
|  +-------------------------------------------------------+
|  |                                                       |
|  |  [Page Content — changes per selected menu item]      |
|  |                                                       |
|  |                                                       |
+--+-------------------------------------------------------+
   |  [Totals/Summary Drawer — shown for Prescribe,        |
   |   Nutrition, and Treatment Plan pages]                |
   +-------------------------------------------------------+
```

---

## 1. Side Navigation Drawer

Activated by the hamburger icon (☰) in the top-left. On desktop, the drawer is persistent; on mobile, it slides over the content.

```
+---------------------------+
| ☰  GenPRES               |
+---------------------------+
| 🧯 Emergency List         |
| 💉 Continuous Meds List   |
| 🍽️ Nutrition               |
| ✉️ Prescribe               |
| 📋 Treatment Plan          |
| ⚠️ Interactions            |
| 💊 Formulary               |
| 🩸 Parenteralia            |
| ⚙️ Settings                |
+---------------------------+
```

---

## 2. Application Bar (TitleBar)

Displayed at the top of every page.

```
+----------------------------------------------------------+
| [☰]  GenPRES — {Current Page Title}  [🏥] {Hosp} [🌐] {lang} [Login] |
+----------------------------------------------------------+
```

- **☰** — toggle side navigation drawer
- **🏥** — hospital selector dropdown (populated from Google Sheets)
- **🌐 {lang}** — language selector (e.g., NL, EN)
- **Login** — login/logout button

---

## 3. Patient Panel

Shown persistently above the page content area on all pages except Settings. Displays the current patient's clinical context (populated by the clinician).

```
+----------------------------------------------------------+
| Patient: [____] yr [____] mo | [____] kg | [____] cm    |
| Dept: [Select ▼]  | Indication: [Select ▼]               |
+----------------------------------------------------------+
```

---

## 4. Emergency List (LifeSupport)

A pre-calculated list of emergency medications with dosages appropriate for the current patient.

```
+----------------------------------------------------------+
|  Emergency List                                          |
|  +------------------------------------------------------+|
|  | Medication     | Dose       | Route | Concentration  ||
|  |----------------|------------|-------|----------------||
|  | Adrenaline     | 0.01 mg/kg | IV    | 1 mg/10 ml     ||
|  | Atropine       | 0.02 mg/kg | IV    | 0.5 mg/ml      ||
|  | ...            |            |       |                ||
|  +------------------------------------------------------+|
|  [ Export ] [ Print ]                                    |
+----------------------------------------------------------+
```

---

## 5. Continuous Medication List (ContinuousMeds)

A list of continuous infusion medications with calculated rates for the current patient.

```
+----------------------------------------------------------+
|  Continuous Medication List                              |
|  +------------------------------------------------------+|
|  | Medication   | Concentration | Rate  | Dose/hr       ||
|  |--------------|---------------|-------|---------------||
|  | Morphine     | 1 mg/ml       | 2 ml/hr| 2 mg/hr      ||
|  | Midazolam    | 1 mg/ml       | 1 ml/hr| 1 mg/hr      ||
|  | ...          |               |       |               ||
|  +------------------------------------------------------+|
+----------------------------------------------------------+
```

---

## 6. Prescribe

Select a medication, choose route/form, enter order details, and view calculated dose constraints.

```
+----------------------------------------------------------+
|  Prescribe                                               |
|  Medication: [ Search/Select medication... ▼ ]           |
|  Route:      [ Select route ▼ ]                          |
|  Form:       [ Select form ▼  ]                          |
|                                                          |
|  +------------------------------------------------------+|
|  | Order Variables (editable)                           ||
|  | Dose:      [_____] mg/kg  (range: 10–15 mg/kg)      ||
|  | Frequency: [_____] /day   (2, 3, or 4 times)        ||
|  | Duration:  [_____] days                              ||
|  +------------------------------------------------------+|
|  [ Add to Treatment Plan ]                               |
+----------------------------------------------------------+
| [▼ Totals: Total intake: 80 mg/day | 4 mg/kg/day ]       |
+----------------------------------------------------------+
```

---

## 7. Nutrition

Manage enteral and parenteral nutrition orders with total intake calculations.

```
+----------------------------------------------------------+
|  Nutrition                                               |
|  [ Enteral ] [ Parenteral ]                              |
|  Product: [ Select nutrition product ▼ ]                 |
|  Rate:    [_____] ml/hr                                  |
|  ...                                                     |
+----------------------------------------------------------+
| [▼ Totals: Energy: 1200 kcal/day | Protein: 25 g/day ]   |
+----------------------------------------------------------+
```

---

## 8. Treatment Plan (OrderPlan)

Overview of all active medication orders for the current patient, with cumulative intake totals.

```
+----------------------------------------------------------+
|  Treatment Plan                                          |
|  +------------------------------------------------------+|
|  | Medication  | Dose      | Freq | Route | Actions     ||
|  |-------------|-----------|------|-------|-------------||
|  | Paracetamol | 15 mg/kg  | q6h  | Oral  | [Edit] [Del]||
|  | Morphine    | 0.1 mg/kg | q4h  | IV    | [Edit] [Del]||
|  +------------------------------------------------------+|
|  [ Add Medication ]                                      |
+----------------------------------------------------------+
| [▼ Totals: Combined intake summary ]                     |
+----------------------------------------------------------+
```

---

## 9. Interactions

Drug–drug interaction checker for medications in the current treatment plan.

```
+----------------------------------------------------------+
|  Interactions                                            |
|  +------------------------------------------------------+|
|  | ⚠️ Morphine + Midazolam: increased sedation risk      ||
|  | ℹ️ Paracetamol + Ibuprofen: monitor renal function    ||
|  +------------------------------------------------------+|
+----------------------------------------------------------+
```

---

## 10. Formulary

Browse the complete drug formulary with dosing guidelines.

```
+----------------------------------------------------------+
|  Formulary                                               |
|  Search: [ Enter drug name... ] [🔍]                     |
|  +------------------------------------------------------+|
|  | Paracetamol | Oral | 10–15 mg/kg q4–6h | ...        ||
|  | Ibuprofen   | Oral | 5–10 mg/kg q6–8h  | ...        ||
|  +------------------------------------------------------+|
+----------------------------------------------------------+
```

---

## 11. Parenteralia

Reference for parenteral preparation guidelines (concentrations, diluents, stability).

```
+----------------------------------------------------------+
|  Parenteralia                                            |
|  Search: [ Enter drug name... ] [🔍]                     |
|  +------------------------------------------------------+|
|  | Morphine | Max 1 mg/ml | NaCl 0.9% | Stable 24h     ||
|  | ...      |             |           |                 ||
|  +------------------------------------------------------+|
+----------------------------------------------------------+
```

---

## 12. Settings

Application configuration (language, hospital, display preferences). Patient panel is hidden on this page.

```
+----------------------------------------------------------+
|  Settings                                                |
|  Language:  [ Select ▼ ]                                 |
|  Hospital:  [ Select ▼ ]                                 |
|  Demo mode: [ On / Off ]                                 |
|  Version:   vX.Y.Z                                       |
+----------------------------------------------------------+
```

---

## Responsive Behaviour

| Viewport | Side Drawer | Patient Panel |
|----------|-------------|---------------|
| Desktop  | Persistent (always visible) | Inline above page |
| Mobile   | Overlay (toggled by ☰)      | Collapsed / summary |

---

## Notes

- Navigation is via the side drawer; there is no top navigation bar with tabs.
- The patient panel is always visible (except on Settings) and reflects patient context entered by the clinician.
- Medication data and dosing rules are loaded from the Dutch Z-index product database and Google Sheets respectively.
- All configuration (hospitals, indications, departments, language) comes from Google Sheets.
- The Totals/Summary area is a bottom panel shown on Prescribe, Nutrition, and Treatment Plan pages to display cumulative intake.
- The UI is built with Material-UI (MUI) components via Fable/Elmish.

