// markdown-lint.fsx
// Runs markdownlint-cli2 on staged Markdown files as a pre-commit warning check.
// Always exits 0 so it never blocks a commit — issues are printed as warnings only.
open System
open System.Diagnostics
open System.Runtime.InteropServices

let stagedFiles = fsi.CommandLineArgs |> Array.skip 1

if stagedFiles.Length = 0 then
    exit 0

let isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
let npx = if isWindows then "npx.cmd" else "npx"
let fileArgs = stagedFiles |> String.concat " "

let psi = ProcessStartInfo(npx, $"--yes markdownlint-cli2 {fileArgs}")
psi.UseShellExecute <- false

let proc = Process.Start(psi)
proc.WaitForExit()

if proc.ExitCode <> 0 then
    printfn ""
    printfn "⚠️  Markdown lint issues found above (non-blocking)."
    printfn "   Fix these to improve documentation quality."
    printfn "   Run 'npx markdownlint-cli2 <file>' for details."
    printfn ""

// Non-blocking: always exit 0 regardless of lint results.
exit 0
