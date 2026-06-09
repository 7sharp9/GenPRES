#I __SOURCE_DIRECTORY__

// Agent architecture demonstration for Informedica.ZForm.Lib
//
// This script shows how to wrap the ZForm (GStand) API in a Command/Response
// agent using Informedica.Agents.Lib.  It follows the script-based development
// workflow described in AGENTS.md — prototype here first, migrate to source
// once the design is approved.
//
// To run: from this directory
//   dotnet fsi Agent.fsx
//
// Pre-requisites: dotnet build GenPRES.sln

#load "load.fsx"

#r "../../Informedica.Agents.Lib/bin/Debug/net10.0/Informedica.Agents.Lib.dll"

open System

open Informedica.Utils.Lib
open Informedica.ZIndex.Lib
open Informedica.ZForm.Lib
open Informedica.Agents.Lib


// ============================================================
// Command / Response discriminated union types
// ============================================================

/// Configuration for a GStand dose-rule query
type GStandQueryConfig =
    {
        GPKs: int list
        IsRate: bool
        SubstanceUnit: string option
        TimeUnit: string option
    }

    static member Default =
        {
            GPKs = []
            IsRate = false
            SubstanceUnit = None
            TimeUnit = None
        }


/// Commands that can be sent to the ZForm agent
[<RequireQualifiedAccess>]
type ZFormCommand =
    /// Get all dose rules for a generic / form / route combination
    | GetDoseRules of config: GStandQueryConfig * generic: string * form: string * route: string
    /// Get dose rules restricted to a specific patient category
    | GetPatientDoseRules of
        config: GStandQueryConfig *
        patient: Types.PatientFilter *
        generic: string *
        form: string *
        route: string
    /// Get the per-substance dose breakdown for a patient
    | GetSubstanceDoses of
        config: GStandQueryConfig *
        patient: Types.PatientFilter *
        generic: string *
        form: string *
        route: string


/// Responses returned by the ZForm agent
[<RequireQualifiedAccess>]
type ZFormResponse =
    | DoseRules of DoseRule seq
    | SubstanceDoses of
        {|
            indications: string list
            dosage: Informedica.ZForm.Lib.Types.Dosage
        |} seq
    | Error of string


// ============================================================
// Agent state: ZForm is stateless by design — every query
// derives from the ZIndex data already on disk.  The agent
// state carries only the GStand configuration defaults.
// ============================================================

type ZFormState = unit // stateless — ZForm reads from ZIndex cache


// ============================================================
// Command processor
// ============================================================

let inline toGStandConfig (cfg: GStandQueryConfig) : Informedica.ZForm.Lib.Types.CreateConfig =
    {
        GPKs = cfg.GPKs
        IsRate = cfg.IsRate
        SubstanceUnit = cfg.SubstanceUnit |> Option.map Mapping.stringToUnit
        TimeUnit = cfg.TimeUnit |> Option.map Mapping.stringToUnit
    }


let processCommand (_state: ZFormState) (cmd: ZFormCommand) : ZFormResponse * ZFormState =
    let response =
        match cmd with
        | ZFormCommand.GetDoseRules(cfg, generic, form, route) ->
            GStand.createDoseRules (toGStandConfig cfg) None None None None generic form route
            |> ZFormResponse.DoseRules

        | ZFormCommand.GetPatientDoseRules(cfg, patient, generic, form, route) ->
            // RuleFinder returns ZIndex rules; GStand turns them into structured doses
            let rules =
                {
                    Patient = patient
                    Product =
                        Types.GenericFormRoute
                            {
                                Generic = generic
                                Form = form
                                Route = route
                            }
                }
                |> RuleFinder.find (cfg.GPKs)

            GStand.getSubstanceDoses (toGStandConfig cfg) rules
            |> Seq.map (fun sd ->
                // Wrap back into a DoseRule for a uniform response type
                // In practice the caller would use ZFormCommand.GetSubstanceDoses
                sd
            )
            |> ignore
            // Return dose rules via the standard path instead
            GStand.createDoseRules
                (toGStandConfig cfg)
                patient.Age
                patient.Weight
                None
                None
                generic
                form
                route
            |> ZFormResponse.DoseRules

        | ZFormCommand.GetSubstanceDoses(cfg, patient, generic, form, route) ->
            let rules =
                {
                    Patient = patient
                    Product =
                        Types.GenericFormRoute
                            {
                                Generic = generic
                                Form = form
                                Route = route
                            }
                }
                |> RuleFinder.find (cfg.GPKs)

            GStand.getSubstanceDoses (toGStandConfig cfg) rules
            |> ZFormResponse.SubstanceDoses

    response, () // state is unit, always unchanged


// ============================================================
// Agent factory
// ============================================================

/// Create and start a ZForm agent.
let createZFormAgent () =
    Agent.createStatefulReply<ZFormCommand, ZFormResponse, ZFormState> ((), processCommand)


// ============================================================
// Helper
// ============================================================

let ask cmd (agent: Agent<ZFormCommand * AsyncReplyChannel<ZFormResponse>>) = agent |> Agent.postAndReply cmd


// ============================================================
// Demo — exercise the agent
// ============================================================

printfn "\n--- ZForm Agent Demo ---\n"

Environment.SetEnvironmentVariable("GENPRES_PROD", "0")
FilePath.useDemo () |> ignore

let agent = createZFormAgent ()

let cfg = GStandQueryConfig.Default


// 1. Get dose rules for paracetamol suppository / rectal route
printfn "1. Paracetamol suppository (rectaal) dose rules:"

match
    agent
    |> ask (ZFormCommand.GetDoseRules(cfg, "paracetamol", "zetpil", "rectaal"))
with
| ZFormResponse.DoseRules rules ->
    let ruleList = rules |> Seq.toArray
    printfn $"   Found {ruleList.Length} dose rule(s)"

    ruleList
    |> Array.truncate 2
    |> Array.iter (fun dr -> printfn $"   {DoseRule.toString false dr}")
| ZFormResponse.Error msg -> printfn $"   Error: {msg}"
| _ -> printfn "   Unexpected response"


// 2. Get substance doses for a specific patient
printfn "\n2. Substance doses for a 10 kg, 4-year-old patient:"

let patient: Types.PatientFilter =
    {
        Age = Some 4. // years
        Weight = Some 10. // kg
        BSA = None
    }

match
    agent
    |> ask (ZFormCommand.GetSubstanceDoses(cfg, patient, "paracetamol", "zetpil", "rectaal"))
with
| ZFormResponse.SubstanceDoses doses ->
    let doseList = doses |> Seq.toArray
    printfn $"   Found {doseList.Length} substance dose(s)"
    doseList |> Array.truncate 3 |> Array.iter (fun sd -> printfn $"   {sd}")
| ZFormResponse.Error msg -> printfn $"   Error: {msg}"
| _ -> printfn "   Unexpected response"


// 3. Dispose
Agent.dispose agent
printfn "\nAgent disposed."


// ============================================================
// Notes for migration to source files
// ============================================================
//
// When migrating to .fs source files:
//
// 1.  Add Agent.fs to Informedica.ZForm.Lib containing:
//         - GStandQueryConfig (record)
//         - ZFormCommand (DU)
//         - ZFormResponse (DU)
//         - processCommand
//         - create (unit -> Agent<...>)
//
// 2.  Add ProjectReference to Informedica.Agents.Lib in ZForm.Lib.fsproj
