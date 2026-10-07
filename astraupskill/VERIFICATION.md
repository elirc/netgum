# Verification

Run from the repository root (the folder holding `Netgum.sln`):

```powershell
$env:DOTNET_ROLL_FORWARD = "Major" # only when the installed runtime is newer than net8.0
dotnet restore Netgum.sln
dotnet test Netgum.sln --configuration Release --no-restore
dotnet build Netgum.sln --configuration Release --no-restore
```

The SDK is pinned by `global.json` at the project root (`10.0.302`, `rollForward: latestFeature`). The *SDK* pin is not the same as a *runtime* for the target framework: the projects target `net8.0`, so with only the .NET 10 runtime installed the test host aborts with "You must install or update .NET to run this application" until `DOTNET_ROLL_FORWARD=Major` is set. Installing the .NET 8 runtime is the happy path; the roll-forward is the fallback, and it is what the recorded runs used.

A later fix pass (documented in chapter 03) added `tests/Netgum.Tests/Domain/EmailAddressTests.cs` with three cases covering the `EmailAddress` parser and its preserved inner exception. Observed on 2026-09-17 with SDK 10.0.302 and `DOTNET_ROLL_FORWARD=Major`:

```
dotnet test Netgum.sln --configuration Release --no-build
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 13 s - Netgum.Tests.dll (net8.0)
```

The evidence files below record the earlier 11/11 run; 11 is the delivered count and 14 is the count after the three added parser tests. The `cwd` field inside the evidence JSON names the staging directory the delivery run used (`...\astra-remaining-improvements\stage\028086674b89`), not a path you need; the same commands run from this project root, as the 14/14 line above shows.

The focused application suite uses EF Core InMemory and counting gateway/email/unit-of-work doubles. It reports 8 cases: the original checkout success, two invalid-customer rows, sold-out, inactive, expired-coupon, paid-terminal and failed-terminal (counted from `[Fact]`/`[Theory]`/`[InlineData]` in `CheckoutServiceTests.cs`). The central runner records the actual installed runtime and exit code; a Major roll-forward is local execution evidence, not native .NET 8 coverage.

Original changed files are preserved byte-for-byte under `astraupskill/snapshots/`: checkout service, checkout tests, email address, order, product, product variant, and product tests (seven files). The course files are UTF-8 additions. Do not include `bin`, `obj`, a database file, payment credentials, or real email configuration in delivery.

Scope limits are explicit. Fake payments and console email are not external integrations. EF InMemory does not prove relational constraints, SQL transactions, cross-context stock concurrency, or provider compensation. The post-await revalidation reduces stale state but cannot make a distributed payment and database transaction atomic. Creator mutation endpoints have no seller authentication, and download authorization is based on paid order ID. The future idempotency, webhook, owner-check, and outbox designs are teaching extensions, not implemented behavior.

For a focused repeat, run `dotnet test tests/Netgum.Tests/Netgum.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~CheckoutServiceTests`. Keep commands finite and do not call real payment, email, migration, or production automation.

The recorded focused run used `DOTNET_ROLL_FORWARD=Major` because the available test host was newer than the target. Its nonempty console log reports 11 total tests and 11 passes, and the TRX file is emitted under the test project's `TestResults` directory. That is execution evidence for this local environment; it does not change the project target or establish native .NET 8 runtime coverage. The restore record is separate from the test record so a future learner can distinguish package resolution from test execution.

The authoritative [results summary](evidence/results.json), [test run record](evidence/luna-test-10.json), [TRX counters](evidence/netgum-tests.trx) (`total="11" passed="11"`), and [Release build record](evidence/root-build-01.json) are included. The JSON records name `.log` files that were not committed, so the TRX is the only per-test evidence in the repository. Eleven discovered cases passed in that 2026-09-12 run: eight application cases and three domain cases (the `EmailAddressTests` came later). Earlier failed or empty-output attempts are not the final evidence. No tests were added for production identity, the email failure window, two simultaneous pending completions, or relational rollback. Read the failure-ledger limits in the solutions before treating sequential idempotency as a stronger guarantee.
