# Usability Validation Report

GenPRES Usability Validation Report — Version 0.1 (Pre-execution), May 2026

> **Status**: Usability validation is **planned but not yet executed**. This document records the validation framework, scope, and approach in advance of formal testing. It will be updated with test results following execution of the summative usability evaluation.

---

## 1. Purpose and Scope

This report documents the usability validation activities for GenPRES in accordance with **IEC 62366-1:2015** (Application of usability engineering to medical devices). The scope covers the intended use context — medication prescribing, TPN formulation, and clinical decision support alert handling — by healthcare professionals in paediatric and neonatal intensive care settings.

Usability validation confirms that the final user interface design does not introduce use errors that could lead to patient harm.

---

## 2. Regulatory Framework

| Standard | Applicability |
|---|---|
| IEC 62366-1:2015 | Primary usability engineering standard; defines the usability engineering process |
| MDR 2017/745 Annex I §5 | Requires demonstration that use errors are designed out or adequately mitigated |
| ISO 14971:2019 | Risk management; use errors identified via usability testing feed into the risk management file |

The usability engineering file for GenPRES comprises:

| Document | Location | Status |
|---|---|---|
| User profile | `docs/mdr/usability/user-profile.md` | ✅ Complete |
| Critical task analysis | `docs/mdr/usability/critical-tasks.md` | ✅ Complete |
| Formative testing plan | `docs/mdr/usability/formative-testing.md` | ✅ Complete |
| Summative testing plan | `docs/mdr/usability/summative-testing.md` | ✅ Complete |
| Summative test results | This document (§5) | ⏳ Pending execution |
| Residual risk assessment | `docs/mdr/risk-analysis/` | ✅ In progress |

---

## 3. Intended Users and Use Environment

Derived from `docs/mdr/usability/user-profile.md`. The three primary user groups are:

| User group | Role | Key needs |
|---|---|---|
| Paediatric ICU physicians | Prescribe complex medication regimens for critically ill children | Rapid, accurate dosage recommendations; interaction alerts; intuitive navigation under time pressure |
| Clinical pharmacists | Validate prescriptions, manage TPN formulations, ensure medication safety | Detailed drug information; accurate and editable TPN calculations; auditability |
| Paediatric ICU nurses | Administer medications, monitor response | Clear summaries of active orders; visibility into dose changes and alerts; cross-device access |

---

## 4. Critical Tasks

Seven critical tasks are identified in `docs/mdr/usability/critical-tasks.md`:

| # | Task | Patient-safety relevance |
|---|---|---|
| T1 | Access the application from an EPD URL with embedded patient context | Incorrect patient context → wrong-patient medication error |
| T2 | Prescribe medication: select drug, enter patient data, validate dose calculation, confirm | Dose calculation error could cause over- or under-dosing |
| T3 | Respond to safety alerts (dose-range violation, drug interaction) | Missed or misunderstood alert could lead to unsafe prescribing |
| T4 | Modify an active prescription | Incorrect modification could leave the wrong dose active |
| T5 | Calculate and order TPN | TPN formulation error could cause severe electrolyte imbalance |
| T6 | Log out and confirm session clearing | Residual session data could be misinterpreted as a current patient's data |
| T7 | Handle network interruption and resume | Data loss or corruption on reconnect could cause ordering errors |

---

## 5. Summative Usability Test Results

> **⏳ Pending**: Summative testing has not yet been executed. Results will be recorded in this section following test completion.

### 5.1 Test execution dates

| Activity | Planned | Actual |
|---|---|---|
| Participant recruitment | Q3 2026 | — |
| Test execution | Q3 2026 | — |
| Report completion | Q3 2026 | — |

### 5.2 Participants

Minimum 10 participants required per IEC 62366 recommendations; target distribution:

| User group | Target count |
|---|---|
| Paediatric ICU physicians | 4 |
| Clinical pharmacists | 3 |
| Paediatric ICU nurses | 3 |

### 5.3 Task performance results (to be completed)

| Task | Participants | Success rate | Mean completion time | Errors (critical) | Errors (non-critical) |
|---|---|---|---|---|---|
| T1 | — | — | — | — | — |
| T2 | — | — | — | — | — |
| T3 | — | — | — | — | — |
| T4 | — | — | — | — | — |
| T5 | — | — | — | — | — |
| T6 | — | — | — | — | — |
| T7 | — | — | — | — | — |

**Acceptance criterion**: ≥ 90% of participants complete each critical task successfully; no use errors resulting in potential patient harm.

### 5.4 System Usability Scale (SUS) score

| Metric | Target | Actual |
|---|---|---|
| Mean SUS score | ≥ 70 (acceptable) | — |
| Minimum individual score | ≥ 55 | — |

### 5.5 Findings and recommendations (to be completed)

*To be populated after test execution.*

### 5.6 Conclusion (to be completed)

*To be populated after test execution.*

---

## 6. Formative Testing Completed

Formative usability activities were conducted iteratively during development. Key design changes informed by informal formative feedback include:

| Change | Rationale |
|---|---|
| Responsive table layout (PR #235) | Improved legibility on tablet-sized screens; reduced horizontal scrolling |
| Colour-coded dose-check severity (PR #309) | Replaced text-only feedback with Visual/Caution/Warning/Alert colour bands to reduce cognitive load when scanning multiple medications |
| Localised UI strings — Dutch (PR #239) | Primary clinical environment is Dutch-speaking; hardcoded Dutch strings replaced with proper localisation support |
| Remember-filter functionality (PR #251) | Reduces repeated navigation steps for emergency list filtering in fast-paced ICU workflows |
| Admin authentication (PR #288) | Prevents accidental settings changes in shared-workstation environments |

These changes were validated through informal scenario walkthroughs with the development team. Formal moderated formative testing per the formative plan (`docs/mdr/usability/formative-testing.md`) is scheduled as part of W6 (Usability Engineering workshop, Q2 2026).

---

## 7. Open Issues and Residual Risks

| Issue | Risk level | Mitigation |
|---|---|---|
| Summative test not yet executed | High (required for regulatory sign-off) | Scheduled Q3 2026; interim controls: formative feedback and expert review |
| Mobile navigation not validated at scale | Medium | Responsive layout implemented; formal mobile validation to be included in summative test |
| TPN workflow complexity | Medium | TPN critical task (T5) given additional time allocation in test plan; expert pharmacist participants prioritised |

---

## 8. References

- IEC 62366-1:2015 — Usability engineering for medical devices
- User profile: `docs/mdr/usability/user-profile.md`
- Critical tasks: `docs/mdr/usability/critical-tasks.md`
- Formative test plan: `docs/mdr/usability/formative-testing.md`
- Summative test plan: `docs/mdr/usability/summative-testing.md`
- Risk management: `docs/mdr/risk-analysis/`
- ROADMAP: `ROADMAP.md` (W6 Usability Engineering milestone)