/// # FHIR Medication Interface Implementation Plan
///
/// This script implements the plan described in docs/mdr/interface/genpres_interface_specification.md
/// and follows the steps outlined in the GenPRES FHIR integration issue.
///
/// ## Steps
///
/// 1. Define the FHIR scenarios from the specification
/// 2. Look up product information from GPK product codes via ZIndex
/// 3. Parse quantitative variables to ValueUnits
/// 4. Translate each scenario to the Medication string representation
/// 5. Run each scenario through Medication.fromString as an order
/// 6. Print the results
/// 7. Sketch a FHIR-based translation approach

#load "load.fsx"

open System
open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib


// =============================================================================
// STEP 1: Define the FHIR scenarios from the specification
// =============================================================================
//
// Source: docs/mdr/interface/genpres_interface_specification.md, sections 6.1–6.6
//
// NOTE: The GPK codes in the specification are PLACEHOLDERS (e.g. "2345678").
// Real codes must come from the G-Standard database. See Step 2 for product lookup.

/// Represents a product as described in the FHIR scenario YAML
type ScenarioProduct =
    {
        // Placeholder GPK code from the spec (not a real G-Standard code)
        GpkPlaceholder: string
        // Amount of product used
        Quantity: decimal
        Unit: string
        // Human-readable description from the spec comment
        Description: string
    }


/// Represents the administration schedule from the FHIR YAML schema block
type AdministrationSchema =
    {
        // Number of times per period (null for continuous)
        Frequency: int option
        // Duration of the period (e.g. 1 for per day, 36 for per 36 hours)
        TimePeriod: decimal option
        // Unit of the period (e.g. "dag", "uur")
        TimeUnit: string option
        // Infusion rate quantity
        RateQuantity: decimal option
        // Rate unit numerator (e.g. "mL")
        RateUnit1: string option
        // Rate unit denominator (e.g. "uur")
        RateUnit2: string option
        // Exact administration times (for timed orders)
        ExactTimes: string list
    }


/// Represents a FHIR scenario as described in section 6 of the specification
type FhirScenario =
    {
        // Scenario identifier (matches section number)
        ScenarioId: string
        // Short descriptive name
        Description: string
        // Patient weight in kg
        WeightKg: decimal
        // Patient height in cm
        HeightCm: decimal
        // Gender ("male" / "female")
        Gender: string
        // Clinical indication
        Indication: string
        // Generic medication name
        MedicationName: string
        // Administration route (Dutch G-Standard term)
        Route: string
        // Pharmaceutical form (Dutch)
        Shape: string
        // Order type: "Once", "OnceTimed", "Discontinuous", "Timed", "Continuous"
        DoseType: string
        // Products from the YAML products block
        Products: ScenarioProduct list
        // Administration quantity and unit
        AdminQuantity: decimal
        AdminUnit: string
        // Scheduling
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
                RateUnit1 = None
                RateUnit2 = None
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
                RateUnit1 = Some "mL"
                RateUnit2 = Some "uur"
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
                RateUnit1 = None
                RateUnit2 = None
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
                RateUnit1 = None
                RateUnit2 = None
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
                RateUnit1 = Some "mL"
                RateUnit2 = Some "uur"
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
                RateUnit1 = Some "mL"
                RateUnit2 = Some "uur"
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
                RateUnit1 = Some "mL"
                RateUnit2 = Some "uur"
                ExactTimes = []
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
    ]


// =============================================================================
// STEP 2: Look up product information from GPK codes via ZIndex
// =============================================================================
//
// NOTE: The GPK codes in the spec are PLACEHOLDERS. A real implementation would
// use ZIndex.GenericProduct.get [gpkCode] to resolve a product by its actual
// G-Standard GPK code. Here we demonstrate the lookup API by searching for
// products by their generic name, which is what the spec intends.

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


/// Lookup all products for a given generic medication name using ZIndex
let lookupByGenericName (name: string) =
    GenericProduct.get []
    |> Array.filter (fun gp -> gp.Name |> String.equalsCapInsens name)


/// Demonstrate GPK lookup for the medications in our scenarios
let demonstrateProductLookup () =
    printfn "\n=== STEP 2: Product Lookup via ZIndex ==="
    printfn "NOTE: Spec uses placeholder GPK codes (e.g. '2345678')."
    printfn "Real lookup uses ZIndex.GenericProduct.get [gpkCode].\n"

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
// Demonstrate how the scenario quantities translate to GenUnits ValueUnit values.
// These are the same unit types used in the Medication text format.

let demonstrateValueUnitParsing () =
    printfn "\n=== STEP 3: Parsing Quantitative Variables to ValueUnits ==="

    // Weight adjust unit
    let weight19_5kg = 19.5m |> BigRational.fromDecimal |> ValueUnit.singleWithUnit Units.Weight.kiloGram
    printfn "Patient weight:  %s" (weight19_5kg |> ValueUnit.toStringDecimalDutchShort)

    // Suppository quantity
    let stuk = Units.General.general "stuk"
    let qty1stuk = 1N |> ValueUnit.singleWithUnit stuk
    printfn "1 stuk:          %s" (qty1stuk |> ValueUnit.toStringDecimalDutchShort)

    // Concentration: 750 mg/stuk
    let conc750mgStuk =
        750N
        |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> Units.per stuk)

    printfn "750 mg/stuk:     %s" (conc750mgStuk |> ValueUnit.toStringDecimalDutchShort)

    // IV concentration: 10 mg/mL
    let conc10mgMl =
        10N
        |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> Units.per Units.Volume.milliLiter)

    printfn "10 mg/mL:        %s" (conc10mgMl |> ValueUnit.toStringDecimalDutchShort)

    // Infusion rate: 85 mL/uur
    let rate85mlHr =
        85N
        |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Time.hour)

    printfn "85 mL/uur:       %s" (rate85mlHr |> ValueUnit.toStringDecimalDutchShort)

    // Dose adjust: 40 mg/kg/dose (quantity-adjust, no time)
    let doseAdj40 =
        40N
        |> ValueUnit.singleWithUnit (
            Units.Mass.milliGram
            |> Units.per Units.Weight.kiloGram
        )

    printfn "40 mg/kg/dose:   %s" (doseAdj40 |> ValueUnit.toStringDecimalDutchShort)

    // Dose adjust: 3 mg/kg/uur (rate-adjust, with time)
    let rateAdj3 =
        3N
        |> ValueUnit.singleWithUnit (
            Units.Mass.milliGram
            |> Units.per Units.Weight.kiloGram
            |> Units.per Units.Time.hour
        )

    printfn "3 mg/kg/uur:     %s" (rateAdj3 |> ValueUnit.toStringDecimalDutchShort)

    // Frequency: 4 x/dag
    let freq4xDay =
        [| 4N |] |> ValueUnit.withUnit (Units.Count.times |> Units.per Units.Time.day)

    printfn "4 x/dag:         %s" (freq4xDay |> ValueUnit.toStringDecimalDutchShort)

    // Per-time dose: 10–20 mg/kg/dose (quantity-adjust MinMax)
    let doseMin = 10N |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> Units.per Units.Weight.kiloGram)
    let doseMax = 20N |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> Units.per Units.Weight.kiloGram)
    let doseRange = MinMax.createInclIncl doseMin doseMax

    printfn "10–20 mg/kg:     %s" (doseRange |> MinMax.toString ValueUnit.toStringDecimalDutchShort ValueUnit.toStringDecimalDutchShort "min " "min " "max " "max ")

    printfn ""


demonstrateValueUnitParsing ()


// =============================================================================
// STEP 4-5: Translate each scenario to the Medication string representation
// =============================================================================
//
// The Medication string format is the canonical text representation used by
// Medication.fromString / Medication.toString.
// See: src/Informedica.GenORDER.Lib/Medication.fs
//
// This step shows how each FHIR scenario maps to that format.

/// Map FHIR DoseType string to the GenPRES OrderType token used in the Medication text format
let doseTypeToOrderType =
    function
    | "Once" -> "OnceOrder"
    | "OnceTimed" -> "OnceTimedOrder"
    | "Discontinuous" -> "DiscontinuousOrder"
    | "Timed" -> "TimedOrder"
    | "Continuous" -> "ContinuousOrder"
    | other -> failwith $"Unknown DoseType: {other}"


/// Build the Frequencies field string for a scenario schema
let buildFrequenciesString (schema: AdministrationSchema) =
    match schema.Frequency, schema.TimeUnit with
    | Some freq, Some unit ->
        let period = schema.TimePeriod |> Option.defaultValue 1m

        if period = 1m then
            $"%i{freq} x/{unit}"
        else
            $"%i{freq} x/{period} {unit}"
    | _ -> ""


/// Build the Time (infusion duration) field string for an OnceTimed or Timed scenario.
/// Computes duration = adminQuantity / rate when both are available.
let buildTimeString (adminQuantityMl: decimal) (schema: AdministrationSchema) =
    match schema.RateQuantity, schema.RateUnit1, schema.RateUnit2 with
    | Some rate, Some _, Some _ when rate > 0m ->
        // Duration in hours = volume / rate; convert to minutes
        let durationMin = adminQuantityMl / rate * 60m
        let durationMin = System.Math.Ceiling(float durationMin) |> int
        // Provide a small time window around the computed duration (±25%)
        let minTime = max 1 (durationMin - durationMin / 4)
        let maxTime = durationMin + durationMin / 4
        $"%i{minTime} min - %i{maxTime} min"
    | _ -> ""


/// Translate a FhirScenario into the Medication text string that can be parsed by Medication.fromString.
///
/// This function creates a Medication text template based on the scenario metadata.
/// The concentrations and dose limits are populated with example values that reflect
/// the clinical intent described in the scenario.
let scenarioToMedicationText (id: string) (scenario: FhirScenario) : string =
    let orderType = scenario.DoseType |> doseTypeToOrderType

    let adjustStr =
        $"{scenario.WeightKg} kg"

    let frequenciesStr = buildFrequenciesString scenario.Schema
    let timeStr = buildTimeString scenario.AdminQuantity scenario.Schema

    // Build component text based on the products in the scenario
    let componentText =
        scenario.Products
        |> List.mapi (fun i product ->
            // Determine concentration and dose fields from clinical context
            let (concentrationStr, doseStr) =
                match scenario.MedicationName, scenario.DoseType with
                | "paracetamol", "Once" when scenario.Route = "RECTAAL" ->
                    "120;240;500;1000;125;250;60;30;360;90;750;180 mg/stuk",
                    "paracetamol, [dun] mg, [qty-adj] 40 mg/kg/dosis, [qty] max 1000 mg/dosis"

                | "paracetamol", "OnceTimed" ->
                    "10 mg/ml",
                    "paracetamol, [dun] mg, [qty-adj] 20 mg/kg/dosis, [qty] max 1000 mg/dosis"

                | "paracetamol", "Discontinuous" when scenario.Route = "RECTAAL" ->
                    "120;240;500;1000;125;250;60;30;360;90;750;180 mg/stuk",
                    "paracetamol, [dun] mg, [qty-adj] 10 mg/kg - 20 mg/kg/dosis, [qty] max 1000 mg/dosis"

                | "paracetamol", ("Timed" | "Discontinuous") ->
                    "10 mg/ml",
                    "paracetamol, [dun] mg, [per-time-adj] 60 mg/kg/dag, [qty] max 1000 mg/dosis"

                | "gentamicine", _ ->
                    "1;2;4 mg/ml",
                    "gentamicine, [dun] mg, [qty-adj] 5 mg/kg/dosis, [qty] max 500 mg/dosis"

                | "propofol", _ ->
                    "10 mg/ml",
                    "propofol, [dun] mg, [rate-adj] 1 mg/kg/uur - 4 mg/kg/uur"

                | "noradrenaline", _ when i = 0 ->
                    "1 mg/ml",
                    "noradrenaline, [dun] microg, [rate-adj] 0,05 microg/kg/min - 2 microg/kg/min"

                | "noradrenaline", _ ->
                    // Second component: glucose 10% diluent (no active substance dose)
                    "100 mg/ml", ""

                | _ ->
                    "1 mg/ml", ""

            // Component names
            let compName =
                if scenario.Products.Length > 1 && i = 1 then
                    "gluc 10%"
                else
                    scenario.MedicationName

            let quantityStr =
                match product.Unit with
                | "stuk" -> $"1 stuk"
                | "mL" ->
                    // Show available container sizes (from known product catalogue)
                    match scenario.MedicationName with
                    | "paracetamol" -> "50;100 ml"
                    | "gentamicine" -> "1 ml"
                    | "propofol" -> "50;200 ml"
                    | "noradrenaline" when i = 0 -> "5;10;20 ml"
                    | _ -> "50 ml"
                | u -> $"1 {u}"

            let divisibleStr =
                match product.Unit with
                | "stuk" -> "1"
                | "mL" -> "10"
                | _ -> "1"

            let substanceDoseStr =
                if doseStr = "" then "Dose:" else $"Dose: %s{doseStr}"

            $"""
\tName: %s{compName}
\tForm: %s{scenario.Shape}
\tQuantities: %s{quantityStr}
\tDivisible: %s{divisibleStr}
\tDose:
\tSolution:
\tSubstances:

\t\tName: %s{compName}
\t\tQuantities:
\t\tConcentrations: %s{concentrationStr}
\t\t%s{substanceDoseStr}
\t\tSolution:"""
        )
        |> String.concat "\n"

    // Determine the top-level Dose field
    let topDoseStr =
        match scenario.DoseType with
        | "Once" when scenario.Route = "RECTAAL" ->
            "[dun], [qty] 1 stuk/dosis"
        | "OnceTimed" ->
            "[dun], [qty-adj] max 20 ml/kg/dosis, [qty] max 1000 ml/dosis"
        | "Continuous" ->
            ""
        | _ -> ""

    $"""Id: %s{id}
Name: %s{scenario.MedicationName}
Quantity:
Quantities:
Route: %s{scenario.Route}
OrderType: %s{orderType}
Adjust: %s{adjustStr}
Frequencies: %s{frequenciesStr}
Time: %s{timeStr}
Dose: %s{topDoseStr}
Div:
DoseCount: 1 x
Components:%s{componentText}"""


// Build all scenario texts
let scenarioMedicationTexts =
    allScenarios
    |> List.map (fun scenario ->
        let guid = Guid.NewGuid().ToString()
        scenario, scenarioToMedicationText guid scenario
    )


printfn "\n=== STEP 4: Medication Text Representations ==="

for scenario, text in scenarioMedicationTexts do
    printfn "\n--- Scenario %s: %s ---" scenario.ScenarioId scenario.Description
    printfn "%s" text


// =============================================================================
// STEP 6: Run each scenario through Medication.fromString
// =============================================================================

printfn "\n=== STEP 5-6: Running Scenarios through Medication.fromString ==="

let runScenario (scenario: FhirScenario) (medText: string) =
    printfn "\n--- Scenario %s: %s ---" scenario.ScenarioId scenario.Description
    printfn "Patient: %M kg, %M cm, %s" scenario.WeightKg scenario.HeightCm scenario.Gender
    printfn "Indication: %s" scenario.Indication

    match Medication.fromString medText with
    | Error errors ->
        printfn "PARSE ERROR:"
        errors |> List.iter (printfn "  - %s")

    | Ok med ->
        printfn "Parsed OK: %s (%A)" med.Name med.OrderType

        // Convert to order DTO and build the order
        match med |> Medication.toOrderDto |> Order.Dto.fromDto with
        | Error exn ->
            printfn "ORDER BUILD ERROR: %A" exn

        | Ok order ->
            printfn "Order built OK."

            // Solve the order to propagate constraints
            let solved =
                order
                |> Order.solveMinMax "ImplementationPlan" true OrderLogging.noOp

            match solved with
            | Error(_, msg) ->
                printfn "SOLVE WARNING: %s (partial result may still be valid)" msg
                // Print the partial order anyway
                order |> Order.printTable ConsoleTables.Format.Minimal

            | Ok solvedOrder ->
                printfn "Solved OK."
                solvedOrder |> Order.printTable ConsoleTables.Format.Minimal


for scenario, medText in scenarioMedicationTexts do
    runScenario scenario medText


// =============================================================================
// STEP 7: Print summary of results
// =============================================================================

printfn "\n=== STEP 7: Summary ==="
printfn "Processed %i FHIR scenarios from the interface specification." (List.length allScenarios)
printfn ""

for scenario, medText in scenarioMedicationTexts do
    let result =
        medText
        |> Medication.fromString
        |> Result.bind (fun med ->
            med
            |> Medication.toOrderDto
            |> Order.Dto.fromDto
            |> Result.mapError (fun exn -> [ $"{exn}" ])
        )

    let status =
        match result with
        | Ok _ -> "OK"
        | Error errs -> $"ERROR: {errs |> String.concat '; '}"

    printfn "  Scenario %-6s %-50s %s" scenario.ScenarioId scenario.Description status


// =============================================================================
// STEP 8: Sketch a FHIR-based translation approach
// =============================================================================
//
// This section outlines how the GenPRES Medication model would be represented
// in FHIR R4 MedicationRequest resources.
//
// The FHIR MedicationRequest resource encodes the same information as the
// Medication text format, but in a structured, interoperable form.
//
// Key FHIR resources used:
//   - MedicationRequest  : the prescription order (one per scenario)
//   - Medication         : the medication product identified by GPK code
//   - Dosage             : frequency, timing, route, and dose quantity
//   - DosageInstruction  : rate for continuous/timed infusion orders
//
// Mapping outline:
//
//   FhirScenario.MedicationName         → MedicationRequest.medication.coding (GPK system)
//   FhirScenario.Route                  → MedicationRequest.dosageInstruction.route (G-Standard thesaurus)
//   FhirScenario.WeightKg               → MedicationRequest.dosageInstruction.doseAndRate (weight-based)
//   FhirScenario.Schema.Frequency       → MedicationRequest.dosageInstruction.timing.repeat.frequency
//   FhirScenario.Schema.TimeUnit        → MedicationRequest.dosageInstruction.timing.repeat.periodUnit
//   FhirScenario.Schema.RateQuantity    → MedicationRequest.dosageInstruction.doseAndRate.rateRatio
//   FhirScenario.Schema.ExactTimes      → MedicationRequest.dosageInstruction.timing.repeat.timeOfDay
//
// Example FHIR JSON skeleton for scenario 6.3 (Paracetamol 4x/dag rectal):
//
//   {
//     "resourceType": "MedicationRequest",
//     "id": "<uuid>",
//     "status": "active",
//     "intent": "order",
//     "medicationCodeableConcept": {
//       "coding": [{
//         "system": "urn:oid:2.16.840.1.113883.2.4.4.7",   // G-Standard GPK
//         "code": "<real-gpk-code>",
//         "display": "paracetamol 180 mg/stuk zetpil"
//       }]
//     },
//     "subject": { "reference": "Patient/123456" },
//     "dosageInstruction": [{
//       "route": {
//         "coding": [{ "system": "urn:oid:2.16.840.1.113883.2.4.4.9", "code": "12" }],
//         "text": "RECTAAL"
//       },
//       "doseAndRate": [{
//         "doseQuantity": { "value": 1, "unit": "stuk" }
//       }],
//       "timing": {
//         "repeat": {
//           "frequency": 4,
//           "period": 1,
//           "periodUnit": "d"
//         }
//       }
//     }]
//   }
//
// Reverse mapping (FHIR → GenPRES Medication text):
//
//   The fromFhirRequest function (to be implemented in Informedica.FHIR.Lib) would:
//   1. Extract the GPK code from medicationCodeableConcept.coding
//   2. Call ZIndex.GenericProduct.get [gpkCode] to resolve concentration data
//   3. Map timing.repeat.frequency + periodUnit → Frequencies ValueUnit
//   4. Map doseAndRate.rateRatio → Rate ValueUnit (for continuous orders)
//   5. Map subject.weight (from Patient resource) → Adjust ValueUnit
//   6. Construct a Medication record and call Medication.toOrderDto
//   7. Return the solved order

printfn "\n=== STEP 8: FHIR-based Translation Approach ==="
printfn """
The Informedica.FHIR.Lib library will provide two main functions:

  toFhirRequest   : Medication → FHIR MedicationRequest JSON
  fromFhirRequest : FHIR MedicationRequest JSON → Result<Medication, string list>

The mapping between the GenPRES Medication text format and FHIR R4 MedicationRequest
is defined as follows:

  GenPRES field        FHIR field
  ─────────────────────────────────────────────────────────────────────────────
  Name                 medicationCodeableConcept.coding[GPK].display
  Route                dosageInstruction.route.coding[G-Standard thesaurus]
  OrderType            dosageInstruction.timing + intent
  Adjust (weight)      dosageInstruction.doseAndRate.doseQuantity (per kg)
  Frequencies          dosageInstruction.timing.repeat.frequency + periodUnit
  Time (infusion)      dosageInstruction.timing.repeat.duration + durationUnit
  Component.Concentration  Medication.ingredient.strength.numerator
  Substance.Dose[qty-adj]  dosageInstruction.doseAndRate.doseRange (weight-based)
  Substance.Dose[rate-adj] dosageInstruction.doseAndRate.rateRatio
  Schema.ExactTimes    dosageInstruction.timing.repeat.timeOfDay

For multi-product scenarios (6.6+), each component maps to a separate
Medication.ingredient entry, with the primary product identified by GPK code and
diluents identified by their own GPK codes.

Next implementation steps for Informedica.FHIR.Lib:
  1. Add a FHIR R4 serialization dependency (e.g. Hl7.Fhir.R4 NuGet package)
  2. Implement toFhirRequest using the mapping table above
  3. Implement fromFhirRequest using ZIndex GPK lookup + Medication.fromString
  4. Validate round-trip: fromFhirRequest (toFhirRequest medication) = Ok medication
  5. Write Expecto tests for each scenario defined in this script
"""
