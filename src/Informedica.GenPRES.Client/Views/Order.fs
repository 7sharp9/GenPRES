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
                    setRateDec : int -> OrderLoader -> unit
                    setRateMed : OrderLoader -> unit
                    setRateInc : int -> OrderLoader -> unit
                    setRateMax : OrderLoader -> unit

                    setDoseQtyMin : OrderLoader -> unit
                    setDoseQtyDec : int -> OrderLoader -> unit
                    setDoseQtyMed : OrderLoader -> unit
                    setDoseQtyInc : int -> OrderLoader -> unit
                    setDoseQtyMax : OrderLoader -> unit

                    setComponentQtyMin : OrderLoader -> unit
                    setComponentQtyDec : int -> OrderLoader -> unit
                    setComponentQtyMed : OrderLoader -> unit
                    setComponentQtyInc : int -> OrderLoader  -> unit
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
            | DecreaseDoseRateProperty n -> handleNav (navigate.setRateDec n)
            | SetMedianDoseRateProperty -> handleNav navigate.setRateMed
            | IncreaseDoseRateProperty n -> handleNav (navigate.setRateInc n)
            | SetMaxDoseRateProperty -> handleNav navigate.setRateMax
            // == DoseQty ==
            | SetMinDoseQuantityProperty -> handleNav navigate.setDoseQtyMin
            | DecreaseDoseQuantityProperty n -> handleNav (navigate.setDoseQtyDec n)
            | SetMedianDoseQuantityProperty -> handleNav navigate.setDoseQtyMed
            | IncreaseDoseQuantityProperty n -> handleNav (navigate.setDoseQtyInc n)
            | SetMaxDoseQuantityProperty -> handleNav navigate.setDoseQtyMax
            // == ComponentQty ==
            | SetMinComponentQuantityProperty -> handleNav navigate.setComponentQtyMin
            | DecreaseComponentQuantityProperty n -> handleNav (navigate.setComponentQtyDec n)
            | SetMedianComponentQuantityProperty -> handleNav navigate.setComponentQtyMed
            | IncreaseComponentQuantityProperty n -> handleNav (navigate.setComponentQtyInc n)
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
                decrRate : OrderContext * int -> unit
                setMedianRate: OrderContext -> unit
                incrRate : OrderContext * int -> unit
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
                fun n (ol : OrderLoader) ->
                    match props.orderContext with
                    | Resolved ctx ->
                        match ol.Component with
                        | None -> ()
                        | Some _ ->
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
                            nav (ctx, n)
                    | _ -> ()

            let createWithCmpN nav =
                fun n (ol : OrderLoader) ->
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
                            nav (ctx, cmp, n)
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
                setRateDec = createWithN props.navigateOrderScenario.decrRate
                setRateMed = create props.navigateOrderScenario.setMedianRate
                setRateInc = createWithN props.navigateOrderScenario.incrRate
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

        let showDosingDivider =
            match state.Order with
            | None -> false
            | Some ord ->
                let hasSubstIndx = substIndx.IsSome && itms |> Array.length > 0

                // substance dose quantity: not continuous, substIndx, itms > 0, has vals
                let hasSubstDoseQty =
                    hasSubstIndx
                    && ord.Schedule.IsContinuous |> not
                    && substIndx
                       |> Option.bind (fun i -> itms |> Array.tryItem i)
                       |> Option.bind (fun itm ->
                           itm.Dose.Quantity.Variable.Vals
                           |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       )
                       |> Option.defaultValue false

                // substance dose quantity adjust: once/onceTimed, useAdjust, substIndx, itms > 0, has vals
                let hasSubstDoseQtyAdj =
                    hasSubstIndx
                    && useAdjust
                    && (ord.Schedule.IsOnce || ord.Schedule.IsOnceTimed)
                    && substIndx
                       |> Option.bind (fun i -> itms |> Array.tryItem i)
                       |> Option.bind (fun itm ->
                           itm.Dose.QuantityAdjust.Variable.Vals
                           |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       )
                       |> Option.defaultValue false

                // substance dose per time: not continuous, substIndx, itms > 0, has vals
                let hasSubstPerTime =
                    hasSubstIndx
                    && ord.Schedule.IsContinuous |> not
                    && substIndx
                       |> Option.bind (fun i -> itms |> Array.tryItem i)
                       |> Option.bind (fun itm ->
                           let vals =
                               if useAdjust then itm.Dose.PerTimeAdjust.Variable.Vals
                               else itm.Dose.PerTime.Variable.Vals
                           vals |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       )
                       |> Option.defaultValue false

                // substance dose rate: continuous, substIndx, itms > 0, has vals
                let hasSubstRate =
                    hasSubstIndx
                    && ord.Schedule.IsContinuous
                    && substIndx
                       |> Option.bind (fun i -> itms |> Array.tryItem i)
                       |> Option.bind (fun itm ->
                           let vals =
                               if useAdjust then itm.Dose.RateAdjust.Variable.Vals
                               else itm.Dose.Rate.Variable.Vals
                           vals |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       )
                       |> Option.defaultValue false

                hasSubstDoseQty || hasSubstDoseQtyAdj || hasSubstPerTime || hasSubstRate

        let showPrepDivider =
            match state.Order with
            | None -> false
            | Some ord ->
                let multiComponent = ord.Orderable.Components |> Array.length > 1

                // component orderable quantity: requires components > 1
                let hasCompOrdQty =
                    multiComponent
                    && ord.Orderable.Components
                       |> Array.tryFind (fun c ->
                           state.SelectedComponent.IsNone || c.Name = state.SelectedComponent.Value
                       )
                       |> Option.bind _.OrderableQuantity.Variable.Vals
                       |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       |> Option.defaultValue false

                // substance component concentration: requires substIndx and vals > 1
                let hasSubstCompConc =
                    substIndx
                    |> Option.bind (fun i -> itms |> Array.tryItem i)
                    |> Option.bind (fun itm ->
                        itm.ComponentConcentration.Variable.Vals
                        |> Option.map (fun v -> v.Value |> Array.length > 1)
                    )
                    |> Option.defaultValue false

                // substance orderable quantity: requires substIndx, itms > 0, components > 1, continuous
                let hasSubstOrbQty =
                    multiComponent
                    && ord.Schedule.IsContinuous
                    && substIndx
                       |> Option.bind (fun i -> itms |> Array.tryItem i)
                       |> Option.bind (fun itm ->
                           itm.OrderableQuantity.Variable.Vals
                           |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       )
                       |> Option.defaultValue false

                // substance orderable concentration: requires substIndx, itms > 0, components > 1, not continuous
                let hasSubstOrbConc =
                    multiComponent
                    && ord.Schedule.IsContinuous |> not
                    && substIndx
                       |> Option.bind (fun i -> itms |> Array.tryItem i)
                       |> Option.bind (fun itm ->
                           itm.OrderableConcentration.Variable.Vals
                           |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       )
                       |> Option.defaultValue false

                // orderable quantity: requires components > 1
                let hasOrbQty =
                    multiComponent
                    && ord.Orderable.OrderableQuantity.Variable.Vals
                       |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       |> Option.defaultValue false

                hasCompOrdQty || hasSubstCompConc || hasSubstOrbConc || hasSubstOrbQty || hasOrbQty

        let showAdminDivider =
            match state.Order with
            | None -> false
            | Some ord ->
                // frequency: shown when has vals or single val (nav)
                let hasFrequency =
                    ord.Schedule.Frequency.Variable.Vals
                    |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                    |> Option.defaultValue false
                // orderable dose quantity: shown when not continuous and has vals
                let hasDoseQty =
                    ord.Schedule.IsContinuous |> not
                    && ord.Orderable.Dose.Quantity.Variable.Vals
                       |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                       |> Option.defaultValue false
                // orderable dose rate: shown when continuous/timed/onceTimed
                // (navigate is always Some, so select renders even with empty vals)
                let hasDoseRate =
                    ord.Schedule.IsContinuous
                    || ord.Schedule.IsTimed
                    || ord.Schedule.IsOnceTimed
                // administration time: shown when has vals
                let hasTime =
                    ord.Schedule.Time.Variable.Vals
                    |> Option.map (fun v -> v.Value |> Array.isEmpty |> not)
                    |> Option.defaultValue false

                hasFrequency || hasDoseQty || hasDoseRate || hasTime

        let getWarning warning =
            match warning with
            | IsNormal -> None
            | IsCaution -> Some Mui.Colors.Blue.``600``
            | IsWarning -> Some  Mui.Colors.Orange.``700``
            | IsAlert -> Some Mui.Colors.Red.``700``

        let select 
            isLoading
            lbl 
            selected 
            updateSelected 
            navigate 
            hasClear 
            warning 
            xs =

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
                    warning = warning
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

        let preparationDivider =
            if showPrepDivider then
                JSX.jsx
                    $"""<Divider><Typography variant="caption">bereiding</Typography></Divider>"""
            else JSX.jsx $"<></>"

        let dosingDivider =
            if showDosingDivider then
                JSX.jsx
                    $"""<Divider><Typography variant="caption">dosering</Typography></Divider>"""
            else JSX.jsx $"<></>"

        let administrationDivider =
            if showAdminDivider then
                JSX.jsx
                    $"""<Divider><Typography variant="caption">toediening</Typography></Divider>"""
            else JSX.jsx $"<></>"

        let content =
            let createNav navigable solved 
                setMin
                decr
                setMed
                incr
                setMax =
                {|
                    first = 
                        if navigable then (fun () -> setMin |> dispatch) |> Some
                        elif solved then (fun () -> 2 |> decr |> dispatch) |> Some
                        else None
                    decrease =
                        if solved then (fun () -> 1 |> decr |> dispatch) |> Some
                        else None
                    median = 
                        if navigable then (fun () -> setMed |> dispatch) |> Some
                        else None
                    increase = 
                        if solved then (fun () -> 1 |> incr |> dispatch) |> Some
                        else None
                    last = 
                        if navigable then (fun () -> setMax |> dispatch) |> Some
                        elif solved then (fun () -> 2 |> incr |> dispatch) |> Some
                        else None
                |}
                |> Some

            JSX.jsx
                $"""
            import CardHeader from '@mui/material/CardHeader';
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';
            import Paper from '@mui/material/Paper';
            import Divider from '@mui/material/Divider';
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
                                |> select false "componenten" state.SelectedComponent (ChangeComponent >> dispatch) None false None
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
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
                                |> select false "stoffen" state.SelectedItem (ChangeItem >> dispatch) None false None
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
                    }
                    {dosingDivider}
                    {
                        // substance dose quantity
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous |> not &&
                                                itms |> Array.length > 0 ->
                            let label, vals =
                                itms[i].Dose.Quantity.Variable.Vals
                                |> Option.map (fun v ->
                                    (Terms.``Order Dose``
                                    |> getTerm "keer dosis"
                                    |> fun s -> $"{s} ({v.Unit})"),
                                    v.Value
                                    |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                                    |> Array.distinctBy snd
                                )
                                |> Option.defaultValue ("", [||])

                            let warning = itms[i].Dose.Quantity.Level |> getWarning

                            vals
                            |> select false label None (ChangeSubstanceDoseQuantity >> dispatch) None false warning
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
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
                                    |> getTerm "keer dosis"
                                    |> fun s -> $"{s} ({v.Unit})"),
                                    v.Value
                                    |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                                    |> Array.distinctBy snd
                                )
                                |> Option.defaultValue ("", [||])

                            let warning = itms[i].Dose.QuantityAdjust.Level |> getWarning

                            vals
                            |> select false label None (ChangeSubstanceDoseQuantityAdjust >> dispatch) None true warning
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
                    }
                    {
                        // substance dose per time / dose per time adjust
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
                                    |> getTerm "dosering"
                                    |> fun s -> $"{s} ({v.Unit})"),
                                    v.Value
                                    |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}")
                                    |> Array.distinctBy snd
                                )
                                |> Option.defaultValue ("", [||])

                            let warning = 
                                if useAdjust then
                                    itms[i].Dose.PerTimeAdjust.Level |> getWarning
                                else
                                    itms[i].Dose.PerTime.Level |> getWarning

                            vals
                            |> select false label None dispatch None true warning
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
                    }
                    {
                        // substance dose rate / dose rate adust
                        let navigate = None

                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous &&
                                                itms |> Array.length > 0 ->
                            let dispatch = if useAdjust then ChangeSubstanceRateAdjust >> dispatch else ChangeSubstanceRate >> dispatch

                            let warning = 
                                if useAdjust then
                                    itms[i].Dose.RateAdjust.Level |> getWarning
                                else
                                    itms[i].Dose.Rate.Level |> getWarning

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
                            |> select false (Terms.``Order Adjusted dose`` |> getTerm "dosering") None dispatch navigate true warning
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
                    }
                    {preparationDivider}
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
                                        cmp.OrderableQuantity.Variable.Min.IsSome &&
                                        cmp.OrderableQuantity.Variable.Incr.IsSome &&
                                        cmp.OrderableQuantity.Variable.Max.IsSome ||
                                        c >= 1

                                if not show then None
                                else
                                    let solved = ord |> isSolved
                                    let navigable = 
                                        cmp 
                                        |> Option.map (_.OrderableQuantity >> OrderVariable.isNavigable)
                                        |> Option.defaultValue false

                                    createNav navigable solved 
                                        SetMinComponentQuantityProperty
                                        DecreaseComponentQuantityProperty
                                        SetMedianComponentQuantityProperty
                                        IncreaseComponentQuantityProperty
                                        SetMaxComponentQuantityProperty

                            let warning =
                                cmp
                                |> Option.bind (_.OrderableQuantity.Level >> getWarning)

                            vals
                            |> select false "bereiding hoeveelheid" None (ChangeComponentOrderableQuantity >> dispatch) navigate false warning
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
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

                                if itm.ComponentConcentration.DefinedConstraints.Vals 
                                   |> Option.map (fun vu -> vu.Value |> Array.length > 1)
                                   |> Option.defaultValue false
                                then
                                    itm.ComponentConcentration.Variable.Vals
                                    |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}"))
                                    |> Option.defaultValue [||]
                                    |> select false "product sterkte" None (change >> dispatch) None false None
                                else JSX.jsx $"<></>"
                            | None ->
                                match
                                    ord.Orderable.Components
                                    |> Array.tryFind (fun c -> state.SelectedComponent.IsNone || c.Name = state.SelectedComponent.Value) with
                                | Some cmp ->
                                    match cmp.Items |> Array.tryFind (fun i -> i.Name = cmp.Name) with
                                    | Some itm ->
                                        let change = fun s -> (cmp.Name, itm.Name, s) |> ChangeSubstanceComponentConcentration


                                        if itm.ComponentConcentration.DefinedConstraints.Vals 
                                           |> Option.map (fun vu -> vu.Value |> Array.length > 1)
                                           |> Option.defaultValue false 
                                        then
                                            itm.ComponentConcentration.Variable.Vals
                                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d} {v.Unit}"))
                                            |> Option.defaultValue [||]
                                            |> select false "product sterkte" None (change >> dispatch) None false None
                                        else JSX.jsx $"<></>"

                                    | None ->
                                        [||]
                                        |> select true "" None ignore None false None
                                | None ->
                                    [||]
                                    |> select true "" None ignore None false None

                        | _ ->
                            [||]
                            |> select true "" None ignore None false None

                    }
                    {
                        // substance orderable quantity
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous &&
                                                itms |> Array.length > 0 &&
                                                ord.Orderable.Components |> Array.length > 1 ->
                            let warning = itms[i].OrderableQuantity.Level |> getWarning

                            itms[i].OrderableQuantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false $"{itms[i].Name} hoeveelheid" None (ChangeSubstanceOrderableQuantity >> dispatch) None false warning
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
                    }
                    {
                        // substance orderable concentration
                        match substIndx, state.Order with
                        | Some i, Some ord when ord.Schedule.IsContinuous |> not &&
                                                itms |> Array.length > 0 &&
                                                ord.Orderable.Components |> Array.length > 1 ->
                            let warning = itms[i].OrderableQuantity.Level |> getWarning                            

                            itms[i].OrderableConcentration.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 3} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false $"{itms[i].Name} concentratie" None (ChangeSubstanceOrderableConcentration >> dispatch) None false warning
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
                    }
                    {
                        // orderable quantity
                        match state.Order with
                        | Some ord when ord.Orderable.Components |> Array.length > 1 ->
                            let warning = ord.Orderable.OrderableQuantity.Level |> getWarning

                            ord.Orderable.OrderableQuantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false "totale hoeveelheid" None (ChangeOrderableQuantity >> dispatch) None false warning
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
                    }
                    {administrationDivider}
                    {
                        // frequency
                        match state.Order with
                        | Some ord  when ord.Schedule.IsDiscontinuous ||
                                         ord.Schedule.IsTimed ->
                            let xs =
                                ord.Schedule.Frequency.Variable.Vals
                                |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                                |> Option.defaultValue [||]

                            let navigate =
                                if xs |> Array.length <> 1 then None
                                else
                                    let solved = ord |> isSolved
                                    let navigable = false

                                    createNav navigable solved 
                                        SetMinFrequencyProperty
                                        (fun _ -> DecreaseFrequencyProperty)
                                        SetMedianFrequencyProperty
                                        (fun _ -> IncreaseFrequencyProperty)
                                        SetMaxFrequencyProperty

                            let warning = ord.Schedule.Frequency.Level |> getWarning

                            select false (Terms.``Order Frequency`` |> getTerm "frequentie") None (ChangeFrequency >> dispatch) navigate false warning xs
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
                    }
                    {
                        // orderable dose quantity
                        match state.Order with
                        | Some ord when ord.Schedule.IsContinuous |> not ->
                            // only show nav if all components have
                            // a distinct orderable quantity
                            let showNav =
                                ord.Orderable.Components
                                |> Array.forall (fun cmp ->
                                    cmp.OrderableQuantity.Variable.Vals
                                    |> Option.map (fun vu ->
                                        vu.Value |> Array.length = 1
                                    )
                                    |> Option.defaultValue false
                                )

                            let navigate =
                                if not showNav then None
                                else
                                    let canIncr =
                                        ord.Orderable.Components |> Array.length = 1 ||
                                        ord.Orderable.DoseCount.Variable.Vals
                                        |> Option.map (fun vu ->
                                            vu.Value
                                            |> Array.map snd
                                            |> Array.forall (fun v -> v > 1m)
                                        )
                                        |> Option.defaultValue false

                                    let solved = ord |> isSolved
                                    let navigable = 
                                        ord.Orderable.Dose.Quantity
                                        |> OrderVariable.isNavigable
                                    // specific case where increase is maximized by dose count
                                    {|
                                        first = 
                                            if navigable then (fun () -> SetMinDoseQuantityProperty |> dispatch) |> Some
                                            elif solved then (fun () -> 2 |> DecreaseDoseQuantityProperty |> dispatch) |> Some
                                            else None
                                        decrease =
                                            if solved then (fun () -> 1 |> DecreaseDoseQuantityProperty |> dispatch) |> Some
                                            else None
                                        median = 
                                            if navigable then (fun () -> SetMedianDoseQuantityProperty |> dispatch) |> Some
                                            else None
                                        increase = 
                                            if solved && canIncr then (fun () -> 1 |> IncreaseDoseQuantityProperty |> dispatch) |> Some
                                            else None
                                        last = 
                                            if navigable then (fun () -> SetMaxDoseQuantityProperty |> dispatch) |> Some
                                            elif solved && canIncr then (fun () -> 2 |> IncreaseDoseQuantityProperty |> dispatch) |> Some
                                            else None
                                    |}
                                    |> Some

                            let warning = ord.Orderable.Dose.Quantity.Level |> getWarning

                            ord.Orderable.Dose.Quantity.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false "toedien hoeveelheid" None (ChangeOrderableDoseQuantity >> dispatch) navigate false warning
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
                    }
                    {
                        // orderable dose rate
                        match state.Order with
                        | Some ord when ord.Schedule.IsContinuous ||
                                        ord.Schedule.IsTimed ||
                                        ord.Schedule.IsOnceTimed ->
                            let solved = ord |> isSolved
                            let navigable = ord.Orderable.Dose.Rate |> OrderVariable.isNavigable

                            let navigate =
                                createNav navigable solved 
                                    SetMinDoseRateProperty
                                    DecreaseDoseRateProperty
                                    SetMedianDoseRateProperty
                                    IncreaseDoseRateProperty
                                    SetMaxDoseRateProperty

                            let warning = ord.Orderable.Dose.Rate.Level |> getWarning

                            ord.Orderable.Dose.Rate.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> string} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> select false (Terms.``Order Drip rate`` |> getTerm "inloop snelheid") None (ChangeOrderableDoseRate >> dispatch) navigate false warning
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
                    }
                    {
                        // administration time
                        match state.Order with
                        | Some ord ->
                            let warning = ord.Schedule.Time.Level |> getWarning

                            ord.Schedule.Time.Variable.Vals
                            |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> fixPrecision 2} {v.Unit}"))
                            |> Option.defaultValue [||]
                            |> Array.distinctBy snd
                            |> select false (Terms.``Order Administration time`` |> getTerm "inloop tijd") None (ChangeTime >> dispatch) None true warning
                        | _ ->
                            [||]
                            |> select true "" None ignore None false None
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


