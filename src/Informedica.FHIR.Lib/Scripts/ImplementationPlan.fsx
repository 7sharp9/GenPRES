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
// Source: docs/mdr/interface/genpres_interface_specification.md, sections 6.1–6.6
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
// STEP 8: Sketch a FHIR-based solution
// =============================================================================
//
// This section outlines how the full FHIR ↔ GenPRES round-trip would work.
//
// Key insight: the FHIR scenario provides the FILTER, not the dosing data.
// The dosing data (concentrations, dose limits) is returned by GenPRES
// after lookup against ZIndex/GenFORM rules.
//
// ── FHIR → GenPRES (import) ─────────────────────────────────────────────────
//
//   fromFhirRequest (MedicationRequest → OrderScenario):
//
//   FHIR field                               GenPRES field
//   ────────────────────────────────────────────────────────────────────────
//   Patient weight/height/age/gender     →   Patient record (for adjust calc)
//   dosageInstruction.route              →   Filter.Route
//   medication.form / ingredient         →   Filter.Form
//   dosageInstruction.timing intent      →   Filter.DoseType
//   medicationCodeableConcept (GPK)      →   Filter.Generic (via ZIndex lookup)
//   indication (condition reference)     →   Filter.Indication
//
//   After filter setup → call OrderContext.getScenarios → lookup concentrations
//                         and dose limits from ZIndex/GenFORM rules
//
//   Then apply from the FHIR scenario:
//     products[].quantity + unit          →  Component Orderable Quantities
//     administration quantity + unit      →  Orderable Dose Quantity
//     schema rate quantity + units        →  Orderable Dose Rate (RateFormUnit/RateTimeUnit)
//     schema frequency + period + unit    →  Schedule Frequency
//
// ── GenPRES → FHIR (export) ─────────────────────────────────────────────────
//
//   toFhirRequest (OrderScenario → MedicationRequest):
//
//   GenPRES field                            FHIR field
//   ────────────────────────────────────────────────────────────────────────
//   OrderScenario.Name (GPK lookup)      →   medicationCodeableConcept.coding[GPK]
//   OrderScenario.Route                  →   dosageInstruction.route (G-Standard thesaurus)
//   OrderScenario.Form                   →   medication.form
//   OrderScenario.DoseType               →   dosageInstruction.timing intent
//   Patient weight/height                →   Patient.extension (body weight)
//   Filter.Indication                    →   reasonCode
//   Component orderable quantities       →   ingredient[].amount
//   Orderable Dose Quantity              →   dosageInstruction.doseAndRate.doseQuantity
//   Orderable Dose Rate (RateFormUnit/   →   dosageInstruction.doseAndRate.rateRatio
//     RateTimeUnit)
//   Schedule Frequency + TimePeriod +    →   dosageInstruction.timing.repeat
//     TimeUnit
//   Schema.ExactTimes                    →   dosageInstruction.timing.repeat.timeOfDay
//
// Example FHIR JSON for scenario 6.3 (Paracetamol 4x/dag rectal):
//
//   {
//     "resourceType": "MedicationRequest",
//     "status": "active",
//     "intent": "order",
//     "medicationCodeableConcept": {
//       "coding": [{
//         "system": "urn:oid:2.16.840.1.113883.2.4.4.7",
//         "code": "<real-gpk-code>",
//         "display": "paracetamol zetpil"
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
//         "repeat": { "frequency": 4, "period": 1, "periodUnit": "d" }
//       }
//     }]
//   }
//
// Next implementation steps for Informedica.FHIR.Lib:
//   1. Add FHIR R4 serialization dependency (e.g. Hl7.Fhir.R4 NuGet package)
//   2. Implement fromFhirRequest using the mapping above
//   3. Implement toFhirRequest using the mapping above
//   4. Validate round-trip: fromFhirRequest (toFhirRequest scenario) ≈ original scenario
//   5. Write Expecto tests for each scenario defined in this script

printfn "\n=== STEP 8: FHIR-based Solution Approach ==="
printfn """
FHIR → GenPRES (import):
  1. Extract patient context → build Patient record
  2. Extract indication, route, form, dose type → set Filter
  3. Look up GPK code in ZIndex to get Filter.Generic
  4. Call OrderContext.create + getScenarios → concentrations/dose limits from ZIndex
  5. Apply orderable quantities, rate, and frequency from FHIR scenario data

GenPRES → FHIR (export):
  1. Map OrderScenario fields to MedicationRequest resource
  2. Map orderable quantities → ingredient amounts
  3. Map dose quantity → doseAndRate.doseQuantity
  4. Map rate (RateFormUnit/RateTimeUnit) → doseAndRate.rateRatio
  5. Map frequency/period → timing.repeat

See the comments in Step 8 above for the full field-level mapping.
"""
