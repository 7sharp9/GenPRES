
#I __SOURCE_DIRECTORY__

#load "load.fsx"

#time

open System

open Informedica.Utils.Lib
open Informedica.GenForm.Lib


Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable(Informedica.Utils.Lib.BCL.FilePath.GENPRES_PROD, "1")


// Load all dose rules
let provider = Resources.createResourceProvider ()
provider.LoadResources() |> ignore


let doseRules = provider.DoseRules


// List all unique indications
let indications =
    doseRules
    |> Array.map _.Indication
    |> Array.distinct
    |> Array.sort


printfn "\n=== All Indications ==="
indications |> Array.iter (printfn "  %s")


// List all unique generics
let generics =
    doseRules
    |> Array.map _.Generic
    |> Array.distinct
    |> Array.sort


printfn "\n=== All Generics ==="
generics |> Array.iter (printfn "  %s")


// Find nutrition-related indications and generics
let nutritionIndications =
    indications
    |> Array.filter (fun i ->
        i.Contains("Parenter", StringComparison.OrdinalIgnoreCase) ||
        i.Contains("Enter", StringComparison.OrdinalIgnoreCase) ||
        i.Contains("Voeding", StringComparison.OrdinalIgnoreCase) ||
        i.Contains("Nutrition", StringComparison.OrdinalIgnoreCase) ||
        i.Contains("suppletie", StringComparison.OrdinalIgnoreCase) ||
        i.Contains("lipid", StringComparison.OrdinalIgnoreCase) ||
        i.Contains("glucose", StringComparison.OrdinalIgnoreCase) ||
        i.Contains("elektrolyt", StringComparison.OrdinalIgnoreCase)
    )


printfn "\n=== Nutrition-related Indications ==="
nutritionIndications |> Array.iter (printfn "  %s")


// For each nutrition indication, list associated generics
for ind in nutritionIndications do
    let gens =
        doseRules
        |> Array.filter (fun dr -> dr.Indication = ind)
        |> Array.map _.Generic
        |> Array.distinct
        |> Array.sort
    printfn $"\n=== Generics for '%s{ind}' ==="
    gens |> Array.iter (printfn "  %s")
