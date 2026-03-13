# GenPRES User Guide (English)

> **⚠️ Medical Disclaimer**  
> GenPRES is not intended for direct clinical use without appropriate validation and regulatory approval.  
> See [SUPPORT.md](../../../SUPPORT.md) for the full disclaimer.

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Accessing the Application](#2-accessing-the-application)
3. [Basic Navigation](#3-basic-navigation)
4. [Prescribing Medication](#4-prescribing-medication)
5. [Emergency List and Infusion Pumps](#5-emergency-list-and-infusion-pumps)
6. [Testing Without Patient Data](#6-testing-without-patient-data)
7. [Unit Conversion Testing](#7-unit-conversion-testing)
8. [Common Use Cases](#8-common-use-cases)
9. [Troubleshooting](#9-troubleshooting)

---

## 1. Introduction

GenPRES (Generic Prescribing System) is an open-source Clinical Decision Support System (CDSS) designed to assist clinical staff in:

- Looking up evidence-based dosing rules and constraints
- Performing safe medication calculations
- Verifying the correct application of clinical protocols

GenPRES targets pediatric and neonatal intensive care settings, but can be adapted to any medical environment.

The live system runs at <http://genpres.nl>.

---

## 2. Accessing the Application

### With Patient Data (EPD Integration)

In a clinical setting GenPRES is typically launched from an Electronic Patient Dossier (EPD) with patient parameters pre-filled in the URL query string, for example:

```
http://genpres.nl?age=2&weight=12&gender=male
```

Supported query parameters include:

| Parameter | Description | Example |
|-----------|-------------|---------|
| `age`     | Patient age in years | `age=2` |
| `weight`  | Body weight in kg | `weight=12` |
| `gender`  | `male` or `female` | `gender=male` |
| `height`  | Body height in cm | `height=90` |

### Without Patient Data (Demo / Testing)

The application can be used **without patient data** in the query string. Open the application directly:

```
http://localhost:5173
```

or on the production server:

```
http://genpres.nl
```

When no patient context is provided the application starts in demo mode. You can enter patient details manually in the interface before selecting a medication.

---

## 3. Basic Navigation

After opening the application you will see the main screen divided into functional areas:

### Patient Panel (top)

Displays patient parameters (age, weight, gender, height). If these are not provided via the URL, you can enter them manually here.

### Medication Search (main area)

Use the search field to find medications by:
- Generic name (e.g., *paracetamol*, *morphine*)
- ATC code

### Medication List

Select a medication from the search results to open the dosing panel.

### Dosing Panel

Shows the calculated dose range based on the patient parameters and the selected dosing protocol. Fields include:

- **Dose per kg** – weight-adjusted dose
- **Total dose** – calculated absolute dose
- **Frequency** – number of doses per day
- **Route** – administration route (oral, IV, etc.)
- **Concentration / Volume** – for infusion preparations

---

## 4. Prescribing Medication

### Step-by-step workflow

1. **Enter patient details** (age, weight, gender) in the patient panel.
2. **Search for a medication** by typing the generic name or ATC code in the search field.
3. **Select the medication** from the list of results.
4. **Review the dose range** shown in the dosing panel. The system highlights values that are outside safe limits.
5. **Adjust dose or frequency** if clinically indicated. The system will warn you if the entered value exceeds maximum or minimum limits.
6. **Select the administration route** (oral, IV, rectal, etc.).
7. **Confirm the prescription** and transfer the details to the EPD or print/export as required.

### Safety alerts

GenPRES displays colour-coded alerts:

| Colour | Meaning |
|--------|---------|
| 🟢 Green | Value within safe range |
| 🟡 Yellow | Value at the boundary of the safe range – use with caution |
| 🔴 Red | Value outside safe range – review before proceeding |

---

## 5. Emergency List and Infusion Pumps

The emergency list provides quick access to standard infusion pump settings for critical medications (e.g., adrenaline, dopamine, noradrenaline). It is designed for use in emergency and ICU scenarios.

### Opening the Emergency List

1. Open the application.
2. Navigate to **Emergency** or **Noodlijst** in the main menu.
3. Enter or confirm the patient's weight.
4. The system generates the standard infusion concentrations and pump rates for each medication.

### Standard Infusion Pumps

Each entry on the emergency list shows:

- **Medication name**
- **Recommended concentration** (e.g., 1 mg/mL)
- **Starting dose** (µg/kg/min or mL/h)
- **Dose range** (minimum – maximum)

---

## 6. Testing Without Patient Data

You can run a complete end-to-end workflow without real patient data, which is useful for:

- Developer onboarding
- QA testing
- Training and demonstrations

### Procedure

1. Start the application locally:

   ```bash
   dotnet run
   ```

   Open <http://localhost:5173> in your browser.

2. Leave the URL query string empty (no `?age=...` parameters).

3. On the main screen, **manually enter test patient data**:
   - Age: e.g., `2` years
   - Weight: e.g., `12` kg
   - Gender: `Male`

4. Search for a medication, e.g., `paracetamol`.

5. Review the calculated dosing information.

6. Optionally adjust dose values and observe safety alerts.

### Demo cache

The repository contains a demo cache file with sample medication data. This is sufficient for all testing workflows above. No live internet connection or proprietary data files are required.

---

## 7. Unit Conversion Testing

GenPRES internally uses `BigRational` arithmetic for exact unit-safe calculations via **Informedica.GenUNITS.Lib**. The following procedure lets you verify unit conversions in the UI.

### Verifying dose units

1. Select a medication with a known dose (e.g., *paracetamol* oral).
2. Observe the **dose per kg** field — it should show the value in `mg/kg`.
3. Change the patient weight and confirm that the **total dose** field updates correspondingly.

### Verifying infusion concentrations

1. Select an IV medication (e.g., *morphine*).
2. Observe the **concentration** field (mg/mL) and the **rate** field (mL/h).
3. Modify the desired dose and confirm the pump rate recalculates correctly.

### Example: Paracetamol oral

| Patient weight | Dose/kg | Expected total dose |
|---------------|---------|-------------------|
| 10 kg | 15 mg/kg | 150 mg |
| 20 kg | 15 mg/kg | 300 mg |
| 30 kg | 15 mg/kg | 450 mg |

---

## 8. Common Use Cases

### Use case 1: Oral paracetamol for a toddler

1. Enter: age `2` years, weight `12` kg, gender `Male`.
2. Search: `paracetamol`.
3. Select **Paracetamol – oral**.
4. Observe the recommended dose range (typically 10–15 mg/kg, 4–6 times daily).
5. Confirm the maximum daily dose is not exceeded.

### Use case 2: IV morphine infusion for a child

1. Enter: age `5` years, weight `20` kg, gender `Female`.
2. Search: `morphine`.
3. Select **Morphine – IV continuous infusion**.
4. Observe the starting dose (e.g., 10–40 µg/kg/h) and the calculated pump rate.
5. Adjust the dose; confirm the rate updates.

### Use case 3: TPN calculation

1. Enter patient parameters (weight, age).
2. Navigate to **TPN** in the main menu.
3. Review the auto-generated macronutrient and micronutrient formula.
4. Adjust individual components if clinically indicated.
5. Export or print the TPN order for pharmacy.

---

## 9. Troubleshooting

### Application does not start

- Make sure you have the required prerequisites installed (.NET SDK, Node.js, npm). See [DEVELOPMENT.md](../../../DEVELOPMENT.md#toolchain-requirements).
- Run `dotnet run` from the repository root.
- Check that port `5173` is not occupied by another process.

### No medication data shown

- The application requires a cache file. The demo cache (`*.demo`) included in the repository is sufficient for testing.
- Ensure the `GENPRES_PROD` environment variable is set to `0` (demo mode). See [DEVELOPMENT.md](../../../DEVELOPMENT.md#environment-configuration).

### Dose values appear incorrect

- Verify that patient weight and age are entered correctly.
- Check whether the correct administration route is selected.
- Review the safety colour coding — a red alert indicates a value outside the permitted range.

### Further help

- GitHub Issues: <https://github.com/informedica/GenPRES/issues>
- Slack workspace: <https://genpresworkspace.slack.com>

---

*Version: 1.0 — March 2026*  
*Language: English*  
*[🇳🇱 Nederlandse versie](../nl/gebruikershandleiding.md)*
