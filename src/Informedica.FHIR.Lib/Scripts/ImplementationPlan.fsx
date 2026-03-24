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

open System
open MathNet.Numerics
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
        // Total administration quantity and unit
        AdminQuantity: decimal
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
                Frequency = Some 1
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
                Frequency = Some 1
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
    GenericProduct.get []
    |> Array.filter (fun gp -> gp.Name |> String.equalsCapInsens name)


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


/// Convert a FhirScenario's patient data into a Patient record
let buildPatient (scenario: FhirScenario) : Patient =
    let gender =
        match scenario.Gender with
        | "male" -> Male
        | "female" -> Female
        | _ -> AnyGender

    patient
    |> setGender gender
    |> setWeight (scenario.WeightKg |> Kilogram |> Some)
    |> setHeight (scenario.HeightCm |> decimal |> int |> Centimeter |> Some)


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


/// Reconstruct an OrderContext for a given FHIR scenario by:
///   1. Building the Patient from scenario patient data
///   2. Creating an initial OrderContext
///   3. Setting the filter from the scenario's indication, route, shape, and dose type
///   4. Evaluating to trigger the ZIndex/GenFORM lookup
let reconstructOrderContext (scenario: FhirScenario) : Result<OrderContext, string> =
    let pat = buildPatient scenario
    let doseType = parseDoseType scenario.DoseType

    let ctx = OrderContext.create OrderLogging.noOp provider pat

    // Set filter fields from the FHIR scenario context
    let filter =
        { ctx.Filter with
            Indication = Some scenario.Indication
            Generic = Some scenario.MedicationName
            Route = Some scenario.Route
            Form = Some scenario.Shape
            DoseType = Some doseType
        }

    { ctx with Filter = filter }
    |> OrderContext.UpdateOrderContext
    |> OrderContext.evaluate OrderLogging.noOp provider
    |> function
        | Ok cmd -> cmd |> OrderContext.Command.get |> Ok
        | Error(msg, _) -> Error msg


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
            | Error(_, msg) ->
                printfn "  Solve warning: %s" msg
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
// ── FHIR R4 resource types (mirror of the official data model) ───────────────
//
// The types below model exactly the FHIR R4 fields used in GenPRES integration.
// They are defined here in the script to document the mapping; the actual
// Informedica.FHIR.Lib library would use Hl7.Fhir.R4 (NuGet) types instead.

/// A single coding within a CodeableConcept
type FhirCoding =
    {
        // Coding system URI, e.g. "urn:oid:2.16.840.1.113883.2.4.4.7" for GPK
        System: string
        // Code value within the system
        Code: string
        // Human-readable display text
        Display: string option
    }


/// A concept represented by one or more codings plus an optional display text
type FhirCodeableConcept =
    {
        Coding: FhirCoding list
        // Free-text representation (used when no coding is available)
        Text: string option
    }


/// A measured or measurable quantity with unit
type FhirQuantity =
    {
        Value: decimal
        // Unit display label
        Unit: string
        // Unit system URI, e.g. "http://unitsofmeasure.org" for UCUM
        System: string option
        // UCUM code for the unit, e.g. "mL", "mg", "h"
        Code: string option
    }


/// A ratio of two quantities, used for rates and concentrations
type FhirRatio =
    {
        // Numerator (e.g. 85 mL for a rate of 85 mL/uur)
        Numerator: FhirQuantity
        // Denominator (e.g. 1 uur)
        Denominator: FhirQuantity
    }


/// The repeat pattern within a Timing resource
/// See https://hl7.org/fhir/R4/datatypes.html#Timing
type FhirTimingRepeat =
    {
        // How many times per period (e.g. 4 for "4 x/dag")
        Frequency: int option
        // Length of the period (e.g. 1 for "per dag", 36 for "per 36 uur")
        Period: decimal option
        // UCUM-based period unit: s | min | h | d | wk | mo | a
        PeriodUnit: string option
        // Duration of each administration for timed infusions (e.g. 15 min)
        Duration: decimal option
        // UCUM-based duration unit
        DurationUnit: string option
        // Exact clock times for Timed orders, format HH:MM:SS
        TimeOfDay: string list
    }


/// The Timing datatype: describes when an event is to occur
type FhirTiming =
    {
        // Specific event date/times (for OnceTimed with a fixed start)
        Event: DateTime list
        // Repeating pattern
        Repeat: FhirTimingRepeat option
    }


/// Rate expressed as either a ratio or a simple quantity
type FhirDosageRate =
    // Ratio: numerator/denominator, e.g. 85 mL / 1 uur
    | RateRatio of FhirRatio
    // SimpleQuantity with UCUM composite unit, e.g. "85 mL/h"
    | RateQuantity of FhirQuantity


/// A dose/rate entry within dosageInstruction.doseAndRate[]
type FhirDosageAndRate =
    {
        // Type of dose entry (e.g. "ordered" vs. "calculated"); optional
        Type: FhirCodeableConcept option
        // The dose quantity per administration
        Dose: FhirQuantity option
        // The infusion rate
        Rate: FhirDosageRate option
    }


/// The Dosage datatype: instructions for how medication should be taken/given
/// See https://hl7.org/fhir/R4/dosage.html
type FhirDosage =
    {
        // Free-text dosage instructions (human-readable summary)
        Text: string option
        // Timing of administration
        Timing: FhirTiming option
        // Route of administration (G-Standard thesaurus 9)
        Route: FhirCodeableConcept option
        // Method of administration
        Method: FhirCodeableConcept option
        // Dose quantity and rate
        DoseAndRate: FhirDosageAndRate list
    }


/// A single ingredient within a Medication resource
type FhirMedicationIngredient =
    {
        // The substance identified by GPK code
        ItemCodeableConcept: FhirCodeableConcept
        // Whether this is an active ingredient
        IsActive: bool option
        // Concentration: e.g. 10 mg / 1 mL
        Strength: FhirRatio option
    }


/// The Medication resource: describes the medication product
/// See https://hl7.org/fhir/R4/medication.html
type FhirMedication =
    {
        ResourceType: string // always "Medication"
        Id: string option
        // Product identification by GPK code
        Code: FhirCodeableConcept
        // Pharmaceutical form (G-Standard thesaurus 10)
        Form: FhirCodeableConcept option
        // Ingredient list (supports multi-ingredient products)
        Ingredient: FhirMedicationIngredient list
    }


/// A reference to another FHIR resource
type FhirReference =
    {
        // Relative or absolute reference, e.g. "Patient/123456"
        Reference: string
        Display: string option
    }


/// The MedicationRequest resource: a prescription or medication order
/// See https://hl7.org/fhir/R4/medicationrequest.html
type FhirMedicationRequest =
    {
        ResourceType: string // always "MedicationRequest"
        Id: string option
        // Status: active | draft | on-hold | cancelled | completed | ...
        Status: string
        // Intent: proposal | plan | order | original-order | ...
        Intent: string
        // Identified medication by GPK code (use medicationCodeableConcept for GPK)
        MedicationCodeableConcept: FhirCodeableConcept
        // The patient
        Subject: FhirReference
        // When the prescription was written
        AuthoredOn: DateTime option
        // Clinical indication (ICD-10 or free text)
        ReasonCode: FhirCodeableConcept list
        // Free-text notes
        Note: string list
        // Dosage instructions
        DosageInstruction: FhirDosage list
        // How much to dispense
        DispenseRequest: {| Quantity: FhirQuantity option |} option
        // Contained Medication resources (for inline ingredient detail)
        Contained: FhirMedication list
    }


// ── G-Standard and FHIR system constants ─────────────────────────────────────
//
// These OIDs and system URIs are used to identify coding systems in FHIR resources.

/// G-Standard and FHIR coding system constants
module FhirSystems =

    /// OID for the Dutch G-Standard GPK product table
    let gpk = "urn:oid:2.16.840.1.113883.2.4.4.7"

    /// OID for the G-Standard route thesaurus (Thesaurus 9)
    let route = "urn:oid:2.16.840.1.113883.2.4.4.9"

    /// OID for the G-Standard pharmaceutical form thesaurus (Thesaurus 10)
    let form = "urn:oid:2.16.840.1.113883.2.4.4.10"

    /// UCUM unit system URI
    let ucum = "http://unitsofmeasure.org"

    /// SNOMED CT system URI
    let snomed = "http://snomed.info/sct"


/// G-Standard route code → GenPRES route name and vice versa
module RouteMapping =

    // G-Standard Thesaurus 9 route codes
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


/// FHIR timing period unit (UCUM-based) ↔ GenPRES time unit
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


// ── FHIR → GenPRES translation ────────────────────────────────────────────────
//
// fromFhirMedicationRequest: converts a FhirMedicationRequest resource into a
// FhirScenario that can drive the GenPRES OrderContext lookup.
//
// Translation logic:
//   - Patient context comes from the FHIR Patient resource (passed as parameters here)
//   - Indication: MedicationRequest.reasonCode[0].text
//   - Generic name: resolved from GPK code via ZIndex.GenericProduct.get [gpkCode]
//   - Route: MedicationRequest.dosageInstruction[0].route.text (or look up from coding)
//   - Shape: Medication.form.text (from contained Medication resource)
//   - DoseType: inferred from timing and rate structure (see below)
//   - Products: from Medication.ingredient[] + MedicationRequest quantities
//   - AdminQuantity: dosageInstruction[0].doseAndRate[0].doseQuantity
//   - Rate: dosageInstruction[0].doseAndRate[0].rateRatio (RateFormUnit/RateTimeUnit)
//   - Frequency: dosageInstruction[0].timing.repeat (frequency, period, periodUnit)
//   - ExactTimes: dosageInstruction[0].timing.repeat.timeOfDay

/// Infer the GenPRES DoseType string from the structure of a FHIR Dosage
let inferDoseType (dosage: FhirDosage) : string =
    let hasRate = dosage.DoseAndRate |> List.exists (fun dr -> dr.Rate.IsSome)

    match dosage.Timing with
    | None -> if hasRate then "Continuous" else "Once"
    | Some timing ->
        match timing.Repeat with
        | None -> if hasRate then "Continuous" else "Once"
        | Some r ->
            match r.Frequency, r.Duration with
            // No frequency → continuous
            | None, _ -> if hasRate then "Continuous" else "Once"
            // One-time with duration or rate → OnceTimed
            | Some 1, Some _ -> "OnceTimed"
            | Some 1, None when hasRate -> "OnceTimed"
            // One-time, no rate → Once
            | Some 1, None -> "Once"
            // Multiple per period with rate → Timed
            | Some _, _ when hasRate -> "Timed"
            // Multiple per period, no rate → Discontinuous
            | Some _, _ -> "Discontinuous"


/// Extract schedule from a FHIR Dosage
let extractSchema (dosage: FhirDosage) : AdministrationSchema =
    let rateQty, rateFormUnit, rateTimeUnit =
        dosage.DoseAndRate
        |> List.tryHead
        |> Option.bind _.Rate
        |> Option.map
            (function
            | RateRatio r -> Some r.Numerator.Value, Some r.Numerator.Unit, Some r.Denominator.Unit
            | RateQuantity q ->
                // Parse UCUM composite unit like "mL/h"
                let parts =
                    (q.Code |> Option.defaultValue q.Unit).Split('/')

                match parts with
                | [| num; den |] -> Some q.Value, Some num, Some(PeriodUnitMapping.toGenPres den)
                | _ -> Some q.Value, Some q.Unit, None)
        |> Option.defaultValue (None, None, None)

    let frequency, timePeriod, timeUnit, exactTimes =
        dosage.Timing
        |> Option.map (fun t ->
            let r = t.Repeat

            let freq = r |> Option.bind _.Frequency
            let period = r |> Option.bind _.Period

            let unit =
                r
                |> Option.bind _.PeriodUnit
                |> Option.map PeriodUnitMapping.toGenPres

            let times = r |> Option.map _.TimeOfDay |> Option.defaultValue []
            freq, period, unit, times)
        |> Option.defaultValue (None, None, None, [])

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
/// The FhirScenario can then drive GenPRES OrderContext lookup (see Step 4).
let fromFhirMedicationRequest
    (weightKg: decimal)
    (heightCm: decimal)
    (gender: string)
    (req: FhirMedicationRequest)
    : FhirScenario =

    // --- Indication (reasonCode.text) ---
    let indication =
        req.ReasonCode
        |> List.tryHead
        |> Option.bind _.Text
        |> Option.defaultValue ""

    // --- GPK code → generic name via ZIndex ---
    let gpkCode =
        req.MedicationCodeableConcept.Coding
        |> List.tryFind (fun c -> c.System = FhirSystems.gpk)
        |> Option.map _.Code
        |> Option.defaultValue ""

    let medicationName =
        if gpkCode |> String.isNullOrWhiteSpace then
            req.MedicationCodeableConcept.Text |> Option.defaultValue ""
        else
            // Try to parse GPK code as int and look up via ZIndex
            match gpkCode |> System.Int32.TryParse with
            | true, gpkInt ->
                match GenericProduct.get [ gpkInt ] |> Array.tryHead with
                | Some gp -> gp.Name
                | None -> req.MedicationCodeableConcept.Text |> Option.defaultValue gpkCode
            | false, _ -> req.MedicationCodeableConcept.Text |> Option.defaultValue gpkCode

    // --- Route (dosageInstruction.route) ---
    let route =
        req.DosageInstruction
        |> List.tryHead
        |> Option.bind _.Route
        |> Option.map (fun cc ->
            match cc.Text with
            | Some t -> t
            | None ->
                cc.Coding
                |> List.tryFind (fun c -> c.System = FhirSystems.route)
                |> Option.map (fun c -> RouteMapping.toName c.Code)
                |> Option.defaultValue "")
        |> Option.defaultValue ""

    // --- Shape (Medication.form from contained resource) ---
    let shape =
        req.Contained
        |> List.tryHead
        |> Option.bind _.Form
        |> Option.bind _.Text
        |> Option.defaultValue ""

    // --- DoseType (inferred from dosage timing/rate structure) ---
    let doseType =
        req.DosageInstruction
        |> List.tryHead
        |> Option.map inferDoseType
        |> Option.defaultValue "Once"

    // --- Admin quantity (dosageInstruction.doseAndRate.doseQuantity) ---
    let adminQty, adminUnit =
        req.DosageInstruction
        |> List.tryHead
        |> Option.bind (fun d -> d.DoseAndRate |> List.tryHead)
        |> Option.bind _.Dose
        |> Option.map (fun q -> q.Value, q.Unit)
        |> Option.defaultValue (0m, "")

    // --- Schema (timing + rate) ---
    let schema =
        req.DosageInstruction
        |> List.tryHead
        |> Option.map extractSchema
        |> Option.defaultValue
            {
                Frequency = None
                TimePeriod = None
                TimeUnit = None
                RateQuantity = None
                RateFormUnit = None
                RateTimeUnit = None
                ExactTimes = []
            }

    // --- Products (from contained Medication.ingredient[]) ---
    // NOTE: In a real implementation the admin quantity would be distributed
    // across ingredients according to their proportions.
    let products =
        req.Contained
        |> List.collect (fun med ->
            med.Ingredient
            |> List.map (fun ing ->
                let gpk =
                    ing.ItemCodeableConcept.Coding
                    |> List.tryFind (fun c -> c.System = FhirSystems.gpk)
                    |> Option.map _.Code
                    |> Option.defaultValue ""

                let display =
                    ing.ItemCodeableConcept.Coding
                    |> List.tryHead
                    |> Option.bind _.Display
                    |> Option.defaultValue (ing.ItemCodeableConcept.Text |> Option.defaultValue "")

                {
                    GpkPlaceholder = gpk
                    Quantity = adminQty
                    Unit = adminUnit
                    Description = display
                }))

    {
        ScenarioId = req.Id |> Option.defaultValue ""
        Description = req.Note |> List.tryHead |> Option.defaultValue ""
        WeightKg = weightKg
        HeightCm = heightCm
        Gender = gender
        Indication = indication
        MedicationName = medicationName
        Route = route
        Shape = shape
        DoseType = doseType
        Products = products
        AdminQuantity = adminQty
        AdminUnit = adminUnit
        Schema = schema
    }


// ── GenPRES → FHIR translation ────────────────────────────────────────────────
//
// toFhirMedicationRequest: converts a GenPRES FhirScenario (populated after
// OrderContext lookup and order pipeline) into a FHIR R4 MedicationRequest.
//
// Translation logic:
//   - MedicationRequest.status: "active"
//   - MedicationRequest.intent: "order"
//   - MedicationRequest.medicationCodeableConcept: GPK code from ZIndex lookup
//   - MedicationRequest.reasonCode: scenario.Indication
//   - DosageInstruction.route: RouteMapping.toCode scenario.Route
//   - DosageInstruction.timing.repeat: Frequency / TimePeriod / TimeUnit / ExactTimes
//   - DosageInstruction.doseAndRate.doseQuantity: AdminQuantity + AdminUnit
//   - DosageInstruction.doseAndRate.rateRatio: RateFormUnit / RateTimeUnit / RateQuantity
//   - Contained Medication.form: scenario.Shape
//   - Contained Medication.ingredient: one entry per product

/// Build a FHIR Timing resource from an AdministrationSchema
let toFhirTiming (schema: AdministrationSchema) : FhirTiming option =
    let repeat =
        match schema.Frequency, schema.TimePeriod, schema.TimeUnit with
        | None, None, None when schema.ExactTimes |> List.isEmpty -> None
        | _ ->
            Some
                {
                    Frequency = schema.Frequency
                    Period = schema.TimePeriod
                    PeriodUnit = schema.TimeUnit |> Option.map PeriodUnitMapping.toFhir
                    Duration = None
                    DurationUnit = None
                    TimeOfDay = schema.ExactTimes
                }

    if repeat.IsSome || schema.ExactTimes |> List.isEmpty |> not then
        Some { Event = []; Repeat = repeat }
    else
        None


/// Build a FHIR DosageAndRate from an AdministrationSchema + admin quantity
let toFhirDosageAndRate (adminQty: decimal) (adminUnit: string) (schema: AdministrationSchema) : FhirDosageAndRate list =
    let dose =
        if adminQty > 0m then
            Some
                {
                    Value = adminQty
                    Unit = adminUnit
                    System = Some FhirSystems.ucum
                    Code = Some adminUnit
                }
        else
            None

    let rate =
        match schema.RateQuantity, schema.RateFormUnit, schema.RateTimeUnit with
        | Some qty, Some fu, Some tu ->
            Some(
                RateRatio
                    {
                        Numerator =
                            {
                                Value = qty
                                Unit = fu
                                System = Some FhirSystems.ucum
                                Code = Some fu
                            }
                        Denominator =
                            {
                                Value = 1m
                                Unit = tu
                                System = Some FhirSystems.ucum
                                Code = Some(PeriodUnitMapping.toFhir tu)
                            }
                    }
            )
        | _ -> None

    [ { Type = None; Dose = dose; Rate = rate } ]


/// Convert a GenPRES FhirScenario into a FHIR R4 MedicationRequest.
/// GPK code must be resolved beforehand (real GPK, not placeholder).
let toFhirMedicationRequest
    (resolvedGpkCode: string)
    (patientRef: string)
    (scenario: FhirScenario)
    : FhirMedicationRequest =

    let routeCode = RouteMapping.toCode scenario.Route

    let dosage =
        {
            Text = None
            Timing = toFhirTiming scenario.Schema
            Route =
                Some
                    {
                        Coding =
                            [
                                {
                                    System = FhirSystems.route
                                    Code = routeCode
                                    Display = Some scenario.Route
                                }
                            ]
                        Text = Some scenario.Route
                    }
            Method = None
            DoseAndRate = toFhirDosageAndRate scenario.AdminQuantity scenario.AdminUnit scenario.Schema
        }

    let medicationIngredients =
        scenario.Products
        |> List.map (fun p ->
            {
                ItemCodeableConcept =
                    {
                        Coding =
                            [
                                {
                                    System = FhirSystems.gpk
                                    Code = p.GpkPlaceholder
                                    Display = Some p.Description
                                }
                            ]
                        Text = Some p.Description
                    }
                IsActive = Some true
                // Strength would be populated from ZIndex lookup
                Strength = None
            })

    let containedMedication =
        {
            ResourceType = "Medication"
            Id = Some $"med-{scenario.ScenarioId}"
            Code =
                {
                    Coding =
                        [
                            {
                                System = FhirSystems.gpk
                                Code = resolvedGpkCode
                                Display = Some scenario.MedicationName
                            }
                        ]
                    Text = Some scenario.MedicationName
                }
            Form =
                Some
                    {
                        Coding = []
                        Text = Some scenario.Shape
                    }
            Ingredient = medicationIngredients
        }

    {
        ResourceType = "MedicationRequest"
        Id = Some $"req-{scenario.ScenarioId}"
        Status = "active"
        Intent = "order"
        MedicationCodeableConcept =
            {
                Coding =
                    [
                        {
                            System = FhirSystems.gpk
                            Code = resolvedGpkCode
                            Display = Some scenario.MedicationName
                        }
                    ]
                Text = Some scenario.MedicationName
            }
        Subject = { Reference = patientRef; Display = None }
        AuthoredOn = Some DateTime.UtcNow
        ReasonCode =
            [
                {
                    Coding = []
                    Text = Some scenario.Indication
                }
            ]
        Note = [ scenario.Description ]
        DosageInstruction = [ dosage ]
        DispenseRequest = None
        Contained = [ containedMedication ]
    }


// ── Demonstrate round-trip for each scenario ──────────────────────────────────

printfn "\n=== STEP 8: FHIR R4 Bidirectional Translation ==="
printfn """
Official FHIR R4 resource docs:
  MedicationRequest  https://hl7.org/fhir/R4/medicationrequest.html
  Medication         https://hl7.org/fhir/R4/medication.html
  Dosage             https://hl7.org/fhir/R4/dosage.html

Translation direction:
  A) FHIR MedicationRequest → FhirScenario (via fromFhirMedicationRequest)
       → set Filter on OrderContext → call getScenarios → look up ZIndex rules
       → apply orderable quantities from FHIR scenario → run order pipeline
  B) GenPRES OrderScenario → FHIR MedicationRequest (via toFhirMedicationRequest)
       → serialize to JSON for EHR / medication administration system

Key insight: FHIR provides the filter context and orderable quantities only.
Concentrations and dose limits are always derived from ZIndex/GenFORM, never
stored in the FHIR resource.
"""

printfn "--- Round-trip demonstration (scenario → FHIR → scenario) ---"

for scenario in allScenarios do
    printfn ""
    printfn "  Scenario %-7s %s" scenario.ScenarioId scenario.Description

    // A) GenPRES → FHIR
    let gpkPlaceholder =
        scenario.Products
        |> List.tryHead
        |> Option.map _.GpkPlaceholder
        |> Option.defaultValue ""

    let fhirReq = toFhirMedicationRequest gpkPlaceholder "Patient/DEMO" scenario

    printfn "    → FHIR MedicationRequest: id=%A status=%s intent=%s"
        fhirReq.Id
        fhirReq.Status
        fhirReq.Intent

    printfn "      medication: %s [GPK: %s]"
        (fhirReq.MedicationCodeableConcept.Text |> Option.defaultValue "")
        (fhirReq.MedicationCodeableConcept.Coding |> List.tryHead |> Option.map _.Code |> Option.defaultValue "")

    printfn "      indication: %s"
        (fhirReq.ReasonCode |> List.tryHead |> Option.bind _.Text |> Option.defaultValue "")

    printfn "      route: %s"
        (fhirReq.DosageInstruction |> List.tryHead |> Option.bind _.Route |> Option.bind _.Text |> Option.defaultValue "")

    let dosageText =
        fhirReq.DosageInstruction
        |> List.tryHead
        |> Option.map (fun d ->
            let doseStr =
                d.DoseAndRate
                |> List.tryHead
                |> Option.bind _.Dose
                |> Option.map (fun q -> $"{q.Value} {q.Unit}")
                |> Option.defaultValue "(no dose)"

            let rateStr =
                d.DoseAndRate
                |> List.tryHead
                |> Option.bind _.Rate
                |> Option.map (function
                    | RateRatio r -> $"{r.Numerator.Value} {r.Numerator.Unit}/{r.Denominator.Unit}"
                    | RateQuantity q -> $"{q.Value} {q.Unit}")
                |> Option.defaultValue "(no rate)"

            let freqStr =
                d.Timing
                |> Option.bind _.Repeat
                |> Option.map (fun r ->
                    match r.Frequency, r.PeriodUnit with
                    | Some f, Some u -> $"{f} x/{PeriodUnitMapping.toGenPres u}"
                    | _ -> "(continuous)")
                |> Option.defaultValue "(no schedule)"

            $"dose={doseStr} rate={rateStr} freq={freqStr}")
        |> Option.defaultValue "(no dosage)"

    printfn "      dosage: %s" dosageText

    // B) FHIR → FhirScenario (round-trip)
    let roundTripped =
        fromFhirMedicationRequest scenario.WeightKg scenario.HeightCm scenario.Gender fhirReq

    let routeMatch = roundTripped.Route = scenario.Route
    let doseTypeMatch = roundTripped.DoseType = scenario.DoseType
    let indicationMatch = roundTripped.Indication = scenario.Indication

    printfn "    ← Round-trip: Route=%s DoseType=%s Indication=%s"
        (if routeMatch then "✓" else $"✗ got '{roundTripped.Route}'")
        (if doseTypeMatch then "✓" else $"✗ got '{roundTripped.DoseType}'")
        (if indicationMatch then "✓" else $"✗ got '{roundTripped.Indication}'")


// ── Next implementation steps ─────────────────────────────────────────────────

printfn """

=== Next steps for Informedica.FHIR.Lib ===
  1. Add Hl7.Fhir.R4 NuGet package (paket: 'nuget Hl7.Fhir.R4')
  2. Replace the script FhirMedicationRequest type with the Hl7.Fhir.R4 model type
  3. Implement fromFhirMedicationRequest using the FhirScenario approach:
       MedicationRequest resource → FhirScenario → OrderContext filter → getScenarios
  4. Implement toFhirMedicationRequest:
       OrderScenario + orderable quantities → MedicationRequest resource
  5. Add JSON serialization using Hl7.Fhir.Serialization.FhirJsonParser
  6. Write Expecto tests for each scenario defined in this script
  7. Validate round-trip: fromFhirMedicationRequest (toFhirMedicationRequest scenario)
     preserves Route, DoseType, Indication, AdminQuantity, Rate, Frequency
"""
