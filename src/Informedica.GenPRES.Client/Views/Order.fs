namespace Views



module Order =

    open Fable.Core
    open Fable.React
    open Feliz
    open Shared.Types
    open Shared.Models.Order
    open Shared
    open Elmish
    open Utils
    open FSharp.Core


    module private Elmish =


        type State =
            {
                Order : Order option
                SelectedComponent : string option
                SelectedItem : string option
            }

        type Msg =
            | ChangeComponent of string option
            | ChangeComponentOrderableQuantity of string option
            | ChangeComponentDoseQuantity of string option
            | ChangeItem of string option
            | ChangeFrequency of string option
            | ChangeTime of string option
            | ChangeSubstanceDoseQuantity of string option
            | ChangeSubstanceDoseQuantityAdjust of string option
            | ChangeSubstancePerTime of string option
            | ChangeSubstancePerTimeAdjust of string option
            | ChangeSubstanceRate of string option
            | ChangeSubstanceRateAdjust of string option
            | ChangeSubstanceComponentConcentration of cmp: string * sbst: string * string option
            | ChangeSubstanceOrderableConcentration of string option
            | ChangeSubstanceOrderableQuantity of string option
            | ChangeOrderableDoseQuantity of string option
            | ChangeOrderableDoseRate of string option
            | ChangeOrderableQuantity of string option
            | UpdateOrderScenario of Order
            | ResetOrderScenario
            // Frequency property commands
            | DecreaseFrequencyProperty
            | IncreaseFrequencyProperty
            | SetMinFrequencyProperty 
            | SetMaxFrequencyProperty
            | SetMedianFrequencyProperty
            // Dose Quantity property commands
            | DecreaseDoseQuantityProperty of ntimes: int
            | IncreaseDoseQuantityProperty of ntimes: int
            | SetMinDoseQuantityProperty 
            | SetMaxDoseQuantityProperty 
            | SetMedianDoseQuantityProperty
            // Rate property commands
            | DecreaseDoseRateProperty of ntimes: int
            | IncreaseDoseRateProperty of ntimes: int
            | SetMinDoseRateProperty
            | SetMaxDoseRateProperty
            | SetMedianDoseRateProperty
            // Component Quantity property commands
            | DecreaseComponentQuantityProperty of ntimes: int
            | IncreaseComponentQuantityProperty of ntimes: int
            | SetMinComponentQuantityProperty
            | SetMaxComponentQuantityProperty
            | SetMedianComponentQuantityProperty


        let init (ctx : Deferred<OrderContext>) =
            let ord, cmp, itm =
                match ctx with
                | Resolved ctx ->
                    match ctx.Scenarios with
                    | [| sc |] ->

                        let ord = sc.Order
                        let cmp = sc.Component
                        let itm = sc.Item

                        match ord.Orderable.Components with
                        | [||] -> Some ord, None, None
                        | _ ->
                            ord.Orderable.Components
                            |> Array.tryFind (fun c ->
                                cmp.IsNone ||
                                c.Name = cmp.Value
                            )
                            |> Option.map (fun c ->
                                // only use substances that are not additional
                                let substs =
                                    c.Items
                                    |> Array.filter (_.IsAdditional >> not)

                                if substs |> Array.isEmpty then
                                    Some ord,
                                    Some c.Name,
                                    None
                                else
                                    let s =
                                        substs
                                        |> Array.tryFind (fun i -> i.Name |> Some = itm)
                                        |> Option.map (fun s -> s.Name)
                                        |> Option.defaultValue (substs[0].Name)
                                        |> Some
                                    Some ord,
                                    Some c.Name,
                                    s
                            )
                            |> Option.defaultValue (Some ord, None, None)

                    | _ -> None, None, None

                | _ -> None, None, None

            {
                SelectedComponent = cmp
                SelectedItem = itm
                Order = ord
            }
            , Cmd.none


        let update
            updateOrderScenario
            resetOrderScenario
            (navigate : 
                {| 
                    setFreqMin : OrderLoader -> unit
                    setFreqDec : OrderLoader -> unit
                    setFreqMed : OrderLoader -> unit 
                    setFreqInc : OrderLoader -> unit
                    setFreqMax : OrderLoader -> unit

                    setRateMin : OrderLoader -> unit
                    setRateDec : OrderLoader -> unit
                    setRateMed : OrderLoader -> unit 
                    setRateInc : OrderLoader -> unit
                    setRateMax : OrderLoader -> unit

                    setDoseQtyMin : OrderLoader -> unit
                    setDoseQtyDec : OrderLoader -> unit
                    setDoseQtyMed : OrderLoader -> unit 
                    setDoseQtyInc : OrderLoader -> unit
                    setDoseQtyMax : OrderLoader -> unit

                    setComponentQtyMin : OrderLoader -> unit
                    setComponentQtyDec : OrderLoader -> unit
                    setComponentQtyMed : OrderLoader -> unit 
                    setComponentQtyInc : OrderLoader -> unit
                    setComponentQtyMax : OrderLoader -> unit

                |})
            (msg: Msg)
            (state : State) : State * Cmd<Msg>
            =
            let setVu s (vu : Types.ValueUnit option) =
                match vu with
                | Some vu ->
                    { vu with
                        Value =
                            vu.Value
                            |> Array.tryFind (fun (v, _) ->
                                let b = v = (s |> Option.defaultValue "")
                                if not b then Logging.warning "couldn't find" s
                                b
                            )
                            |> Option.map Array.singleton
                            |> Option.defaultValue vu.Value
                    } |> Some
                | None -> None

            let setVar (s : string option) (var : Variable) =
                { var with
                    IsNonZeroPositive = s.IsNone
                    Vals =
                        if s.IsNone then None
                        else var.Vals |> setVu s
                }

            let setOvar s (ovar: OrderVariable) =
                { ovar with Variable = ovar.Variable |> setVar s }

            let handleNav nav =
                match state.Order with
                | None -> state, Cmd.none
                | Some ord -> 
                    // dispatch to parent
                    OrderLoader.create state.SelectedComponent state.SelectedItem ord
                    |> nav
                    // return awaiting updated order
                    { state with
                        Order = None
                    }
                    , Cmd.none

            match msg with

            | UpdateOrderScenario ord ->

                OrderLoader.create state.SelectedComponent state.SelectedItem ord
                |> updateOrderScenario

                { state with
                    Order = None
                }
                , Cmd.none

            | ResetOrderScenario ->
                match state.Order with
                | Some ord ->
                    OrderLoader.create state.SelectedComponent state.SelectedItem ord
                    |> resetOrderScenario
                | None -> ()

                { state with
                    Order = None
                }
                , Cmd.none

            | ChangeComponent cmp ->
                match cmp with
                | None -> state, Cmd.none
                | Some _ ->
                    { state with
                        SelectedComponent = cmp
                        SelectedItem =
                            if state.SelectedComponent = cmp then state.SelectedItem
                            else None
                    }, Cmd.none

            | ChangeComponentOrderableQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.map (fun cmp ->
                                            match state.SelectedComponent with
                                            | Some c when cmp.Name = c ->
                                                { cmp with
                                                    OrderableQuantity =
                                                        cmp.OrderableQuantity |> setOvar s
                                                }
                                            | _ -> cmp
                                        )
                                }
                        }
                        |> UpdateOrderScenario

                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeComponentDoseQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.map (fun cmp ->
                                            match state.SelectedComponent with
                                            | Some c when cmp.Name = c ->
                                                { cmp with
                                                    Dose =
                                                        { cmp.Dose with
                                                            Quantity =
                                                                cmp.Dose.Quantity
                                                                |> setOvar s
                                                        }
                                                }
                                            | _ -> cmp
                                        )
                                }
                        }
                        |> UpdateOrderScenario

                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeItem itm ->
                match itm with
                | None -> state, Cmd.none
                | Some _ -> { state with SelectedItem = itm }, Cmd.none

            | ChangeFrequency s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Schedule =
                                { ord.Schedule with
                                    Frequency =
                                        ord.Schedule.Frequency
                                        |> setOvar s
                                }
                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeTime s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Schedule =
                                { ord.Schedule with
                                    Time =
                                        ord.Schedule.Time
                                        |> setOvar s
                                }
                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceDoseQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    Dose =
                                                                        { itm.Dose with
                                                                            Quantity =
                                                                                itm.Dose.Quantity
                                                                                |> setOvar s
                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }
                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceDoseQuantityAdjust s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    Dose =
                                                                        { itm.Dose with
                                                                            QuantityAdjust =
                                                                                itm.Dose.QuantityAdjust
                                                                                |> setOvar s
                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }
                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstancePerTime s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    Dose =
                                                                        { itm.Dose with
                                                                            PerTime =
                                                                                itm.Dose.PerTime
                                                                                |> setOvar s
                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstancePerTimeAdjust s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    Dose =
                                                                        { itm.Dose with
                                                                            PerTimeAdjust =
                                                                                itm.Dose.PerTimeAdjust
                                                                                |> setOvar s
                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceRate s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    Dose =
                                                                        { itm.Dose with
                                                                            Rate =
                                                                                itm.Dose.Rate
                                                                                |> setOvar s
                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceRateAdjust s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    Dose =
                                                                        { itm.Dose with
                                                                            RateAdjust =
                                                                                itm.Dose.RateAdjust
                                                                                |> setOvar s
                                                                        }
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceComponentConcentration (cname, iname, s) ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 && cmp.Name <> cname then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    ComponentConcentration =
                                                                        itm.ComponentConcentration
                                                                        |> setOvar s
                                                                }
                                                            | _ -> 
                                                                if itm.Name <> iname then itm
                                                                else
                                                                    { itm with
                                                                        ComponentConcentration =
                                                                            itm.ComponentConcentration
                                                                            |> setOvar s
                                                                    }

                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceOrderableConcentration s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    OrderableConcentration =
                                                                        itm.OrderableConcentration
                                                                        |> setOvar s
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeSubstanceOrderableQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Components =
                                        ord.Orderable.Components
                                        |> Array.mapi (fun i cmp ->
                                            if i > 0 then cmp
                                            else
                                                { cmp with
                                                    Items =
                                                        cmp.Items
                                                        |> Array.map (fun itm ->
                                                            match state.SelectedItem with
                                                            | Some subst when subst = itm.Name ->
                                                                { itm with
                                                                    OrderableQuantity =
                                                                        itm.OrderableQuantity
                                                                        |> setOvar s
                                                                }
                                                            | _ -> itm
                                                        )
                                                }
                                        )
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None }, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableDoseQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Dose =
                                        { ord.Orderable.Dose with
                                            Quantity =
                                                ord.Orderable.Dose.Quantity
                                                |> setOvar s
                                        }
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableDoseRate s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    Dose =
                                        { ord.Orderable.Dose with
                                            Rate =
                                                ord.Orderable.Dose.Rate
                                                |> setOvar s
                                        }
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            | ChangeOrderableQuantity s ->
                match state.Order with
                | Some ord ->
                    let msg =
                        { ord with
                            Orderable =
                                { ord.Orderable with
                                    OrderableQuantity =
                                        ord.Orderable.OrderableQuantity
                                        |> setOvar s
                                }

                        }
                        |> UpdateOrderScenario
                    { state with Order = None}, Cmd.ofMsg msg
                | _ -> state, Cmd.none

            // == Frequency ==
            | SetMinFrequencyProperty -> handleNav navigate.setFreqMin
            | DecreaseFrequencyProperty -> handleNav navigate.setFreqDec
            | SetMedianFrequencyProperty -> handleNav navigate.setFreqMed
            | IncreaseFrequencyProperty -> handleNav navigate.setFreqInc
            | SetMaxFrequencyProperty -> handleNav navigate.setFreqMax
            // == Rate ==
            | SetMinDoseRateProperty -> handleNav navigate.setRateMin
            | DecreaseDoseRateProperty _ -> handleNav navigate.setRateDec
            | SetMedianDoseRateProperty -> handleNav navigate.setRateMed
            | IncreaseDoseRateProperty _ -> handleNav navigate.setRateInc
            | SetMaxDoseRateProperty -> handleNav navigate.setRateMax
            // == DoseQty ==
            | SetMinDoseQuantityProperty -> handleNav navigate.setDoseQtyMin
            | DecreaseDoseQuantityProperty _ -> handleNav navigate.setDoseQtyDec
            | SetMedianDoseQuantityProperty -> handleNav navigate.setDoseQtyMed
            | IncreaseDoseQuantityProperty _ -> handleNav navigate.setDoseQtyInc
            | SetMaxDoseQuantityProperty -> handleNav navigate.setDoseQtyMax
            // == ComponentQty ==
            | SetMinComponentQuantityProperty -> handleNav navigate.setComponentQtyMin
            | DecreaseComponentQuantityProperty _ -> handleNav navigate.setComponentQtyDec
            | SetMedianComponentQuantityProperty -> handleNav navigate.setComponentQtyMed
            | IncreaseComponentQuantityProperty _ -> handleNav navigate.setComponentQtyInc
            | SetMaxComponentQuantityProperty -> handleNav navigate.setComponentQtyMax


        let showOrderName (ord : Order option) =
            match ord with
            | Some ord ->
                let form =
                    ord.Orderable.Components
                    |> Array.tryHead
                    |> Option.map _.Form
                    |> Option.defaultValue ""
                $"{ord.Orderable.Name} {form}"
            | None -> "order is loading ..."


    open Elmish


    [<JSX.Component>]
    let View (props:
        {|
            orderContext: Deferred<OrderContext>
            updateOrderScenario: OrderContext -> unit
            navigateOrderScenario : {|
                // Frequency
                setMinFrequency : OrderContext -> unit
                decrFrequency : OrderContext -> unit
                setMedianFrequency: OrderContext -> unit
                incrFrequency : OrderContext -> unit
                setMaxFrequency: OrderContext -> unit
                // Rate
                setMinRate : OrderContext -> unit
                decrRate : OrderContext -> unit
                setMedianRate: OrderContext -> unit
                incrRate : OrderContext -> unit
                setMaxRate: OrderContext -> unit
                // Dose Quantity
                setMinDoseQty : OrderContext -> unit
                decrDoseQty : OrderContext * int -> unit
                setMedianDoseQty: OrderContext  -> unit
                incrDoseQty : OrderContext * int  -> unit
                setMaxDoseQty: OrderContext -> unit
                // Component Quantity
                setMinComponentQty : OrderContext * string -> unit
                decrComponentQty : OrderContext * string * int -> unit
                setMedianComponentQty: OrderContext * string -> unit
                incrComponentQty : OrderContext * string * int  -> unit
                setMaxComponentQty: OrderContext * string -> unit
            |}
            refreshOrderScenario : OrderContext -> unit
            closeOrder : unit -> unit
            localizationTerms : Deferred<string [] []>
        |}) =
        let context = React.useContext Global.context
        let lang = context.Localization

        let getTerm defVal term =
            props.localizationTerms
            |> Deferred.map (fun terms ->
                Localization.getTerm terms lang term
                |> Option.defaultValue defVal
            )
            |> Deferred.defaultValue defVal

        let useAdjust =
            match props.orderContext with
            | Resolved pr ->
                pr.Scenarios
                |> Array.tryExactlyOne
                |> Option.map (fun sc -> sc.UseAdjust)
                |> Option.defaultValue false
            | _ -> false

        let updateOrderScenario (ol : OrderLoader) =
            match props.orderContext with
            | Resolved ctx ->
                { ctx with
                    Scenarios =
                        ctx.Scenarios
                        |> Array.map (fun sc ->
                            if sc.Order.Id <> ol.Order.Id then sc
                            else
                                {
                                    sc with
                                        Component = ol.Component
                                        Item = ol.Item
                                        Order = ol.Order
                                }
                        )
                }
                |> props.updateOrderScenario
            | _ -> ()

        let resetOrderScenario (ol : OrderLoader) =
            match props.orderContext with
            | Resolved ctx ->
                { ctx with
                    Scenarios =
                        ctx.Scenarios
                        |> Array.map (fun sc ->
                            if sc.Order.Id <> ol.Order.Id then sc
                            else
                                {
                                    sc with
                                        Component = ol.Component
                                        Item = ol.Item
                                        Order = ol.Order
                                }
                        )
                }
                |> props.refreshOrderScenario
            | _ -> ()

        let navigate = 
            let create nav =
                fun (ol : OrderLoader) ->
                    match props.orderContext with
                    | Resolved ctx ->
                        { ctx with
                            Scenarios =
                                ctx.Scenarios
                                |> Array.map (fun sc ->
                                    if sc.Order.Id <> ol.Order.Id then sc
                                    else
                                        {
                                            sc with
                                                Component = ol.Component
                                                Item = ol.Item
                                                Order = ol.Order
                                        }
                                )
                        }
                        |> nav
                    | _ -> ()

            let createWithCmp nav =
                fun (ol : OrderLoader) ->
                    match props.orderContext with
                    | Resolved ctx ->
                        match ol.Component with
                        | None -> ()
                        | Some cmp ->
                            let ctx =
                                { ctx with
                                    Scenarios =
                                        ctx.Scenarios
                                        |> Array.map (fun sc ->
                                            if sc.Order.Id <> ol.Order.Id then sc
                                            else
                                                {
                                                    sc with
                                                        Component = ol.Component
                                                        Item = ol.Item
                                                        Order = ol.Order
                                                }
                                        )
                                }
                            nav (ctx, cmp)
                    | _ -> ()

            let createWithN nav =
                fun (ol : OrderLoader) ->
                    match props.orderContext with
                    | Resolved ctx ->
                        match ol.Component with
                        | None -> ()
                        | Some cmp ->
                            let ctx =
                                { ctx with
                                    Scenarios =
                                        ctx.Scenarios
                                        |> Array.map (fun sc ->
                                            if sc.Order.Id <> ol.Order.Id then sc
                                            else
                                                {
                                                    sc with
                                                        Component = ol.Component
                                                        Item = ol.Item
                                                        Order = ol.Order
                                                }
                                        )
                                }
                            nav (ctx, 1)
                    | _ -> ()

            let createWithCmpN nav =
                fun (ol : OrderLoader) ->
                    match props.orderContext with
                    | Resolved ctx ->
                        match ol.Component with
                        | None -> ()
                        | Some cmp ->
                            let ctx =
                                { ctx with
                                    Scenarios =
                                        ctx.Scenarios
                                        |> Array.map (fun sc ->
                                            if sc.Order.Id <> ol.Order.Id then sc
                                            else
                                                {
                                                    sc with
                                                        Component = ol.Component
                                                        Item = ol.Item
                                                        Order = ol.Order
                                                }
                                        )
                                }
                            nav (ctx, cmp, 1)
                    | _ -> ()

            {|
                // Frequency
                setFreqMin = create props.navigateOrderScenario.setMinFrequency
                setFreqDec = create props.navigateOrderScenario.decrFrequency
                setFreqMed = create props.navigateOrderScenario.setMedianFrequency
                setFreqInc = create props.navigateOrderScenario.incrFrequency
                setFreqMax = create props.navigateOrderScenario.setMaxFrequency
                // Dose Rate
                setRateMin = create props.navigateOrderScenario.setMinRate
                setRateDec = create props.navigateOrderScenario.decrRate
                setRateMed = create props.navigateOrderScenario.setMedianRate
                setRateInc = create props.navigateOrderScenario.incrRate
                setRateMax = create props.navigateOrderScenario.setMaxRate
                // Dose Quantity
                setDoseQtyMin = create props.navigateOrderScenario.setMinDoseQty
                setDoseQtyDec = createWithN props.navigateOrderScenario.decrDoseQty
                setDoseQtyMed = create props.navigateOrderScenario.setMedianDoseQty
                setDoseQtyInc = createWithN props.navigateOrderScenario.incrDoseQty
                setDoseQtyMax = create props.navigateOrderScenario.setMaxDoseQty
                // Component Quantity
                setComponentQtyMin = createWithCmp props.navigateOrderScenario.setMinComponentQty
                setComponentQtyDec = createWithCmpN props.navigateOrderScenario.decrComponentQty
                setComponentQtyMed = createWithCmp props.navigateOrderScenario.setMedianComponentQty
                setComponentQtyInc = createWithCmpN props.navigateOrderScenario.incrComponentQty
                setComponentQtyMax = createWithCmp props.navigateOrderScenario.setMaxComponentQty
            |}

        let state, dispatch =
            React.useElmish (
                init props.orderContext,
                update updateOrderScenario resetOrderScenario navigate,
                [| box props.orderContext |]
            )

        let itms =
            match state.Order with
            | Some ord ->
                ord.Orderable.Components
                // only use the main component for dosing
                |> Array.tryFind(fun cmp ->
                    state.SelectedComponent.IsNone ||
                    state.SelectedComponent.Value = cmp.Name
                )
                |> Option.map (fun cmp ->
                    // filter out additional items, they are not used for dosing
                    cmp.Items
                    |> Array.filter (_.IsAdditional >> not)
                )
                |> Option.defaultValue [||]
            | _ -> [||]

        let substIndx =
            itms
            |> Array.tryFindIndex (fun i ->
                state.SelectedItem
                |> Option.map ((=) i.Name)
                |> Option.defaultValue false
            )
            |> function
            | None -> Some 0
            | Some i -> Some i

        let select isLoading lbl selected updateSelected navigate hasClear xs =
            if xs |> Array.isEmpty && navigate |> Option.isNone then
                JSX.jsx $"<></>"
            else
                Components.SimpleSelect.View({|
                    updateSelected = updateSelected
                    label = lbl
                    selected =
                        if xs |> Array.length = 1 then xs[0] |> fst |> Some
                        else selected
                    values = xs
                    isLoading = isLoading
                    hasClear = hasClear
                    navigate = navigate
                |})

        let progress =
            match props.orderContext with
            | Resolved _ -> JSX.jsx $"<></>"
            | _ ->
                JSX.jsx
                    $"""
                import CircularProgress from '@mui/material/CircularProgress';

                <Box sx={ {| mt = 5; display = "flex"; p = 20 |} }>
                    <CircularProgress />
                </Box>
                """

        let fixPrecision = Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision


        let onClickOk =
            fun () -> props.closeOrder ()


        let onClickReset =
            fun () ->
                ResetOrderScenario |> dispatch

        let headerSx = {| backgroundColor = Mui.Colors.Blue.``50`` |}

        let content =
            JSX.jsx
                $"""
            import CardHeader from '@mui/material/CardHeader';
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';
            import Paper from '@mui/material/Paper';

            <div>

            <CardHeader
                sx = {headerSx}
                title={state.Order |> showOrderName}
                titleTypographyProps={ {| variant = "h6" |} }
            ></CardHeader>
            <CardContent>
                <Stack direction={"column"} spacing={3} >
                    {
                        // component name
                        match state.Order with
                        | Some ord ->
                            if ord.Orderable.Components |> Array.length <= 1 then JSX.jsx $"<></>"
                            else
                                ord.Orderable.Components
                                |> Array.map _.Name
                                |> Array.map (fun s -> s, s)
                                |> select false "Componenten" state.SelectedComponent (ChangeComponent >> dispatch) None false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                    {
                        // substance name
                        match state.Order with
                        | Some ord ->
                            if ord.Orderable.Components |> Array.isEmpty ||
                               itms |> Array.length <= 1 then JSX.jsx $"<></>"
                            else
                                itms
                                |> Array.map _.Name
                                |> Array.map (fun s -> s, s)
                                |> select false "Stoffen" state.SelectedItem (ChangeItem >> dispatch) None false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                    {
                        // frequency
                        match state.Order with
                        | Some ord ->
                            let xs =
                                ord.Schedule.Frequency.Variable.Vals
                                |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                                |> Option.defaultValue [||]

                            let navigate =
                                if xs |> Array.length <> 1 then None
                                else
                                    {|
                                        first = fun () -> SetMinFrequencyProperty |> dispatch
                                        decrease = fun () -> DecreaseFrequencyProperty |> dispatch
                                        median = fun () -> SetMedianFrequencyProperty |> dispatch
                                        increase = fun () -> IncreaseFrequencyProperty |> dispatch
                                        last = fun () -> SetMaxFrequencyProperty |> dispatch
                                    |}
                                    |> Some
                            select false (Terms.``Order Frequency`` |> getTerm "Frequentie") None (ChangeFrequency >> dispatch) navigate true xs
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                    {
                        // substance dose quantity
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous |> not &&
                                                itms |> Array.length > 0 ->
                            let label, vals =
                                itms[i].Dose.Quantity.Variable.Vals
                                |> Option.map (fun v ->
                                    (Terms.``Order Dose``
                                    |> getTerm "Keer Dosis"
                                    |> fun s -> $"{s} ({v.Unit})"),
                                    v.Value
                                    |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                                    |> Array.distinctBy snd
                                )
                                |> Option.defaultValue ("", [||])

                            vals
                            |> select false label None (ChangeSubstanceDoseQuantity >> dispatch) None false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                    {
                        // substance dose quantity adjust
                        match substIndx, state.Order with
                        | Some i, Some ord when (ord.Schedule.IsOnce || ord.Schedule.IsOnceTimed) &&
                                                itms |> Array.length > 0 && useAdjust ->
                            let label, vals =
                                itms[i].Dose.QuantityAdjust.Variable.Vals
                                |> Option.map (fun v ->
                                    (Terms.``Order Adjusted dose``
                                    |> getTerm "Keer Dosis"
                                    |> fun s -> $"{s} ({v.Unit})"),
                                    v.Value
                                    |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                                    |> Array.distinctBy snd
                                )
                                |> Option.defaultValue ("", [||])

                            vals
                            |> select false label None (ChangeSubstanceDoseQuantityAdjust >> dispatch) None false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                    {
                        // substance dose per time
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous |> not &&
                                                itms |> Array.length > 0 ->
                            let dispatch =
                                if useAdjust then ChangeSubstancePerTimeAdjust >> dispatch
                                else ChangeSubstancePerTime >> dispatch
                            let label, vals =
                                if useAdjust then
                                    itms[i].Dose.PerTimeAdjust.Variable.Vals
                                else
                                    itms[i].Dose.PerTime.Variable.Vals
                                |> Option.map (fun v ->
                                    (Terms.``Order Adjusted dose``
                                    |> getTerm "Dosering"
                                    |> fun s -> $"{s} ({v.Unit})"),
                                    v.Value
                                    |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                                    |> Array.distinctBy snd
                                )
                                |> Option.defaultValue ("", [||])

                            vals
                            |> select false label None dispatch None false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                    {
                        // substance dose rate
                        let navigate = None

                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous &&
                                                itms |> Array.length > 0 ->
                            let dispatch = if useAdjust then ChangeSubstanceRateAdjust >> dispatch else ChangeSubstanceRate >> dispatch

                            if useAdjust then
                                itms[i].Dose.RateAdjust.Variable.Vals
                            else
                                itms[i].Dose.Rate.Variable.Vals
                            |> Option.map (fun v ->
                                v.Value
                                |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                                |> Array.distinctBy snd
                            )
                            |> Option.defaultValue [||]
                            |> select false (Terms.``Order Adjusted dose`` |> getTerm "Dosering") None dispatch navigate true
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                    {
                        // component orderable quantity
                        match state.Order with
                        | Some ord when ord.Orderable.Components |> Array.length > 1 ->
                            let cmp =
                                ord.Orderable.Components
                                |> Array.tryFind (fun c -> state.SelectedComponent.IsNone || c.Name = state.SelectedComponent.Value)

                            let vals =
                                cmp
                                |> Option.bind _.OrderableQuantity.Variable.Vals
                                |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d} {v.Unit}"))
                                |> Option.defaultValue [||]

                            let navigate =
                                let c = vals |> Array.length

                                let show =
                                    match cmp with
                                    | None -> false
                                    | Some cmp ->
                                        cmp.OrderableQuantity.Constraints.Min.IsSome &&
                                        cmp.OrderableQuantity.Constraints.Incr.IsSome && 
                                        cmp.OrderableQuantity.Constraints.Max.IsSome ||
                                        c = 1 || c > 10
                                        
                                if not show then None
                                else
                                    {|
                                        first = fun () -> SetMinComponentQuantityProperty |> dispatch
                                        decrease = fun () -> 1 |> DecreaseComponentQuantityProperty |> dispatch
                                        median = fun () -> SetMedianComponentQuantityProperty |> dispatch
                                        increase = fun () -> 1 |> IncreaseComponentQuantityProperty |> dispatch
                                        last = fun () -> SetMaxComponentQuantityProperty |> dispatch
                                    |}
                                    |> Some

                            vals
                            |> select false "Bereiding Hoeveelheid" None (ChangeComponentOrderableQuantity >> dispatch) navigate false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                    {
                        // component dose quantity
                        match state.Order with
                        | Some ord when ord.Schedule.IsContinuous |> not &&
                                        ord.Orderable.Components |> Array.length > 1 &&
                                        itms |> Array.isEmpty ->
                            ord.Orderable.Components
                            |> Array.tryFind (fun c -> state.SelectedComponent.IsNone || c.Name = state.SelectedComponent.Value)
                            |> Option.bind _.Dose.Quantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false "Keer Dosering" None (ChangeComponentDoseQuantity >> dispatch) None false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }

                    {
                        // orderable dose quantity
                        match state.Order with
                        | Some ord when ord.Schedule.IsContinuous |> not ->
                            let navigate =
                                {|
                                    first = fun () -> SetMinDoseQuantityProperty |> dispatch
                                    decrease = fun () -> 1 |> DecreaseDoseQuantityProperty |> dispatch
                                    median = fun () -> SetMedianDoseQuantityProperty |> dispatch
                                    increase = fun () -> 1 |> IncreaseDoseQuantityProperty |> dispatch
                                    last = fun () -> SetMaxDoseQuantityProperty |> dispatch
                                |}
                                |> Some

                            ord.Orderable.Dose.Quantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false "Toedien Hoeveelheid" None (ChangeOrderableDoseQuantity >> dispatch) navigate false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                    {
                        // substance component concentration
                        match substIndx, state.Order with
                        | Some i, Some ord ->
                            match itms |> Array.tryItem i with
                            | Some itm ->
                                let cname, iname = 
                                    ord.Orderable.Components |> Array.tryHead |> Option.map _.Name |> Option.defaultValue ""
                                    , itm.Name

                                let change = fun s -> (cname, iname, s) |> ChangeSubstanceComponentConcentration

                                itm.ComponentConcentration.Variable.Vals
                                |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}"))
                                |> Option.defaultValue [||]
                                |> fun xs -> if xs |> Array.length <= 1 then [||] else xs
                                |> select false "Product Sterkte" None (change >> dispatch) None true
                            | None ->
                                match 
                                    ord.Orderable.Components
                                    |> Array.tryFind (fun c -> state.SelectedComponent.IsNone || c.Name = state.SelectedComponent.Value) with
                                | Some cmp ->
                                    match cmp.Items |> Array.tryFind (fun i -> i.Name = cmp.Name) with
                                    | Some itm -> 
                                        let change = fun s -> (cmp.Name, itm.Name, s) |> ChangeSubstanceComponentConcentration

                                        itm.ComponentConcentration.Variable.Vals
                                        |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d} {v.Unit}"))
                                        |> Option.defaultValue [||]
                                        |> select false "Product Sterkte" None (change >> dispatch) None true
                                    | None -> 
                                        [||]
                                        |> select true "" None ignore None false
                                | None -> 
                                    [||]
                                    |> select true "" None ignore None false

                        | _ ->
                            [||]
                            |> select true "" None ignore None false

                    }
                    {
                        // substance orderable concentration
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous |> not &&
                                                itms |> Array.length > 0 &&
                                                ord.Orderable.Components |> Array.length > 1 ->
                            itms[i].OrderableConcentration.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false $"{itms[i].Name |> String.capitalize} Concentratie" None (ChangeSubstanceOrderableConcentration >> dispatch) None false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                    {
                        // substance orderable quantity
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous &&
                                                itms |> Array.length > 0 &&
                                                ord.Orderable.Components |> Array.length > 1 ->
                            itms[i].OrderableQuantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false $"{itms[i].Name |> String.capitalize} Hoeveelheid" None (ChangeSubstanceOrderableQuantity >> dispatch) None false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                    {
                        // orderable quantity
                        match state.Order with
                        | Some ord ->
                            ord.Orderable.OrderableQuantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false "Totale Hoeveelheid" None (ChangeOrderableQuantity >> dispatch) None false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }

                    {
                        // orderable dose rate
                        let navigate =
                            {|
                                first = fun () -> SetMinDoseRateProperty |> dispatch
                                decrease = fun () -> 1 |> DecreaseDoseRateProperty |> dispatch
                                median = fun () -> SetMedianDoseRateProperty |> dispatch
                                increase = fun () -> 1 |> IncreaseDoseRateProperty |> dispatch
                                last = fun () -> SetMaxDoseRateProperty |> dispatch
                            |}
                            |> Some

                        match state.Order with
                        | Some ord when ord.Schedule.IsContinuous ||
                                        ord.Schedule.IsTimed ||
                                        ord.Schedule.IsOnceTimed ->
                            ord.Orderable.Dose.Rate.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false (Terms.``Order Drip rate`` |> getTerm "Pompsnelheid") None (ChangeOrderableDoseRate >> dispatch) navigate false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                    {
                        // administration time
                        match state.Order with
                        | Some ord ->
                            ord.Schedule.Time.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 2} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> Array.distinctBy snd
                            |> select false (Terms.``Order Administration time`` |> getTerm "Inloop tijd") None (ChangeTime >> dispatch) None false
                        | _ ->
                            [||]
                            |> select true "" None ignore None false
                    }
                </Stack>
                {progress}
            </CardContent>
            <CardActions >
                    <Button onClick={onClickOk}>
                        {Terms.``Ok `` |> getTerm "Ok"}
                    </Button>
                    <Button onClick={onClickReset} startIcon={Mui.Icons.RefreshIcon}>
                        Reset
                    </Button>
            </CardActions>
            </div>
            """

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Card from '@mui/material/Card';
        import CardActions from '@mui/material/CardActions';
        import CardContent from '@mui/material/CardContent';
        import Button from '@mui/material/Button';
        import Typography from '@mui/material/Typography';

        <Card variant="outlined" raised={true}>
                {content}
        </Card>
        """


