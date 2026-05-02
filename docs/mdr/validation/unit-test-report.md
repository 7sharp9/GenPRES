# Unit Test Report

GenPRES Unit Test Report — Version 1.0, May 2026

## 1. Summary

This report documents the automated unit and property-based test results for GenPRES as of May 2026. All tests are executed via the `dotnet run ServerTests` command using the Expecto 10.x test runner. The full server-side test suite contains over **5 400 tests** (as of the May 2026 baseline; see §4 for per-module breakdown).

## 2. Test Execution Environment

| Parameter | Value |
|-----------|-------|
| .NET SDK | 10.0 (via `global.json`) |
| Test runner | Expecto ~> 10 with YoloDev.Expecto.TestSdk 0.15.5 |
| Property tests | FsCheck (via Expecto.FsCheck), 1 000 cases/property |
| CI platforms | Ubuntu, Windows, macOS (GitHub Actions `build.yml`) |
| Environment variable | `GENPRES_DEBUG=1` (enables local resource cache for resource tests) |
| Fantomas format gate | Required to pass before tests run |

## 3. Pass/Fail Status

| Status | Count |
|--------|-------|
| ✅ Passed | ≥ 5 408 |
| ❌ Failed | 0 |
| ⚠️ Skipped | 0 |

> **Note**: The 5 408 baseline count was recorded during the security hardening work (ADR-0015, May 2026). The exact count changes as new tests are added. Run `dotnet run ServerTests` locally to obtain the current count.

## 4. Test Coverage by Module

### 4.1 GenSOLVER (`Informedica.GenSOLVER.Tests`)

Source: ~2 270 lines of test code

| Test Category | Description |
|---|---|
| Variable operations | `Variable.fs` — increment, min/max, value range arithmetic |
| Constraint propagation | `Solver.fs` — equation solving, `solveAll` correctness |
| Canon-key normalisation | `CanonKey` round-trip symmetry, hash stability (8 unit + 6 property tests) |
| LRU cache | Capacity, eviction order, thread-safety, O(1) get/put (FsCheck property tests) |
| LRU solver integration | `SessionSolver` correctness with 6 tests + 50-patient capacity benchmark |
| Cycle / stall detection | `LoopDetect.fsx` — 9 Expecto tests for `StateFingerprint` and `CycleDetector` |
| `removeBigRationalMultiples` | Semantics: keeps smallest representatives, removes integer multiples |

### 4.2 GenFORM (`Informedica.GenFORM.Tests`)

Source: ~1 217 lines of test code

| Test Category | Description |
|---|---|
| Dose-rule parsing | Validates CSV parsing against locally cached spreadsheet data |
| Constraint resolution | `PrescriptionRule.fs` — min/max dose adjustment, 131+ patient scenario cases |
| OnceTimed validation | Accepts `MaxRate`, `MaxRateAdj`, or `MaxTime`/`TimeUnit` as valid conditions |
| Component dose display | All `wrap` calls include base + adjustment dose fields |
| `useAdjust` checks | Substance, component, and form level checks |

### 4.3 GenORDER (`Informedica.GenORDER.Tests`)

Source: ~1 160 lines of test code + `Scenarios.fs`

| Test Category | Description |
|---|---|
| Order scenarios | `pcmSupp`, `amfo`, `morfCont`, `pcmDrink`, `cotrim`, `tpn`, `tpnComplete`, `fullMedication` |
| Pipeline correctness | Full Prescription → Preparation → Administration pipeline |
| TPN calculation | Total parenteral nutrition order composition |
| Staged value expansion | Two-phase `skipRate` expansion for `OnceTimed`/`Timed` orders |

### 4.4 GenUNITS (`Informedica.GenUNITS.Lib`)

Source: ~740 lines of test code

| Test Category | Description |
|---|---|
| Unit arithmetic | Addition, subtraction, multiplication, division with unit tracking |
| BigRational conversion | `toBigRational`, `fromFloat`, precision tests |
| `ValueUnit` operations | `singleWithUnit`, `withUnit`, base/unit conversions, `pickNearestHigherElseLower` |
| Unit group compatibility | `eqsGroup` checks for incompatible unit combinations |

### 4.5 Shared (`Informedica.GenPRES.Shared.Tests`)

| Test Category | Description |
|---|---|
| BSA formulas | Mosteller, Du Bois, Haycock, Gehan & George, Fujimoto — boundary values |
| eGFR formulas | CKD-EPI Creatinine 2021, CKD-EPI 2009, MDRD 4-variable, Bedside Schwartz |
| KDIGO classification | `Normal` through `KidneyFailure` GFR stages |
| Age calculations | Post-menstrual age, adjusted age, chronological age in days |

### 4.6 Server / Security (`Informedica.GenPRES.Server.Tests`)

| Test Category | Description |
|---|---|
| JSON security | `JsonSecurity` sub-module — verifies `TypeNameHandling.None` default is not reverted |
| Authentication | Token-based login/logout, constant-time password comparison |
| Endpoint smoke tests | Key server endpoints respond correctly under `CI=true` |

### 4.7 Other Libraries

| Library | Coverage Highlights |
|---------|---------------------|
| `Informedica.Utils.Tests` | FsCheck property tests for Array, List, String utilities |
| `Informedica.GenCORE.Tests` | Domain-model invariants with custom FsCheck generators |
| `Informedica.ZIndex.Tests` | G-Standard fixture loading, product and route lookups |
| `Informedica.ZForm.Tests` | ZForm dose-rule parsing and GStand integration |
| `Informedica.NKF.Tests` | NKF dose-rule parsing and lookup |
| `Informedica.FTK.Tests` | FTK dose-rule parsing |
| `Informedica.Agents.Tests` | `MailboxProcessor` agent lifecycle tests |
| `Informedica.Logging.Tests` | Concurrent logging utilities |
| `Informedica.MCP.Tests` | MCP stdio server tool registration and dispatch |
| `Informedica.FHIR.Tests` | FHIR R4 `MedicationRequest` translation tests (ADR-0020) |
| `Informedica.NLP.Tests` | NLP pipeline unit tests (LLM-independent portions) |
| `Informedica.OTS.Tests` | Google Sheets / OTS data access |
| `Informedica.HIXConnect.Tests` | HIX Connect integration |
| `Informedica.DataPlatform.Tests` | Data platform integration |

## 5. Known Limitations

1. **No formal coverage metrics**: Line and branch coverage are not currently collected. Adding coverage tooling (e.g., Coverlet) is tracked as a future improvement.
2. **Client-side code**: Fable/Elmish client code has no automated unit tests. It is covered by manual testing and the headless CI smoke tests (`dotnet run TestHeadless`).
3. **NLP extraction tests**: `DoseRuleTests.fsx` and `DoseRuleValidation.fsx` require a live LLM endpoint and are not included in the CI suite.
4. **ZIndex / ZForm**: Some tests require locally cached G-Standard CSV files. These files are not committed to the repository; tests gracefully skip or return empty results when the cache is absent.

## 6. Defect History

| PR | Area | Description |
|----|------|-------------|
| #149 | GenSOLVER | Messages=`[||]` root cause fixed; regression tests added |
| #188 | GenFORM | Three validation bugs fixed; regression suite expanded |
| #285 | GenSOLVER | `ValueSetOverflow` fixed with `MAX_CALC_COUNT` cap; ADR-0014 |
| #305 | Codebase | F# 8 modernisation — `_.Property` lambdas; modern indexers; confirmed by 5 408-test pass |

## 7. Next Steps

- Add Coverlet code-coverage collection to the CI pipeline and publish HTML reports
- Extend NLP tests to cover LLM-independent parsing logic within the CI suite
- Add client-side unit tests for critical Elmish update functions
- Formalise the usability validation report (`usability-validation-report.md`)

---

*Version: 1.0 | Date: May 2026 | Author: Repo Assist (AI) — subject to maintainer review*