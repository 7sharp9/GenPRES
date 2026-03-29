# ADR-0004: UI Wireframes

**Date**: 2024-01-01
**Status**: Accepted

## Context

Early in the project, the user interface flows and screen layouts needed to be sketched to guide front-end development and communicate the intended user experience to stakeholders.

## Decision

Produce a set of ASCII wireframes that illustrate the main UI screens: landing page, medication lookup, patient data entry, dosage results, order history, and protocols. These wireframes serve as an initial design baseline.

## Consequences

- Developers and designers have a shared reference for the intended UI structure.
- The wireframes are illustrative and may diverge from the final implementation as development progresses.
- Detailed screen-specific designs are documented separately (see [ADR-0005](0005-ui-nutrition-view.md), [ADR-0006](0006-ui-order-view.md)).

---

These wireframes describe the main user interface flows for GenPRES, a clinical decision support system for medication prescribing. The application is structured as a modern web app, with a focus on medication lookup, patient data entry, medication order calculation, and result display.

---

## 1. Home/Landing Page

```
+------------------------------------------------------+
| [GenPRES Logo]   | [Home] [Medications] [Protocols]  |
+------------------------------------------------------+
|                                                      |
|   Welcome to GenPRES                                 |
|   [ Search medications... ] [🔍]                      |
|                                                      |
|   [ Quick links: ]                                   |
|   - Start New Prescription                           |
|   - View Saved Orders                                |
|   - Help / About                                     |
|                                                      |
+------------------------------------------------------+
| [Footer: Version | Contact | Legal | Language ]      |
+------------------------------------------------------+
```

---

## 2. Medication Lookup

```
+------------------------------------------------------+
| [GenPRES Logo]   | [Home] [Medications] [Protocols]  |
+------------------------------------------------------+
| Medication Search: [ Acetaminophen           ][🔍]   |
+------------------------------------------------------+
| Results:                                            |
| +-----------------+  +-----------------+            |
| | Paracetamol     |  | Ibuprofen       |            |
| +-----------------+  +-----------------+            |
| | Morphine        |  | Amoxicillin     |            |
| +-----------------+  +-----------------+            |
|                                                      |
| [Select a medication to continue]                    |
+------------------------------------------------------+
```

---

## 3. Patient Data Entry

```
+------------------------------------------------------+
| [GenPRES Logo]   | [Home] [Medications] [Protocols]  |
+------------------------------------------------------+
| Medication: Paracetamol                              |
+------------------------------------------------------+
| Patient Information:                                 |
|   - Age:      [____] years  [____] months           |
|   - Weight:   [____] kg                             |
|   - Height:   [____] cm                             |
|   - Department: [Select ▼]                          |
|   - Indication:  [Select ▼]                         |
|                                                      |
| [ Calculate Dosage ]                                |
+------------------------------------------------------+
```

---

## 4. Dosage Calculation & Results

```
+------------------------------------------------------+
| [GenPRES Logo]   | [Home] [Medications] [Protocols]  |
+------------------------------------------------------+
| Medication: Paracetamol for Pain Relief              |
| Patient: 4 years | 20 kg | Pediatrics               |
+------------------------------------------------------+
| Recommended Dosage:                                 |
|  +------------------------------------------------+ |
|  |  Dose: 15 mg/kg every 6 hours                  | |
|  |  Route: Oral                                   | |
|  |  Max daily dose: 60 mg/kg                      | |
|  +------------------------------------------------+ |
|                                                      |
| [ Save Order ] [ Print ] [ New Calculation ]         |
+------------------------------------------------------+
```

---

## 5. Order and History Management

```
+------------------------------------------------------+
| [GenPRES Logo]   | [Home] [Medications] [Protocols]  |
+------------------------------------------------------+
| Your Saved Orders:                                   |
|  +----------------------------------------------+    |
|  | Paracetamol | Child | 20 kg | 2025-05-27     |    |
|  | Ibuprofen   | Adult | 80 kg | 2025-05-25     |    |
|  +----------------------------------------------+    |
|                                                      |
| [ View Details ] [ Delete ] [ Export ]               |
+------------------------------------------------------+
```

---

## 6. Protocols & Additional Resources

```
+------------------------------------------------------+
| [GenPRES Logo]   | [Home] [Medications] [Protocols]  |
+------------------------------------------------------+
| Protocols:                                          |
|  - Asthma Acute                                    |
|  - Sepsis Pediatric                                |
|  - Burn Surface Area                               |
| [ Select to view protocol details... ]              |
+------------------------------------------------------+
```

---

## 7. Error/Info Modals

```
+------------------------------------------------------+
| [!] Please enter all required patient data           |
| [ OK ]                                              |
+------------------------------------------------------+
```

---

## Notes

- The left/top navigation bar gives fast access to main sections.
- Medication data and results are fetched live from the server or cached sources.
- All configuration (departments, indications) are selectable from dropdowns and reflect Google Sheets data.
- The UI is responsive for tablets and smartphones.

---

> These wireframes are based on repository structure, README details, and standard clinical workflow for medication order support. Adapt as needed for final visual or interactive prototypes.
