# Getting Started with GenPRES

> ⚠️ **Clinical Disclaimer**: GenPRES is a Clinical Decision Support System (CDSS). It is **not** intended for direct clinical use without appropriate validation, regulatory approval, and institutional governance. Always apply independent clinical judgment. See [SUPPORT.md](../../SUPPORT.md#medical-advice-disclaimer).

---

## Prerequisites

Before starting, ensure you have the following installed:

- **.NET SDK** (see [DEVELOPMENT.md](../../DEVELOPMENT.md#toolchain-requirements) for supported versions)
- **Node.js** and **npm**

Refer to [`DEVELOPMENT.md`](../../DEVELOPMENT.md) for full environment setup instructions, including the `GENPRES_URL_ID` environment variable needed for live data.

---

## Starting the Application

```bash
dotnet run
```

Open your browser to **<http://localhost:5173>**.

> The demo cache included in the repository is loaded automatically when `GENPRES_URL_ID` is not set. This provides a subset of sample medication data sufficient for testing.

---

## Accessing the Application Without Patient Data

You can open the application at <http://localhost:5173> **without any URL parameters**. In this mode:

- The **Patient** panel expands automatically and displays *"Voer patiënt gegevens in"* (Enter patient data).
- Medication calculations are **not available** until weight and height are provided.
- You can still explore the interface, view the formulary, and navigate pages.

To use all features, enter patient data either:

1. **Directly in the UI** (see [Entering Patient Data Manually](#entering-patient-data-manually)), or
2. **Via URL parameters** (see [URL Parameters](#url-parameters)).

---

## Entering Patient Data Manually

1. Open <http://localhost:5173>.
2. The **Patient** accordion is expanded. Fill in the following fields:
   - **Birth date** (year, month, day) — used to calculate age
   - **Weight** (in kg) — required to enable dose calculations
   - **Height** (in cm) — required to enable dose calculations
   - **Department** — optional; filters applicable protocols
   - **Gestational age** — optional; relevant for neonates
   - **Central venous line** — optional; affects available routes
3. Once **weight** and **height** are set, the Patient panel collapses automatically and dose calculations become available.

---

## URL Parameters

GenPRES supports **hash-based URL routing** for integration with Electronic Patient Record (EPD) systems. Patient context is passed via query parameters after the hash fragment.

**URL format:**

```
http://localhost:5173/#patient?<param1>=<value1>&<param2>=<value2>
```

### Patient Parameters

| Parameter | Description | Example | Notes |
|-----------|-------------|---------|-------|
| `by` | Birth year | `2010` | Required (or use `ad`) |
| `bm` | Birth month | `6` | Optional; 1–12, default: `1` |
| `bd` | Birth day | `15` | Optional; 1–31, default: `1` |
| `ad` | Age in days | `365` | Alternative to `by`/`bm`/`bd` |
| `wt` | Weight in grams | `25000` | Optional; 25000 = 25 kg |
| `ht` | Height in cm | `130` | Optional |
| `gw` | Gestational age (weeks) | `40` | Optional; for neonates |
| `gd` | Gestational age (days) | `0` | Optional |
| `cv` | Central venous line | `y` | Optional; `y` = yes |
| `dp` | Department | `PICU` | Optional; free text |

### Page / View Parameters

| Parameter | Value | View |
|-----------|-------|------|
| `pg` | `el` | Emergency list |
| `pg` | `cm` | Continuous medications |
| `pg` | `pr` | Prescribe |
| `pg` | `fm` | Formulary |
| `pg` | `pe` | Parenteralia |

### Medication Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `md` | Medication name | `paracetamol` |
| `rt` | Route | `intravenous` |
| `fr` | Form | `infusion fluid` |
| `in` | Indication | — |
| `dt` | Dose type | — |

### Other Parameters

| Parameter | Description | Notes |
|-----------|-------------|-------|
| `la` | Language | `en` (English), `du` (Dutch), `fr` (French) |
| `dc` | Show disclaimer | `n` = suppress disclaimer on load |

### Example URLs

**Child patient, prescribing view:**

```
http://localhost:5173/#patient?by=2015&bm=3&bd=10&wt=20000&ht=115&pg=pr
```

**Neonate by age in days, emergency list:**

```
http://localhost:5173/#patient?ad=7&wt=3200&gw=39&gd=2&pg=el
```

**Adult patient, continuous medications, English UI:**

```
http://localhost:5173/#patient?by=1990&bm=1&bd=1&wt=70000&ht=175&pg=cm&la=en&dc=n
```

---

## Navigating the Application

Once patient data is entered, the main views are available via the page selector:

| View | Description |
|------|-------------|
| **Emergency list** (`el`) | Quick-access dosing for emergency medications |
| **Continuous medications** (`cm`) | Continuous infusion pump calculations |
| **Prescribe** (`pr`) | Full medication prescribing with dose calculation and validation |
| **Formulary** (`fm`) | Browse available medications and their constraints |
| **Parenteralia** (`pe`) | Parenteral (IV) preparation information |

For a full walkthrough with screenshots, see the [external user guides](README.md#external-user-guides).

---

## Demo Mode vs. Live Mode

| Mode | Configuration | Data |
|------|--------------|------|
| **Demo** (default) | `GENPRES_PROD=0` or unset | Sample medication data bundled with repository |
| **Live** | `GENPRES_PROD=1` and valid `GENPRES_URL_ID` | Full medication formulary from Google Sheets |

In demo mode the formulary is limited to a representative subset. It is sufficient for testing the application UI and calculation logic but does not reflect the complete clinical rule set.

---

## Next Steps

- [Testing Workflows](testing-workflows.md) — reproducible QA procedures including unit conversion testing
- [External User Guides](README.md#external-user-guides) — full functional walkthroughs with animated screenshots
- [DEVELOPMENT.md](../../DEVELOPMENT.md) — full developer environment setup
