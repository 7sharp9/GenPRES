namespace ServerApi


module Command =

    open Shared.Api

    open Informedica.Utils.Lib


    // SECURITY: validatePassword uses CryptographicOperations.FixedTimeEquals
    // so equal-length password comparisons do not leak information through
    // per-byte timing differences. Per .NET docs, FixedTimeEquals short-
    // circuits when the byte arrays differ in length; the (small) length-
    // leak is acceptable here because the production startup check enforces
    // a minimum 16-character GENPRES_PASSWORD and the proper fix is to
    // migrate this code path away from raw-password-on-the-wire entirely
    // (tracked by TODO(D4 follow-up) in ServerApi.Services.fs).
    //
    // The `Option.filter (IsNullOrWhiteSpace >> not)` step is essential:
    // `Env.getItem` returns `Some ""` when an env var is set but empty,
    // which is what the Dockerfile does with `ENV GENPRES_PASSWORD=` for
    // Plesk-style runtime injection. Without the filter, `password = ""`
    // (or `FixedTimeEquals(getBytes(""), getBytes(""))`) would evaluate to
    // `true` and an empty-password login request would issue a valid
    // 1-hour HMAC token. The filter coerces empty/whitespace to `None` so
    // the fail-closed `Option.defaultValue false` branch fires.
    let private validatePassword (password: string) =
        Env.getItem "GENPRES_PASSWORD"
        |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
        |> Option.map (fun expected ->
            System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(password),
                System.Text.Encoding.UTF8.GetBytes(expected)
            )
        )
        |> Option.defaultValue false


    let private tokenLifetime = System.TimeSpan.FromHours(1.0)


    let private generateToken () =
        // SECURITY: same `Option.filter` as validatePassword — an empty
        // GENPRES_PASSWORD must NOT be used as the HMAC key, otherwise the
        // token would be signed with an attacker-known key and trivially
        // forgeable. Coerce empty/whitespace to `None` so the empty-string
        // return path fires and login is impossible.
        match
            Env.getItem "GENPRES_PASSWORD"
            |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
        with
        | None -> ""
        | Some secret ->
            let expiresAt = System.DateTimeOffset.UtcNow.Add(tokenLifetime).ToUnixTimeSeconds()

            let nonceBytes = Array.zeroCreate<byte> 32
            System.Security.Cryptography.RandomNumberGenerator.Fill nonceBytes
            let nonce = System.Convert.ToBase64String(nonceBytes)
            let payload = $"%d{expiresAt}:%s{nonce}"
            let payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload)

            use hmac =
                new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret))

            let signatureBytes = hmac.ComputeHash(payloadBytes)
            let encodedPayload = System.Convert.ToBase64String(payloadBytes)
            let encodedSignature = System.Convert.ToBase64String(signatureBytes)
            $"%s{encodedPayload}.%s{encodedSignature}"


    let private validateToken (token: string) =
        if System.String.IsNullOrWhiteSpace(token) then
            false
        else
            // SECURITY: same `Option.filter` as validatePassword/generateToken —
            // an empty/whitespace GENPRES_PASSWORD must NOT be accepted as a
            // valid HMAC key, otherwise any token signed with the empty key
            // would verify. Coerce empty to `None` so the fail-closed branch
            // fires.
            match
                Env.getItem "GENPRES_PASSWORD"
                |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
            with
            | None -> false
            | Some secret ->
                let parts = token.Split('.')

                if parts.Length <> 2 then
                    false
                else
                    try
                        let payloadBytes = System.Convert.FromBase64String(parts[0])
                        let providedSig = System.Convert.FromBase64String(parts[1])

                        use hmac =
                            new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret))

                        let expectedSig = hmac.ComputeHash(payloadBytes)

                        if
                            not (
                                System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                                    providedSig,
                                    expectedSig
                                )
                            )
                        then
                            false
                        else
                            let payload = System.Text.Encoding.UTF8.GetString(payloadBytes)
                            let colonIdx = payload.IndexOf(':')

                            if colonIdx < 0 then
                                false
                            else
                                match System.Int64.TryParse(payload.Substring(0, colonIdx)) with
                                | true, expiresAt -> System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() <= expiresAt
                                | false, _ -> false
                    with _ ->
                        false


    let processCmd (env: AppEnv) cmd =
        match cmd with
        | InteractionCmd GetDrugNames ->
            async {
                let! result = env.interaction.getDrugNames ()
                return result |> Result.map (List.toArray >> DrugNamesLoaded >> InteractionResp)
            }
        | LogAnalyzerCmd(ValidatePassword password) ->
            async {
                if validatePassword password then
                    let token = generateToken ()
                    return Ok(PasswordValidated(true, token) |> LogAnalyzerResp)
                else
                    return Ok(PasswordValidated(false, "") |> LogAnalyzerResp)
            }
        | LogAnalyzerCmd(ListLogFiles token) ->
            if not (validateToken token) then
                async { return Error [| "Invalid token" |] }
            else
                async {
                    let! result = env.logAnalyzer.listLogFiles ()
                    return result |> Result.map (LogFilesListed >> LogAnalyzerResp)
                }
        | LogAnalyzerCmd(AnalyzeLogFile(token, fileName)) ->
            if not (validateToken token) then
                async { return Error [| "Invalid token" |] }
            else
                async {
                    let! result = env.logAnalyzer.analyzeLogFile fileName
                    return result |> Result.map (LogFileAnalyzed >> LogAnalyzerResp)
                }
        | _ ->
            match env.requireLoaded () with
            | Some msgs -> async { return Error msgs }
            | None ->
                match cmd with
                | OrderContextCmd(ctxCmd, ctx) ->
                    async {
                        let! result = env.orderContext.evaluate ctxCmd ctx
                        return result |> Result.map (OrderContextResult >> OrderContextResp)
                    }

                | OrderPlanCmd(UpdateOrderPlan(tp, cmdOpt)) ->
                    async {
                        let! result = env.orderPlan.updateOrderPlan tp cmdOpt
                        return result |> Result.map (OrderPlanUpdated >> OrderPlanResp)
                    }

                | OrderPlanCmd(FilterOrderPlan tp) ->
                    async {
                        let! result = env.orderPlan.filterOrderPlan tp
                        return result |> Result.map (OrderPlanFiltered >> OrderPlanResp)
                    }

                | FormularyCmd form ->
                    async {
                        let! result = env.formulary.getFormulary form
                        return result |> Result.map FormularyResp
                    }

                | ParenteraliaCmd par ->
                    async {
                        let! result = env.formulary.getParenteralia par
                        return result |> Result.map ParenteraliaResp
                    }

                | NutritionPlanCmd(InitNutritionPlan patient) ->
                    async {
                        let! result = env.nutritionPlan.initNutritionPlan patient
                        return result |> Result.map (NutritionPlanInitialised >> NutritionPlanResp)
                    }

                | NutritionPlanCmd(UpdateNutritionOrderContext(plan, label, ctx)) ->
                    async {
                        let! result = env.nutritionPlan.updateNutritionOrderContext (plan, label, ctx)
                        return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                    }

                | NutritionPlanCmd(SelectNutritionOrderScenario(plan, label, ctx)) ->
                    async {
                        let! result = env.nutritionPlan.selectNutritionOrderScenario (plan, label, ctx)
                        return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                    }

                | NutritionPlanCmd(NavigateNutritionOrderContext(plan, label, ctxCmd, ctx)) ->
                    async {
                        let! result = env.nutritionPlan.navigateNutritionOrderContext (plan, label, ctxCmd, ctx)
                        return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                    }

                | NutritionPlanCmd(AddNutritionContext(plan, category)) ->
                    async {
                        let! result = env.nutritionPlan.addNutritionContext (plan, category)
                        return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                    }

                | NutritionPlanCmd(RemoveNutritionContext(plan, id)) ->
                    async {
                        let! result = env.nutritionPlan.removeNutritionContext (plan, id)
                        return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                    }

                | InteractionCmd(CheckInteractions drugs) ->
                    async {
                        let! result = env.interaction.checkInteractions drugs
                        return result |> Result.map (List.toArray >> InteractionsChecked >> InteractionResp)
                    }

                | _ ->
                    async {
                        return
                            Error
                                [|
                                    $"Unexpected command after requireLoaded: {cmd |> Command.toString}"
                                |]
                    }
