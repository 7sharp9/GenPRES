/// Phase 0: Fix Agent.fs timeout bug (Greptile finding from PR #177)
///
/// Problem: Agent.postAndReply falls back to tryPostAndReply with a
/// hardcoded 1-second timeout when DefaultTimeout = Timeout.Infinite.
/// This causes slow operations (resource loading, constraint solving)
/// to fail with "Timed out waiting for reply".
///
/// Fix: Increase the fallback timeout to 30 seconds (30_000 ms).
/// The postAndReply function already has a fast-path when DefaultTimeout
/// is set to a specific value, so this only affects agents that haven't
/// configured a timeout (i.e. left at Infinite).

#I __SOURCE_DIRECTORY__

#load "load.fsx"

open System.Threading
open Informedica.Agents.Lib


// ============================================================
// 1. Demonstrate the bug: 1-second timeout is too short
// ============================================================

// Create an agent that takes 2 seconds to reply (simulating slow work)
let slowAgent =
    Agent.createReply<string, string>(fun msg ->
        Thread.Sleep(2000) // simulate slow operation
        $"processed: {msg}"
    )

// This will fail with the current 1-second timeout because
// DefaultTimeout is Timeout.Infinite by default
printfn $"DefaultTimeout = {slowAgent |> Agent.getDefaultTimeout}"
printfn $"Timeout.Infinite = {Timeout.Infinite}"

let result1 =
    try
        slowAgent
        |> Agent.postAndReply "slow-request"
        |> Some
    with ex ->
        printfn $"BUG: {ex.Message}"
        None

printfn $"Result with 1s timeout (should fail): {result1}"


// ============================================================
// 2. Workaround: set DefaultTimeout explicitly
// ============================================================

slowAgent |> Agent.setDefaultTimeout 30_000

let result2 =
    try
        slowAgent
        |> Agent.postAndReply "slow-request-with-timeout"
        |> Some
    with ex ->
        printfn $"Error: {ex.Message}"
        None

printfn $"Result with 30s timeout (should succeed): {result2}"

slowAgent |> Agent.dispose


// ============================================================
// 3. Test the fix: after patching Agent.fs line 289
//    Change: tryPostAndReply 1000 → tryPostAndReply 30_000
// ============================================================

// After the fix, this should work without setting DefaultTimeout:
let slowAgent2 =
    Agent.createReply<string, string>(fun msg ->
        Thread.Sleep(2000)
        $"processed: {msg}"
    )

let result3 =
    try
        slowAgent2
        |> Agent.postAndReply "test-after-fix"
        |> Some
    with ex ->
        printfn $"Still failing after fix: {ex.Message}"
        None

printfn $"Result after fix (should succeed): {result3}"

slowAgent2 |> Agent.dispose


// ============================================================
// 4. Verify fast agents still work fine
// ============================================================

let fastAgent =
    Agent.createReply<int, int>(fun n -> n * 2)

let result4 = fastAgent |> Agent.postAndReply 21
printfn $"Fast agent result (should be 42): {result4}"

fastAgent |> Agent.dispose


// ============================================================
// 5. Test stateful agent with slow init
// ============================================================

let statefulAgent =
    Agent.createStatefulReply<string, string, int>(
        0,
        fun state msg ->
            Thread.Sleep(1500) // simulate moderate work
            let newState = state + 1
            $"call #{newState}: {msg}", newState
    )

let result5 =
    try
        statefulAgent
        |> Agent.postAndReply "stateful-test"
        |> Some
    with ex ->
        printfn $"Stateful agent failed: {ex.Message}"
        None

printfn $"Stateful agent result (should succeed after fix): {result5}"

statefulAgent |> Agent.dispose

printfn "\nAll tests complete."
