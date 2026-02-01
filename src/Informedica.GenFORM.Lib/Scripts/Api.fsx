
#load "load.fsx"

#r "../bin/Debug/net10.0/Informedica.GenForm.Lib.dll"


open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources

#time


System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")


let provider : IResourceProvider =
        Api.getCachedProviderWithDataUrlId
            FormLogging.noOp
            "1JHOrasAZ_2fcVApYpt1qT2lZBsqrAxN-9SvBisXkbsM"


provider.GetResourceInfo () |> ignore


provider
|> Api.getDoseRules
|> Array.collect _.ComponentLimits
|> Array.collect _.Products
|> Array.filter (fun p ->
    p.Generic = "piperacilline/tazobactam" && p.Form = "poeder voor injectievloeistof"
)
|> Array.skip 0
|> Array.head
|> Product.reconstitute
    (provider.GetRouteMappings ())
    None
    (Some "ICK")
    "INTRAVENEUS"
