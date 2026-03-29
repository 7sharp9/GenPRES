---
description: "Fix a failing test in the GenPRES test suite"
---

Investigate and fix a failing test in the GenPRES test suite.

1. **Identify the failing test** — run the relevant test project:
   ```bash
   dotnet test tests/Informedica.<Library>.Tests/
   ```
   or run all server tests:
   ```bash
   dotnet run ServerTests
   ```

2. **Understand the failure** — read the error message carefully. Common issues:
   - Assertion mismatch: compare actual vs. expected values
   - Missing data: check if test data files/cache are present
   - Build error: run `dotnet build GenPRES.sln` first

3. **Locate the test** — tests are in `tests/Informedica.<Library>.Tests/` and use Expecto with Expecto.Flip:
   ```fsharp
   test "description" {
       actual
       |> Expect.equal "message" expected
   }
   ```

4. **Fix approach:**
   - If the test expectation is wrong → update the test assertion
   - If the production logic is wrong → prototype the fix in a `.fsx` script first, then request the user migrate it
   - If test data is stale → update test fixtures

5. **Verify the fix:**
   ```bash
   dotnet test tests/Informedica.<Library>.Tests/
   ```

6. **Do not modify `.fs` source files** (except test files) without explicit user approval. Prototype fixes in `.fsx` scripts instead.
