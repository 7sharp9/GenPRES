/// # FHIR Medication Interface Implementation Plan
///
/// This script implements the plan described in docs/mdr/interface/genpres_interface_specification.md
/// and follows the steps outlined in the GenPRES FHIR integration issue.
///
/// ## Key principle
///
/// A FHIR scenario provides only:
///   1. Patient data (weight, height, age, gender)
///   2. Indication
///   3. Route
///   4. Shape (pharmaceutical form)
///   5. Dose type
///   6. Component Orderable Quantities (from the products block)
///   7. Orderable Dose Quantity and/or Rate (from administration + schema)
///   8. Schedule Frequency (from schema)
///
/// Everything else (concentrations, dose limits) is derived by lookup
/// via ZIndex/GenFORM or by calculation.
///
/// ## Steps
///
/// 1. Define the FHIR scenarios from the specification
/// 2. Look up product information from GPK product codes via ZIndex
/// 3. Parse quantitative variables to ValueUnits
/// 4. Reconstruct an OrderScenario from the scenario context via OrderContext lookup
/// 5. Apply orderable quantities and schedule from the FHIR scenario
/// 6. Run the scenarios through the order pipeline
/// 7. Print the results
/// 8. Sketch a FHIR-based solution

#load "load.fsx"

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore

open System
open MathNet.Numerics
open Hl7.Fhir
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


// =============================================================================
// STEP 1: Define the FHIR scenarios from the specification
// =============================================================================
//
// Source: docs/mdr/interface/genpres_interface_specification.md, sections 6.1–6.11
//
// NOTE: The GPK codes in the specification are PLACEHOLDERS (e.g. "2345678").
// Real codes must come from the G-Standard database. See Step 2 for product lookup.

/// Represents a single product component as described in the FHIR scenario YAML products block
type ScenarioProduct =
    {
        // Placeholder GPK code from the spec (not a real G-Standard code)
        GpkPlaceholder: string
        // Orderable quantity of this component
        Quantity: decimal
        // Unit for the orderable quantity (e.g. "mL", "stuk")
        Unit: string
        // Human-readable description from the spec comment
        Description: string
    }


/// Represents the administration schedule from the FHIR YAML schema block.
/// Only contains what is directly present in the FHIR scenario —
/// concentrations and dose limits are NOT included here.
type AdministrationSchema =
    {
        // Number of administrations per period (None for continuous orders)
        Frequency: int option
        // Duration of the frequency period (e.g. 1 for "per day", 36 for "per 36 hours")
        TimePeriod: decimal option
        // Unit of the frequency period (e.g. "dag", "uur")
        TimeUnit: string option
        // Infusion rate quantity (e.g. 85 for "85 mL/uur")
        RateQuantity: decimal option
        // Rate form unit — the numerator unit (e.g. "mL" for "mL/uur")
        RateFormUnit: string option
        // Rate time unit — the denominator unit (e.g. "uur" for "mL/uur")
        RateTimeUnit: string option
        // Exact administration times (for Timed orders)
        ExactTimes: string list
    }


/// Represents a FHIR treatment scenario as described in section 6 of the specification.
/// Contains only the data that can be directly derived from the FHIR resource —
/// specifically: patient context, indication, route, shape, dose type,
/// orderable quantities, and schedule.
type FhirScenario =
    {
        // Scenario identifier (matches the spec section number)
        ScenarioId: string
        // Short descriptive name
        Description: string
        // Patient weight in kg
        WeightKg: decimal
        // Patient height in cm
        HeightCm: decimal
        // Patient gender: "male" or "female"
        Gender: string
        // Clinical indication (maps to Filter.Indication)
        Indication: string
        // Generic medication name (maps to Filter.Generic)
        MedicationName: string
        // Administration route, Dutch G-Standard term (maps to Filter.Route)
        Route: string
        // Pharmaceutical form, Dutch (maps to Filter.Form)
        Shape: string
        // Dose type: "Once", "OnceTimed", "Discontinuous", "Timed", "Continuous"
        // (maps to Filter.DoseType)
        DoseType: string
        // Component orderable quantities from the YAML products block
        Products: ScenarioProduct list
        // Dose administration quantity and unit
        AdminQuantity: decimal
        // Dose administration form unit
        AdminUnit: string
        // Schedule from the YAML schema block
        Schema: AdministrationSchema
    }


// --- Scenario 6.1: Single-Product Once (Paracetamol rectal suppository) ---
let scenario61 =
    {
        ScenarioId = "6.1"
        Description = "Single-Product Once – paracetamol suppository"
        WeightKg = 19.5m
        HeightCm = 109m
        Gender = "male"
        Indication = "Pijn, acuut/post-operatief"
        MedicationName = "paracetamol"
        Route = "RECTAAL"
        Shape = "zetpil"
        DoseType = "Once"
        Products =
            [
                {
                    GpkPlaceholder = "2345678"
                    Quantity = 1m
                    Unit = "stuk"
                    Description = "paracetamol 750 mg/stuk zetpil (placeholder GPK)"
                }
            ]
        AdminQuantity = 1m
        AdminUnit = "stuk"
        Schema =
            {
                Frequency = None
                TimePeriod = None
                TimeUnit = None
                RateQuantity = None
                RateFormUnit = None
                RateTimeUnit = None
                ExactTimes = []
            }
    }


// --- Scenario 6.2: Single-Product Once-Timed (Paracetamol IV) ---
let scenario62 =
    {
        ScenarioId = "6.2"
        Description = "Single-Product OnceTimed – paracetamol IV infusion"
        WeightKg = 11m
        HeightCm = 79m
        Gender = "male"
        Indication = "Pijn, acuut/post-operatief"
        MedicationName = "paracetamol"
        Route = "INTRAVENEUS"
        Shape = "infusievloeistof"
        DoseType = "OnceTimed"
        Products =
            [
                {
                    GpkPlaceholder = "3456789"
                    Quantity = 22m
                    Unit = "mL"
                    Description = "paracetamol 10 mg/mL infusievloeistof (placeholder GPK)"
                }
            ]
        AdminQuantity = 22m
        AdminUnit = "mL"
        Schema =
            {
                Frequency = None
                TimePeriod = None
                TimeUnit = None
                RateQuantity = Some 85m
                RateFormUnit = Some "mL"
                RateTimeUnit = Some "uur"
                ExactTimes = []
            }
    }


// --- Scenario 6.3: Single-Product Discontinuous (Paracetamol rectal, 4x/day) ---
let scenario63 =
    {
        ScenarioId = "6.3"
        Description = "Single-Product Discontinuous – paracetamol rectal 4x/dag"
        WeightKg = 11m
        HeightCm = 79m
        Gender = "male"
        Indication = "Milde tot matige pijn; koorts"
        MedicationName = "paracetamol"
        Route = "RECTAAL"
        Shape = "zetpil"
        DoseType = "Discontinuous"
        Products =
            [
                {
                    GpkPlaceholder = "2345678"
                    Quantity = 1m
                    Unit = "stuk"
                    Description = "paracetamol 180 mg/stuk zetpil (placeholder GPK)"
                }
            ]
        AdminQuantity = 1m
        AdminUnit = "stuk"
        Schema =
            {
                Frequency = Some 4
                TimePeriod = Some 1m
                TimeUnit = Some "dag"
                RateQuantity = None
                RateFormUnit = None
                RateTimeUnit = None
                ExactTimes = []
            }
    }


// --- Scenario 6.3.1: Single-Product Discontinuous Specific Time (Gentamicine neonatal) ---
let scenario631 =
    {
        ScenarioId = "6.3.1"
        Description = "Single-Product Discontinuous – gentamicine 1x/36h neonatal"
        WeightKg = 1.8m
        HeightCm = 42m
        Gender = "female"
        Indication = "Ernstige infectie, gram negatieve microorganismen"
        MedicationName = "gentamicine"
        Route = "INTRAVENEUS"
        Shape = "infusievloeistof"
        DoseType = "Discontinuous"
        Products =
            [
                {
                    GpkPlaceholder = "2345678"
                    Quantity = 9m
                    Unit = "mL"
                    Description = "gentamicine 1 mg/mL infusievloeistof (placeholder GPK)"
                }
            ]
        AdminQuantity = 9m
        AdminUnit = "mL"
        Schema =
            {
                Frequency = Some 1
                TimePeriod = Some 36m
                TimeUnit = Some "uur"
                RateQuantity = None
                RateFormUnit = None
                RateTimeUnit = None
                ExactTimes = []
            }
    }


// --- Scenario 6.4: Single-Product Timed (Paracetamol IV with exact times) ---
let scenario64 =
    {
        ScenarioId = "6.4"
        Description = "Single-Product Timed – paracetamol IV 4x/dag with exact times"
        WeightKg = 11m
        HeightCm = 79m
        Gender = "male"
        Indication = "Pijn, acuut/post-operatief"
        MedicationName = "paracetamol"
        Route = "INTRAVENEUS"
        Shape = "infusievloeistof"
        DoseType = "Timed"
        Products =
            [
                {
                    GpkPlaceholder = "2345678"
                    Quantity = 17.5m
                    Unit = "mL"
                    Description = "paracetamol 10 mg/mL infusievloeistof (placeholder GPK)"
                }
            ]
        AdminQuantity = 17.5m
        AdminUnit = "mL"
        Schema =
            {
                Frequency = Some 4
                TimePeriod = Some 1m
                TimeUnit = Some "dag"
                RateQuantity = Some 70m
                RateFormUnit = Some "mL"
                RateTimeUnit = Some "uur"
                ExactTimes = [ "10:00"; "16:00"; "22:00"; "04:00" ]
            }
    }


// --- Scenario 6.5: Single-Product Continuous (Propofol sedation) ---
let scenario65 =
    {
        ScenarioId = "6.5"
        Description = "Single-Product Continuous – propofol sedation"
        WeightKg = 11m
        HeightCm = 79m
        Gender = "male"
        Indication = "Sedatie"
        MedicationName = "propofol"
        Route = "INTRAVENEUS"
        Shape = "emulsie voor injectie"
        DoseType = "Continuous"
        Products =
            [
                {
                    GpkPlaceholder = "2345678"
                    Quantity = 50m
                    Unit = "mL"
                    Description = "propofol 10 mg/mL emulsie voor injectie (placeholder GPK)"
                }
            ]
        AdminQuantity = 50m
        AdminUnit = "mL"
        Schema =
            {
                Frequency = None
                TimePeriod = None
                TimeUnit = None
                RateQuantity = Some 3.3m
                RateFormUnit = Some "mL"
                RateTimeUnit = Some "uur"
                ExactTimes = []
            }
    }


// --- Scenario 6.6: Multi-Product Continuous (Noradrenaline + glucose 10%) ---
let scenario66 =
    {
        ScenarioId = "6.6"
        Description = "Multi-Product Continuous – noradrenaline with glucose 10% diluent"
        WeightKg = 11m
        HeightCm = 79m
        Gender = "male"
        Indication = "Circulatoire insufficiëntie"
        MedicationName = "noradrenaline"
        Route = "INTRAVENEUS"
        Shape = "concentraat voor oplossing voor infusie"
        DoseType = "Continuous"
        Products =
            [
                {
                    GpkPlaceholder = "2345678"
                    Quantity = 5m
                    Unit = "mL"
                    Description = "noradrenaline 1 mg/mL concentraat voor oplossing voor infusie (placeholder GPK)"
                }
                {
                    GpkPlaceholder = "3456789"
                    Quantity = 45m
                    Unit = "mL"
                    Description = "glucose 10% vloeistof/diluent (placeholder GPK)"
                }
            ]
        AdminQuantity = 50m
        AdminUnit = "mL"
        Schema =
            {
                Frequency = None
                TimePeriod = None
                TimeUnit = None
                RateQuantity = Some 1m
                RateFormUnit = Some "mL"
                RateTimeUnit = Some "uur"
                ExactTimes = []
            }
    }


// --- Scenario 6.7: Multi-Product Once-Timed (Amiodaron + glucose 10% diluent) ---
let scenario67 =
    {
        ScenarioId = "6.7"
        Description = "Multi-Product OnceTimed – amiodaron with glucose 10% diluent"
        WeightKg = 11m
        HeightCm = 79m
        Gender = "male"
        Indication = "Ernstige therapieresistente hartritmestoornissen"
        MedicationName = "amiodaron"
        Route = "INTRAVENEUS"
        Shape = "injectievloeistof"
        DoseType = "OnceTimed"
        Products =
            [
                {
                    GpkPlaceholder = "2345678"
                    Quantity = 6m
                    Unit = "mL"
                    Description = "amiodaron 50 mg/mL injectievloeistof (placeholder GPK)"
                }
                {
                    GpkPlaceholder = "3456789"
                    Quantity = 44m
                    Unit = "mL"
                    Description = "glucose 10% vloeistof/diluent (placeholder GPK)"
                }
            ]
        AdminQuantity = 9.1m
        AdminUnit = "mL"
        Schema =
            {
                Frequency = Some 1
                TimePeriod = None
                TimeUnit = None
                RateQuantity = Some 18m
                RateFormUnit = Some "mL"
                RateTimeUnit = Some "uur"
                ExactTimes = []
            }
    }


// --- Scenario 6.8: Multi-Product with Reconstitution Once (Adrenaline) ---
// Products listed are AFTER reconstitution; the reconstitution block is in the YAML.
let scenario68 =
    {
        ScenarioId = "6.8"
        Description = "Multi-Product Reconstitution Once – adrenaline (reconstituted)"
        WeightKg = 3.9m
        HeightCm = 54m
        Gender = "male"
        Indication = "Reanimatie"
        MedicationName = "adrenaline"
        Route = "INTRAVENEUS"
        Shape = "injectievloeistof"
        DoseType = "Once"
        Products =
            [
                {
                    GpkPlaceholder = "2345678"
                    Quantity = 10m
                    Unit = "mL"
                    Description = "adrenaline 0,1 mg/mL oplossing voor infusie (na reconstitutie; placeholder GPK)"
                }
            ]
        AdminQuantity = 0.4m
        AdminUnit = "mL"
        Schema =
            {
                Frequency = Some 1
                TimePeriod = None
                TimeUnit = None
                RateQuantity = None
                RateFormUnit = None
                RateTimeUnit = None
                ExactTimes = []
            }
    }


// --- Scenario 6.9: Multi-Product with Reconstitution Timed (Vancomycine + glucose 5%) ---
let scenario69 =
    {
        ScenarioId = "6.9"
        Description = "Multi-Product Reconstitution Timed – vancomycine 4x/dag"
        WeightKg = 11m
        HeightCm = 79m
        Gender = "male"
        Indication = "Bacteriële infecties"
        MedicationName = "vancomycine"
        Route = "INTRAVENEUS"
        Shape = "poeder voor oplossing voor infusie"
        DoseType = "Timed"
        Products =
            [
                {
                    GpkPlaceholder = "2345678"
                    Quantity = 10m
                    Unit = "mL"
                    Description = "vancomycine 50 mg/mL oplossing voor infusie (na reconstitutie; placeholder GPK)"
                }
                {
                    GpkPlaceholder = "3456789"
                    Quantity = 40m
                    Unit = "mL"
                    Description = "glucose 5% vloeistof/diluent (placeholder GPK)"
                }
            ]
        AdminQuantity = 14.9m
        AdminUnit = "mL"
        Schema =
            {
                Frequency = Some 4
                TimePeriod = Some 1m
                TimeUnit = Some "dag"
                RateQuantity = Some 59m
                RateFormUnit = Some "mL"
                RateTimeUnit = Some "uur"
                ExactTimes = []
            }
    }


// --- Scenario 6.10: Multi-Product TPN (Totale Parenterale Voeding) ---
let scenario610 =
    {
        ScenarioId = "6.10"
        Description = "Multi-Product TPN – Totale Parenterale Voeding 1x/dag"
        WeightKg = 11m
        HeightCm = 79m
        Gender = "male"
        Indication = "Standaard Totale Parenterale Voeding"
        MedicationName = "TPV"
        Route = "INTRAVENEUS"
        Shape = "vloeistof voor infusie"
        DoseType = "Timed"
        Products =
            [
                {
                    GpkPlaceholder = "2345678"
                    Quantity = 105m
                    Unit = "mL"
                    Description = "Samenstelling C vloeistof voor infusie (placeholder GPK)"
                }
                {
                    GpkPlaceholder = "3456789"
                    Quantity = 59.5m
                    Unit = "mL"
                    Description = "NaCl 3% vloeistof voor infusie (placeholder GPK)"
                }
                {
                    GpkPlaceholder = "4567890"
                    Quantity = 20m
                    Unit = "mL"
                    Description = "KCl 7,4% vloeistof voor infusie (placeholder GPK)"
                }
                {
                    GpkPlaceholder = "5678901"
                    Quantity = 715.5m
                    Unit = "mL"
                    Description = "glucose 10% vloeistof voor infusie (placeholder GPK)"
                }
            ]
        AdminQuantity = 900m
        AdminUnit = "mL"
        Schema =
            {
                Frequency = Some 1
                TimePeriod = Some 1m
                TimeUnit = Some "dag"
                RateQuantity = Some 45m
                RateFormUnit = Some "mL"
                RateTimeUnit = Some "uur"
                ExactTimes = [ "17:00" ]
            }
    }


// --- Scenario 6.11: Multi-Product Enteral Feeding (MM met BMF) ---
let scenario611 =
    {
        ScenarioId = "6.11"
        Description = "Multi-Product Enteral – MM met BMF 8x/dag"
        WeightKg = 3.8m
        HeightCm = 53m
        Gender = "female"
        Indication = "Enterale voeding"
        MedicationName = "MM met BMF"
        Route = "ORAAL"
        Shape = "voeding"
        DoseType = "Timed"
        Products =
            [
                {
                    GpkPlaceholder = "9999978"
                    Quantity = 20m
                    Unit = "mL"
                    Description = "MM vloeistof voor voeding (placeholder GPK)"
                }
                {
                    GpkPlaceholder = "99999789"
                    Quantity = 0.1m
                    Unit = "g"
                    Description = "Nutrilon Nenatal BMF poeder voor voeding (placeholder GPK)"
                }
            ]
        AdminQuantity = 20m
        AdminUnit = "mL"
        Schema =
            {
                Frequency = Some 8
                TimePeriod = Some 1m
                TimeUnit = Some "dag"
                RateQuantity = None
                RateFormUnit = None
                RateTimeUnit = None
                ExactTimes = [ "07:00"; "10:00"; "13:00"; "16:00"; "19:00"; "22:00"; "01:00"; "04:00" ]
            }
    }


let allScenarios =
    [
        scenario61
        scenario62
        scenario63
        scenario631
        scenario64
        scenario65
        scenario66
        scenario67
        scenario68
        scenario69
        scenario610
        scenario611
    ]


// =============================================================================
// STEP 2: Look up product information from GPK codes via ZIndex
// =============================================================================
//
// NOTE: The GPK codes in the spec are PLACEHOLDERS. A real implementation would
// use ZIndex.GenericProduct.get [gpkCode] to resolve a product by its actual
// G-Standard GPK code. Here we demonstrate the lookup API by searching for
// products by their generic name.
//
// From these results the concentrations and dose limits are derived, NOT
// hardcoded. See Step 4 for how this feeds into OrderScenario reconstruction.

open Informedica.ZIndex.Lib


/// Print a summary of a GenericProduct record
let printGenericProduct (gp: Types.GenericProduct) =
    printfn "  GPK %i | %-30s | Form: %-30s | Routes: %s"
        gp.Id
        gp.Name
        gp.Form
        (gp.Route |> String.concat ", ")

    for sub in gp.Substances do
        printfn "    Substance: %-25s | Qty: %g %s | GenericQty: %g %s"
            sub.SubstanceName
            sub.SubstanceQuantity
            sub.SubstanceUnit
            sub.GenericQuantity
            sub.GenericUnit


/// Lookup all GenericProducts for a given medication name using ZIndex
let lookupByGenericName (name: string) =
    try
        GenericProduct.get []
        |> Array.filter (fun gp -> gp.Name |> String.equalsCapInsens name)
    with _ -> [||]


/// Demonstrate GPK lookup for the medications in our scenarios
let demonstrateProductLookup () =
    printfn "\n=== STEP 2: Product Lookup via ZIndex ==="
    printfn "NOTE: Spec uses placeholder GPK codes (e.g. '2345678')."
    printfn "Real lookup uses ZIndex.GenericProduct.get [gpkCode]."
    printfn "Concentrations and dose limits are derived from these results, not hardcoded.\n"

    let medicationNames =
        [
            "paracetamol"
            "gentamicine"
            "propofol"
            "noradrenaline"
        ]

    for name in medicationNames do
        printfn "--- %s ---" name
        let products = lookupByGenericName name

        if products |> Array.isEmpty then
            printfn "  (no products found – run after loading G-Standard data)"
        else
            products |> Array.truncate 3 |> Array.iter printGenericProduct

            if products.Length > 3 then
                printfn "  ... and %i more products" (products.Length - 3)

        printfn ""


demonstrateProductLookup ()


// =============================================================================
// STEP 3: Parse quantitative variables to ValueUnits
// =============================================================================
//
// Demonstrate how the FHIR scenario quantities (the only values present in the
// FHIR resources) translate to GenUnits ValueUnit values.
// These correspond to the four directly available data categories:
//   1. Component Orderable Quantities
//   2. Orderable Dose Quantity
//   3. Orderable Dose Rate
//   4. Schedule Frequency

let demonstrateValueUnitParsing () =
    printfn "\n=== STEP 3: Parsing Quantitative Variables to ValueUnits ==="

    // --- Patient context (for adjust calculations) ---

    let weight19_5kg = 19.5m |> BigRational.fromDecimal |> ValueUnit.singleWithUnit Units.Weight.kiloGram
    printfn "Patient weight:         %s" (weight19_5kg |> ValueUnit.toStringDecimalDutchShort)

    // --- 1. Component Orderable Quantities (from FHIR products block) ---

    let stuk = Units.General.general "stuk"
    let qty1stuk = 1N |> ValueUnit.singleWithUnit stuk
    printfn "Orderable qty (stuk):   %s" (qty1stuk |> ValueUnit.toStringDecimalDutchShort)

    let qty22mL = 22N |> ValueUnit.singleWithUnit Units.Volume.milliLiter
    printfn "Orderable qty (mL):     %s" (qty22mL |> ValueUnit.toStringDecimalDutchShort)

    // --- 2. Orderable Dose Quantity (from FHIR administration block) ---

    // Dose quantity (absolute) — from admin quantity
    let adminQty1stuk = 1N |> ValueUnit.singleWithUnit stuk
    printfn "Admin qty:              %s" (adminQty1stuk |> ValueUnit.toStringDecimalDutchShort)

    // --- 3. Orderable Dose Rate (from FHIR schema rate) ---

    // Rate: 85 mL/uur
    let rate85mlHr =
        85N
        |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Time.hour)

    printfn "Rate (85 mL/uur):       %s" (rate85mlHr |> ValueUnit.toStringDecimalDutchShort)

    // Rate: 3.3 mL/uur (propofol, stored as BigRational fraction)
    let rate33mlHr =
        BigRational.fromDecimal 3.3m
        |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Time.hour)

    printfn "Rate (3.3 mL/uur):      %s" (rate33mlHr |> ValueUnit.toStringDecimalDutchShort)

    // --- 4. Schedule Frequency (from FHIR schema pattern) ---

    // Frequency: 4 x/dag
    let freq4xDay =
        [| 4N |] |> ValueUnit.withUnit (Units.Count.times |> Units.per Units.Time.day)

    printfn "Frequency (4 x/dag):    %s" (freq4xDay |> ValueUnit.toStringDecimalDutchShort)

    // Frequency: 1 x/36 uur (gentamicine neonatal)
    let freq1x36h =
        [| 1N |] |> ValueUnit.withUnit (Units.Count.times |> Units.per Units.Time.hour)

    printfn "Frequency (1x/36 uur):  %s" (freq1x36h |> ValueUnit.toStringDecimalDutchShort)

    printfn ""


demonstrateValueUnitParsing ()


// =============================================================================
// STEP 4: Reconstruct an OrderScenario from FHIR context via OrderContext lookup
// =============================================================================
//
// A FHIR scenario provides patient data, indication, route, shape, and dose type.
// These are exactly the inputs needed to reconstruct an OrderScenario via the
// OrderContext lookup mechanism.
//
// The concentrations, dose limits, and product structure are NOT in the FHIR
// scenario — they come from the ZIndex/GenFORM database via OrderContext.getScenarios.
//
// Workflow:
//   1. Build a Patient from the FHIR patient context
//   2. Call OrderContext.create to get the initial context with available filters
//   3. Set the filter fields from the FHIR scenario (Indication, Generic, Route, Form, DoseType)
//   4. Call OrderContext.getScenarios (via UpdateOrderContext + evaluate) to look up matching scenarios
//   5. Apply the orderable quantities and schedule from the FHIR products + schema blocks

open Patient.Optics


/// Convert a FhirScenario's patient data into a Patient record.
/// NOTE: Age and Department are required for OrderContext lookups but are
/// not present in the FHIR scenario. These would come from the FHIR Patient
/// resource in a real implementation. Here we use defaults for demonstration.
let buildPatient (scenario: FhirScenario) : Patient =
    let gender =
        match scenario.Gender with
        | "male" -> Male
        | "female" -> Female
        | _ -> AnyGender

    Patient.patient
    |> Patient.setGender gender
    |> Patient.setAge [ 5 |> Years ]
    |> Patient.setWeight (scenario.WeightKg |> Kilogram |> Some)
    |> Patient.setHeight (scenario.HeightCm |> int |> Centimeter |> Some)
    |> Patient.setDepartment (Some "ICK")


/// Convert the scenario DoseType string to the GenFORM DoseType discriminated union
let parseDoseType (doseTypeStr: string) =
    // doseText is left empty here; it is filled in by the dose rule
    DoseType.fromString doseTypeStr ""


/// Print a summary of the OrderScenario context after lookup
let printOrderScenario (scenario: FhirScenario) (ctx: OrderContext) =
    printfn "\n--- Scenario %s: %s ---" scenario.ScenarioId scenario.Description
    printfn "  Patient: %g kg, %g cm, %s" scenario.WeightKg scenario.HeightCm scenario.Gender
    printfn "  Indication: %s  Route: %s  Shape: %s  DoseType: %s"
        scenario.Indication scenario.Route scenario.Shape scenario.DoseType

    printfn "  Scenarios found: %i" ctx.Scenarios.Length

    if ctx.Scenarios.Length > 0 then
        let sc = ctx.Scenarios[0]
        printfn "  First scenario: %s | %s | %s | %A"
            sc.Name sc.Route sc.Form sc.DoseType

        printfn ""
        printfn "  >>> Prescription (from ZIndex/GenFORM — NOT from FHIR scenario):"

        for line in sc.Prescription |> Array.collect id do
            let text =
                match line with
                | Valid s
                | Caution s
                | Warning s
                | Alert s -> s

            if text |> String.isNullOrWhiteSpace |> not then
                printfn "    %s" text

        printfn ""

        printfn "  >>> Available orderable quantities from the FHIR scenario:"
        printfn "    Admin quantity: %g %s" scenario.AdminQuantity scenario.AdminUnit

        for p in scenario.Products do
            printfn "    Component qty:  %g %s  (%s)" p.Quantity p.Unit p.Description

        match scenario.Schema.RateQuantity, scenario.Schema.RateFormUnit, scenario.Schema.RateTimeUnit with
        | Some rate, Some fu, Some tu ->
            printfn "    Rate:           %g %s/%s" rate fu tu
        | _ -> ()

        match scenario.Schema.Frequency, scenario.Schema.TimeUnit with
        | Some freq, Some unit ->
            let period = scenario.Schema.TimePeriod |> Option.defaultValue 1m
            let freqStr = if period = 1m then $"{freq} x/{unit}" else $"{freq} x/{period} {unit}"
            printfn "    Frequency:      %s" freqStr
        | _ -> ()

        if scenario.Schema.ExactTimes |> List.isEmpty |> not then
            printfn "    Exact times:    %s" (scenario.Schema.ExactTimes |> String.concat ", ")


printfn "\n=== STEP 4: Reconstructing OrderScenarios via OrderContext Lookup ==="
printfn """
The FHIR scenario provides: patient data, indication, route, shape, dose type.
These are the filter inputs for OrderContext.create + getScenarios.
Concentrations and dose limits come from ZIndex/GenFORM — not from the FHIR scenario.
"""

// A resource provider is required for OrderContext operations.
// In production, use Api.getCachedProviderWithDataUrlId.
// Here we use the demo cache that is available without authentication.
let provider : Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId OrderLogging.noOp (Environment.GetEnvironmentVariable("GENPRES_URL_ID"))


/// Reconstruct an OrderContext for a given FHIR scenario by progressively
/// setting filter fields and evaluating after each step. This is required
/// because each evaluate call narrows the available options for the next
/// filter field (e.g., setting Generic populates available Indications).
let reconstructOrderContext (scenario: FhirScenario) =
    let pat = buildPatient scenario

    let evaluate ctx =
        ctx
        |> OrderContext.UpdateOrderContext
        |> OrderContext.evaluate OrderLogging.noOp provider
        |> function
            | Ok cmd -> cmd |> OrderContext.Command.get |> Ok
            | Error e -> Error $"%A{e}"

    let setFilter f ctx = { ctx with Filter = f ctx.Filter }

    // Progressive filter: Generic → Indication → Route → Form → DoseType
    pat
    |> OrderContext.create OrderLogging.noOp provider
    |> setFilter (fun f -> { f with Generic = Some scenario.MedicationName })
    |> evaluate
    |> Result.bind (fun ctx ->
        ctx
        |> setFilter (fun f -> { f with Indication = Some scenario.Indication })
        |> evaluate)
    |> Result.bind (fun ctx ->
        ctx
        |> setFilter (fun f -> { f with Route = Some scenario.Route })
        |> evaluate)
    |> Result.bind (fun ctx ->
        ctx
        |> setFilter (fun f -> { f with Form = Some scenario.Shape })
        |> evaluate)
    |> Result.bind (fun ctx ->
        // Match DoseType by prefix (e.g., "Once" matches Once "startdosering")
        let targetDt = scenario.DoseType.ToLower()
        let matchedDt =
            ctx.Filter.DoseTypes
            |> Array.tryFind (fun dt ->
                let dtStr = $"%A{dt}".ToLower()
                dtStr.StartsWith(targetDt))

        match matchedDt with
        | Some dt ->
            ctx
            |> setFilter (fun f -> { f with DoseType = Some dt })
            |> evaluate
        | None ->
            // No matching DoseType — return context without DoseType filter
            Ok ctx)


for scenario in allScenarios do
    match reconstructOrderContext scenario with
    | Error msg ->
        printfn "\n--- Scenario %s: %s ---" scenario.ScenarioId scenario.Description
        printfn "  Lookup error: %s" msg
    | Ok ctx ->
        printOrderScenario scenario ctx


// =============================================================================
// STEP 5-6: Run through the order pipeline with scenario quantities
// =============================================================================
//
// Once an OrderScenario has been retrieved via lookup, the orderable quantities,
// dose rate, and schedule frequency from the FHIR scenario can be applied.
//
// These are the ONLY values that come from the FHIR scenario and are directly
// set on the order:
//   - Component orderable quantities (from products block)
//   - Orderable dose quantity or rate (from administration block / schema rate)
//   - Schedule frequency (from schema pattern)
//
// The solver then derives all other values from the ZIndex dose rules.

printfn "\n=== STEP 5-6: Running Scenarios through the Order Pipeline ==="

let runOrderScenario (scenario: FhirScenario) =
    printfn "\n--- Scenario %s: %s ---" scenario.ScenarioId scenario.Description

    match reconstructOrderContext scenario with
    | Error msg ->
        printfn "  Context error: %s" msg
    | Ok ctx ->
        match ctx.Scenarios |> Array.tryHead with
        | None ->
            printfn "  No scenarios found for this filter combination."
            printfn "  (Check that indication/route/shape/dose type match the GenFORM database)"
        | Some sc ->
            printfn "  Scenario: %s | %s | %A" sc.Name sc.Route sc.DoseType
            printfn "  Order type: %A" sc.Order.Schedule

            // Solve the order to propagate all constraints from ZIndex dose rules
            match sc.Order |> Order.solveMinMax "FHIR-ImplementationPlan" true OrderLogging.noOp with
            | Error(_, msgs) ->
                printfn "  Solve warning: %A" msgs
                sc.Order |> Order.printTable ConsoleTables.Format.Minimal

            | Ok solvedOrder ->
                printfn "  Solved OK."
                solvedOrder |> Order.printTable ConsoleTables.Format.Minimal


for scenario in allScenarios do
    runOrderScenario scenario


// =============================================================================
// STEP 7: Print summary of results
// =============================================================================

printfn "\n=== STEP 7: Summary ==="
printfn "Processed %i FHIR scenarios." (List.length allScenarios)
printfn ""

for scenario in allScenarios do
    let status =
        match reconstructOrderContext scenario with
        | Error msg -> $"CONTEXT ERROR: {msg}"
        | Ok ctx ->
            let n = ctx.Scenarios.Length

            if n = 0 then
                "NO SCENARIOS FOUND"
            else
                $"OK — {n} scenario(s) found"

    printfn "  Scenario %-6s %-52s %s" scenario.ScenarioId scenario.Description status


// =============================================================================
// STEP 8: FHIR R4 Bidirectional Translation Investigation
// =============================================================================
//
// Official FHIR R4 documentation references:
//   MedicationRequest  https://hl7.org/fhir/R4/medicationrequest.html
//   Medication         https://hl7.org/fhir/R4/medication.html
//   Dosage (Dosage)    https://hl7.org/fhir/R4/dosage.html
//   Timing             https://hl7.org/fhir/R4/datatypes.html#Timing
//   Quantity           https://hl7.org/fhir/R4/datatypes.html#Quantity
//   Ratio              https://hl7.org/fhir/R4/datatypes.html#Ratio
//   CodeableConcept    https://hl7.org/fhir/R4/datatypes.html#CodeableConcept
//
// Dutch G-Standard / NL FHIR:
//   GPK codes          OID urn:oid:2.16.840.1.113883.2.4.4.7
//   Route thesaurus 9  OID urn:oid:2.16.840.1.113883.2.4.4.9
//   Form thesaurus 10  OID urn:oid:2.16.840.1.113883.2.4.4.10
//   Medicatie-9        https://informatiestandaarden.nictiz.nl/wiki/Landingspagina_Medicatie
//
// ── Core design principle ────────────────────────────────────────────────────
//
// FHIR resources describe WHAT was ordered (the outcome of clinical decision-making).
// GenPRES derives HOW to order by looking up the correct dosing constraints from
// the ZIndex/GenFORM database. The FHIR resource provides only the filter context
// (patient + indication + route + form + dose type) and the measured/chosen
// orderable quantities. Everything else (concentrations, dose limits) comes from
// ZIndex.
//
// ── FHIR R4 SDK types ────────────────────────────────────────────────────────
//
// Using the official Hl7.Fhir.R4 NuGet package (loaded via load.fsx).
// See: https://docs.fire.ly/projects/Firely-NET-SDK/

open Hl7.Fhir.Model

module List =
    let ofSeq (s: seq<'T>) = s |> Seq.toList


// ── G-Standard and FHIR system constants ─────────────────────────────────────

module FhirSystems =

    let gpk = "urn:oid:2.16.840.1.113883.2.4.4.7"
    let route = "urn:oid:2.16.840.1.113883.2.4.4.9"
    let form = "urn:oid:2.16.840.1.113883.2.4.4.10"
    let ucum = "http://unitsofmeasure.org"
    let snomed = "http://snomed.info/sct"


module RouteMapping =

    let codeToName =
        Map.ofList
            [
                "2", "INTRAVENEUS"
                "9", "ORAAL"
                "12", "RECTAAL"
                "14", "SUBCUTAAN"
                "15", "INTRAMUSCULAIR"
                "46", "INHALATIE"
            ]

    let nameToCode = codeToName |> Map.toList |> List.map (fun (k, v) -> v, k) |> Map.ofList

    let toCode name =
        nameToCode |> Map.tryFind name |> Option.defaultValue ""

    let toName code =
        codeToName |> Map.tryFind code |> Option.defaultValue ""


module PeriodUnitMapping =

    let fhirToGenPres =
        Map.ofList
            [
                "s", "seconde"
                "min", "minuut"
                "h", "uur"
                "d", "dag"
                "wk", "week"
                "mo", "maand"
                "a", "jaar"
            ]

    let genPresToFhir = fhirToGenPres |> Map.toList |> List.map (fun (k, v) -> v, k) |> Map.ofList

    let toGenPres unit =
        fhirToGenPres |> Map.tryFind unit |> Option.defaultValue unit

    let toFhir unit =
        genPresToFhir |> Map.tryFind unit |> Option.defaultValue unit

    let toUnitsOfTime (s: string) =
        match s with
        | "s" -> Some Timing.UnitsOfTime.S
        | "min" -> Some Timing.UnitsOfTime.Min
        | "h" -> Some Timing.UnitsOfTime.H
        | "d" -> Some Timing.UnitsOfTime.D
        | "wk" -> Some Timing.UnitsOfTime.Wk
        | "mo" -> Some Timing.UnitsOfTime.Mo
        | "a" -> Some Timing.UnitsOfTime.A
        | _ -> None

    let fromUnitsOfTime (u: Timing.UnitsOfTime) =
        match u with
        | Timing.UnitsOfTime.S -> "s"
        | Timing.UnitsOfTime.Min -> "min"
        | Timing.UnitsOfTime.H -> "h"
        | Timing.UnitsOfTime.D -> "d"
        | Timing.UnitsOfTime.Wk -> "wk"
        | Timing.UnitsOfTime.Mo -> "mo"
        | Timing.UnitsOfTime.A -> "a"
        | _ -> ""


// ── Helper: Nullable <-> Option ──────────────────────────────────────────────

module Nullable =

    let toOption (n: System.Nullable<'T>) =
        if n.HasValue then Some n.Value else None

    let ofOption (opt: 'T option) =
        match opt with
        | Some v -> System.Nullable v
        | None -> System.Nullable()


// ── FHIR -> GenPRES translation ──────────────────────────────────────────────

/// Infer the GenPRES DoseType string from the structure of a FHIR Dosage.
///
/// Decision tree:
///   1. Exact times (TimeOfDay non-empty) -> always Timed
///   2. No timing at all -> Once (no rate) or Continuous (has rate)
///   3. Frequency with PeriodUnit (recurring schedule):
///      - any freq + rate -> Timed
///      - any freq, no rate -> Discontinuous
///   4. Frequency without PeriodUnit (one-time):
///      - freq=1 + rate or duration -> OnceTimed
///      - freq=1, no rate -> Once
let inferDoseType (dosage: Dosage) : string =
    let hasRate =
        dosage.DoseAndRate
        |> List.ofSeq
        |> List.exists (fun dr -> dr.Rate <> null)

    let repeat =
        if dosage.Timing <> null && dosage.Timing.Repeat <> null then
            Some dosage.Timing.Repeat
        else
            None

    let hasExactTimes =
        repeat
        |> Option.map (fun r -> r.TimeOfDay |> Seq.isEmpty |> not)
        |> Option.defaultValue false

    if hasExactTimes then
        "Timed"
    else
        match repeat with
        | None -> if hasRate then "Continuous" else "Once"
        | Some r ->
            let freq = r.Frequency |> Nullable.toOption
            let periodUnit = r.PeriodUnit |> Nullable.toOption
            let duration = r.Duration |> Nullable.toOption

            match freq, periodUnit, duration with
            | None, _, _ -> if hasRate then "Continuous" else "Once"
            | Some _, Some _, _ when hasRate -> "Timed"
            | Some _, Some _, _ -> "Discontinuous"
            | Some _, None, Some _ -> "OnceTimed"
            | Some _, None, None when hasRate -> "OnceTimed"
            | Some _, None, None -> "Once"


/// Extract schedule from a FHIR Dosage
let extractSchema (dosage: Dosage) : AdministrationSchema =
    let rateQty, rateFormUnit, rateTimeUnit =
        dosage.DoseAndRate
        |> List.ofSeq
        |> List.tryHead
        |> Option.bind (fun dr -> if dr.Rate <> null then Some dr.Rate else None)
        |> Option.map (fun rate ->
            match rate with
            | :? Ratio as r ->
                Some r.Numerator.Value.Value, Some r.Numerator.Unit, Some r.Denominator.Unit
            | :? Quantity as q ->
                let parts = (if q.Code <> null then q.Code else q.Unit).Split('/')
                match parts with
                | [| num; den |] -> Some q.Value.Value, Some num, Some(PeriodUnitMapping.toGenPres den)
                | _ -> Some q.Value.Value, Some q.Unit, None
            | _ -> None, None, None)
        |> Option.defaultValue (None, None, None)

    let frequency, timePeriod, timeUnit, exactTimes =
        if dosage.Timing <> null && dosage.Timing.Repeat <> null then
            let r = dosage.Timing.Repeat
            let freq = r.Frequency |> Nullable.toOption
            let period = r.Period |> Nullable.toOption
            let unit =
                r.PeriodUnit
                |> Nullable.toOption
                |> Option.map PeriodUnitMapping.fromUnitsOfTime
                |> Option.map PeriodUnitMapping.toGenPres
            let times = r.TimeOfDay |> Seq.toList
            freq, period, unit, times
        else
            None, None, None, []

    {
        Frequency = frequency
        TimePeriod = timePeriod
        TimeUnit = timeUnit
        RateQuantity = rateQty
        RateFormUnit = rateFormUnit
        RateTimeUnit = rateTimeUnit
        ExactTimes = exactTimes
    }


/// Convert a FHIR MedicationRequest + patient parameters into a FhirScenario.
let fromFhirMedicationRequest
    (weightKg: decimal)
    (heightCm: decimal)
    (gender: string)
    (req: MedicationRequest)
    : FhirScenario =

    let indication =
        req.ReasonCode
        |> List.ofSeq
        |> List.tryHead
        |> Option.bind (fun cc -> if cc.Text <> null then Some cc.Text else None)
        |> Option.defaultValue ""

    let medCc =
        if req.Medication <> null then req.Medication :?> CodeableConcept
        else CodeableConcept()

    let gpkCode =
        medCc.Coding
        |> List.ofSeq
        |> List.tryFind (fun c -> c.System = FhirSystems.gpk)
        |> Option.map _.Code
        |> Option.defaultValue ""

    let medicationName =
        if gpkCode |> String.isNullOrWhiteSpace then
            if medCc.Text <> null then medCc.Text else ""
        else
            match gpkCode |> System.Int32.TryParse with
            | true, gpkInt ->
                try
                    match GenericProduct.get [ gpkInt ] |> Array.tryHead with
                    | Some gp -> gp.Name
                    | None -> if medCc.Text <> null then medCc.Text else gpkCode
                with _ -> if medCc.Text <> null then medCc.Text else gpkCode
            | false, _ -> if medCc.Text <> null then medCc.Text else gpkCode

    let dosages = req.DosageInstruction |> List.ofSeq
    let firstDosage = dosages |> List.tryHead

    let route =
        firstDosage
        |> Option.bind (fun d -> if d.Route <> null then Some d.Route else None)
        |> Option.map (fun cc ->
            if cc.Text <> null then cc.Text
            else
                cc.Coding
                |> List.ofSeq
                |> List.tryFind (fun c -> c.System = FhirSystems.route)
                |> Option.map (fun c -> RouteMapping.toName c.Code)
                |> Option.defaultValue "")
        |> Option.defaultValue ""

    let shape =
        req.Contained
        |> List.ofSeq
        |> List.tryHead
        |> Option.bind (fun r ->
            match r with
            | :? Medication as m when m.Form <> null && m.Form.Text <> null -> Some m.Form.Text
            | _ -> None)
        |> Option.defaultValue ""

    let doseType =
        firstDosage
        |> Option.map inferDoseType
        |> Option.defaultValue "Once"

    let adminQty, adminUnit =
        firstDosage
        |> Option.bind (fun d ->
            d.DoseAndRate |> List.ofSeq |> List.tryHead)
        |> Option.bind (fun dr ->
            if dr.Dose <> null then Some (dr.Dose :?> Quantity) else None)
        |> Option.map (fun q -> q.Value.Value, q.Unit)
        |> Option.defaultValue (0m, "")

    let schema =
        firstDosage
        |> Option.map extractSchema
        |> Option.defaultValue
            {
                Frequency = None; TimePeriod = None; TimeUnit = None
                RateQuantity = None; RateFormUnit = None; RateTimeUnit = None
                ExactTimes = []
            }

    let products =
        req.Contained
        |> List.ofSeq
        |> List.collect (fun r ->
            match r with
            | :? Medication as med ->
                med.Ingredient
                |> List.ofSeq
                |> List.map (fun ing ->
                    let itemCc = ing.Item :?> CodeableConcept
                    let gpk =
                        itemCc.Coding
                        |> List.ofSeq
                        |> List.tryFind (fun c -> c.System = FhirSystems.gpk)
                        |> Option.map _.Code
                        |> Option.defaultValue ""
                    let display =
                        itemCc.Coding
                        |> List.ofSeq
                        |> List.tryHead
                        |> Option.bind (fun c -> if c.Display <> null then Some c.Display else None)
                        |> Option.defaultValue (if itemCc.Text <> null then itemCc.Text else "")
                    {
                        GpkPlaceholder = gpk
                        Quantity = adminQty
                        Unit = adminUnit
                        Description = display
                    })
            | _ -> [])

    {
        ScenarioId = if req.Id <> null then req.Id else ""
        Description =
            req.Note
            |> List.ofSeq
            |> List.tryHead
            |> Option.map _.Text.ToString()
            |> Option.defaultValue ""
        WeightKg = weightKg; HeightCm = heightCm; Gender = gender
        Indication = indication; MedicationName = medicationName
        Route = route; Shape = shape; DoseType = doseType
        Products = products; AdminQuantity = adminQty; AdminUnit = adminUnit
        Schema = schema
    }


// ── GenPRES -> FHIR translation ──────────────────────────────────────────────

/// Build a FHIR Timing resource from an AdministrationSchema.
/// The doseType is used to emit Frequency=1 for Once/OnceTimed when the
/// schema has no explicit frequency, so the round-trip preserves DoseType.
let toFhirTiming (doseType: string) (schema: AdministrationSchema) : Timing option =
    let frequency =
        match schema.Frequency with
        | Some _ -> schema.Frequency
        | None ->
            match doseType with
            | "Once" | "OnceTimed" -> Some 1
            | _ -> None

    match frequency, schema.TimePeriod, schema.TimeUnit with
    | None, None, None when schema.ExactTimes |> List.isEmpty -> None
    | _ ->
        let timing = Timing()
        let rep = Timing.RepeatComponent()
        rep.Frequency <- frequency |> Nullable.ofOption
        rep.Period <- schema.TimePeriod |> Nullable.ofOption

        schema.TimeUnit
        |> Option.bind (PeriodUnitMapping.toFhir >> PeriodUnitMapping.toUnitsOfTime)
        |> Nullable.ofOption
        |> fun v -> rep.PeriodUnit <- v

        if schema.ExactTimes |> List.isEmpty |> not then
            rep.TimeOfDay <- System.Collections.Generic.List<string>(schema.ExactTimes)

        timing.Repeat <- rep
        Some timing


/// Build FHIR Dosage.DoseAndRateComponent from schema + admin quantity
let toFhirDosageAndRate (adminQty: decimal) (adminUnit: string) (schema: AdministrationSchema) : Dosage.DoseAndRateComponent =
    let dr = Dosage.DoseAndRateComponent()

    if adminQty > 0m then
        dr.Dose <-
            Quantity(
                Value = System.Nullable adminQty,
                Unit = adminUnit,
                System = FhirSystems.ucum,
                Code = adminUnit
            ) :> DataType

    match schema.RateQuantity, schema.RateFormUnit, schema.RateTimeUnit with
    | Some qty, Some fu, Some tu ->
        dr.Rate <-
            Ratio(
                Numerator = Quantity(Value = System.Nullable qty, Unit = fu, System = FhirSystems.ucum, Code = fu),
                Denominator = Quantity(Value = System.Nullable 1m, Unit = tu, System = FhirSystems.ucum, Code = PeriodUnitMapping.toFhir tu)
            ) :> DataType
    | _ -> ()

    dr


/// Convert a GenPRES FhirScenario into a FHIR R4 MedicationRequest.
let toFhirMedicationRequest
    (resolvedGpkCode: string)
    (patientRef: string)
    (scenario: FhirScenario)
    : MedicationRequest =

    let routeCode = RouteMapping.toCode scenario.Route

    let dosage = Dosage()

    toFhirTiming scenario.DoseType scenario.Schema
    |> Option.iter (fun t -> dosage.Timing <- t)

    dosage.Route <-
        CodeableConcept(
            Text = scenario.Route,
            Coding = System.Collections.Generic.List<_>(
                [ Coding(System = FhirSystems.route, Code = routeCode, Display = scenario.Route) ]))

    dosage.DoseAndRate <-
        System.Collections.Generic.List<_>(
            [ toFhirDosageAndRate scenario.AdminQuantity scenario.AdminUnit scenario.Schema ])

    let medicationIngredients =
        scenario.Products
        |> List.map (fun p ->
            let ing = Medication.IngredientComponent()
            ing.Item <-
                CodeableConcept(
                    Text = p.Description,
                    Coding = System.Collections.Generic.List<_>(
                        [ Coding(System = FhirSystems.gpk, Code = p.GpkPlaceholder, Display = p.Description) ]
                    )) :> DataType
            ing.IsActive <- System.Nullable true
            ing)

    let containedMedication = Medication()
    containedMedication.Id <- $"med-{scenario.ScenarioId}"
    containedMedication.Code <-
        CodeableConcept(
            Text = scenario.MedicationName,
            Coding = System.Collections.Generic.List<_>(
                [ Coding(System = FhirSystems.gpk, Code = resolvedGpkCode, Display = scenario.MedicationName) ]))
    containedMedication.Form <- CodeableConcept(Text = scenario.Shape)
    containedMedication.Ingredient <- System.Collections.Generic.List<_>(medicationIngredients)

    let req = MedicationRequest()
    req.Id <- $"req-{scenario.ScenarioId}"
    req.Status <- System.Nullable MedicationRequest.MedicationrequestStatus.Active
    req.Intent <- System.Nullable MedicationRequest.MedicationRequestIntent.Order
    req.Medication <-
        CodeableConcept(
            Text = scenario.MedicationName,
            Coding = System.Collections.Generic.List<_>(
                [ Coding(System = FhirSystems.gpk, Code = resolvedGpkCode, Display = scenario.MedicationName) ]
            )) :> DataType
    req.Subject <- ResourceReference(patientRef)
    req.AuthoredOn <- DateTime.UtcNow.ToString("o")
    req.ReasonCode <- System.Collections.Generic.List<_>([ CodeableConcept(Text = scenario.Indication) ])
    req.Note <- System.Collections.Generic.List<_>([ Annotation(Text = Markdown(scenario.Description)) ])
    req.DosageInstruction <- System.Collections.Generic.List<_>([ dosage ])
    req.Contained <- System.Collections.Generic.List<Resource>([ containedMedication :> Resource ])
    req


// ── Demonstrate round-trip for each scenario ──────────────────────────────────

printfn "\n=== STEP 8: FHIR R4 Bidirectional Translation (using Hl7.Fhir.R4 SDK) ==="
printfn """
Now using official Hl7.Fhir.R4 NuGet package types:
  MedicationRequest, Medication, Dosage, Timing, Quantity, Ratio, etc.
"""

printfn "--- Round-trip demonstration (scenario -> FHIR -> scenario) ---"


/// Result of processing a single FHIR scenario through the full pipeline
type ScenarioReport =
    {
        ScenarioId: string
        Description: string
        RoundTrip: {| Route: bool; DoseType: bool; Indication: bool |}
        Order: string list option
    }


/// Format TextBlock arrays into concise lines, joining related blocks
let formatTextBlocks (blocks: TextBlock[][]) =
    blocks
    |> Array.collect id
    |> Array.choose (fun tb ->
        match tb with
        | Valid s | Caution s | Warning s | Alert s ->
            if s |> String.isNullOrWhiteSpace |> not then Some s
            else None)
    |> Array.toList


/// Run the full FHIR round-trip + order calculation for all scenarios.
/// Returns a list of ScenarioReport values and prints a concise summary.
let runRoundTrip () =
    allScenarios
    |> List.map (fun scenario ->
        let gpkPlaceholder =
            scenario.Products
            |> List.tryHead
            |> Option.map _.GpkPlaceholder
            |> Option.defaultValue ""

        let fhirReq = toFhirMedicationRequest gpkPlaceholder "Patient/DEMO" scenario

        let roundTripped =
            fromFhirMedicationRequest scenario.WeightKg scenario.HeightCm scenario.Gender fhirReq

        let rtResult =
            {|
                Route = roundTripped.Route = scenario.Route
                DoseType = roundTripped.DoseType = scenario.DoseType
                Indication = roundTripped.Indication = scenario.Indication
            |}

        let orderLines =
            match reconstructOrderContext scenario with
            | Error _ -> None
            | Ok ctx ->
                ctx.Scenarios
                |> Array.tryHead
                |> Option.map (fun sc ->
                    let prs = sc.Prescription |> formatTextBlocks
                    let prep = sc.Preparation |> formatTextBlocks
                    let adm = sc.Administration |> formatTextBlocks

                    [
                        if prs |> List.isEmpty |> not then
                            yield "Rx:    " + (prs |> String.concat " ")
                        if prep |> List.isEmpty |> not then
                            yield "Prep:  " + (prep |> String.concat " ")
                        if adm |> List.isEmpty |> not then
                            yield "Admin: " + (adm |> String.concat " ")
                    ])

        {
            ScenarioId = scenario.ScenarioId
            Description = scenario.Description
            RoundTrip = rtResult
            Order = orderLines
        })


/// Print a concise report from the round-trip results
let printReport (reports: ScenarioReport list) =
    printfn ""

    for r in reports do
        let rt =
            [
                if r.RoundTrip.Route then "Route" else "Route:X"
                if r.RoundTrip.DoseType then "DoseType" else "DoseType:X"
                if r.RoundTrip.Indication then "Indication" else "Indication:X"
            ]
            |> String.concat ", "

        printfn "--- %s: %s [%s]" r.ScenarioId r.Description rt

        match r.Order with
        | None -> printfn "    (no matching order scenario)"
        | Some lines ->
            for line in lines do
                line
                |> String.replace "#" ""
                |> String.replace "|" ""
                |> printfn "    %s"

        printfn ""


let report = runRoundTrip ()

report
|> printReport

// ── Next implementation steps ─────────────────────────────────────────────────


// A FhirScenario should be translated to a string representation of a Medication
// to be able to recreate an Order. The rest of the FhirScenario is needed to provide
// the context for the OrderScenario
let sampleMed =
        """
Id: 93e8c175-99a1-48d8-b2f4-90005fdb8ada
Name: paracetamol
Quantity: 1
Quantities:
Route: RECTAAL
OrderType: OnceOrder
Adjust: 10 kg
Frequencies:
Time:
Dose: [dun], [qty] 1 stuk/dosis
Div:
DoseCount: 1 x
Components:

	Name: paracetamol
	Form: zetpil
	Quantities: 1 stuk
	Divisible: 1
	Dose: [qty] 1 stuk
	Solution:
	Substances:

		Name: paracetamol
		Concentrations: 120;240;500;1000;125;250;60;30;360;90;750;180 mg/stuk
		Dose:
		Solution:
"""

printfn """

=== Next steps for Informedica.FHIR.Lib ===
  1. Add Hl7.Fhir.R4 NuGet package (paket: 'nuget Hl7.Fhir.R4')
  2. Replace the script FhirMedicationRequest type with the Hl7.Fhir.R4 model type
  3. Implement fromFhirMedicationRequest using the FhirScenario approach:
       MedicationRequest resource → FhirScenario → Medication -> Order -> OrderScenario -> Run scenario to
       calculate the full order -> print the order
  4. Implement toFhirMedicationRequest:
       OrderScenario + orderable quantities → MedicationRequest resource
  5. Add JSON serialization using Hl7.Fhir.Serialization.FhirJsonParser
  6. Write Expecto tests for each scenario defined in this script
  7. Validate round-trip: fromFhirMedicationRequest (toFhirMedicationRequest scenario)
     preserves Route, DoseType, Indication, AdminQuantity, Rate, Frequency
"""
