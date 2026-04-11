# ADR-0015: Security Baseline for the Demo Deployment

**Date**: 2026-04-11

**Status**: Accepted

**References**:

- [Security Review 2026-04-10](../../security/2026-04-10-security-review.md)
- [System Architecture (ADR-0001)](0001-system-architecture.md)
- [Clean SAFE Architecture (ADR-0007)](0007-clean-safe-architecture.md)

## Context

GenPRES is a medical-device-class clinical decision support system (CDSS).
A formal static security review was performed on 2026-04-10
([`docs/security/2026-04-10-security-review.md`](../../security/2026-04-10-security-review.md)),
followed by a live probe of the public demo at <https://genpres.nl/>. The
review grades every finding against three deployment contexts:

- **C1** — local development / public demo (current state)
- **C2** — on-prem hospital network (the realistic next step)
- **C3** — public-internet SaaS (aspirational)

The review identified open findings in three remediation buckets (§7.2,
§7.3, §7.4). Two implementation passes resolved the cheap, high-leverage
items at the application layer; the rest are deferred to architectural
work tracked in the existing roadmap. An MDR Design History record is
needed so future maintainers, reviewers, and auditors can locate the
authoritative security artefacts and understand which decisions were
deliberately taken at the demo level versus deferred to a non-demo
deployment.

## Decision

1. The 2026-04-10 security review document is the **authoritative source
   of truth** for GenPRES security findings, severity grading, and
   remediation status. All future security work must update that document
   in-place via dated `Update — YYYY-MM-DD` sections rather than spawning
   parallel review files.

2. The security baseline currently in force on the public demo
   <https://genpres.nl/> is the **post-2026-04-11 state** documented in
   the `Update — 2026-04-11` section of the review:
   - **L1** (binary mismatch in `Fable.Remoting.Giraffe 5.24` on .NET 10)
     mitigated by pinning `Giraffe = 6.4.0` and a `safeWebApi` wrapper
     that catches `MissingMethodException` / `TypeLoadException`.
   - **L2 / B5** legacy catch-all string replaced with a generic 404.
   - **B2** security response headers (HSTS, CSP, X-Content-Type-Options,
     X-Frame-Options, Referrer-Policy, Permissions-Policy) emitted via
     `securityHeadersMiddleware` registered through `app_config`.
   - **A2** per-IP fixed-window rate limiter
     (`Microsoft.AspNetCore.RateLimiting`, 60 requests / 10 s, partition
     keyed by `getClientIP` so `X-Forwarded-For` is honoured).
   - **B3** trusted-proxy allow-list wired via ASP.NET
     `ForwardedHeadersMiddleware` and the `GENPRES_TRUSTED_PROXIES`
     env var (defaults to `127.0.0.1, ::1`, matching the Plesk →
     Kestrel loopback hop on the public demo). Spoofed `X-Forwarded-
     For` from clients outside the allow-list is ignored, which also
     bounds the rate-limiter's partition cardinality.
   - **D2** SRI `sha384` integrity attribute on the external font-awesome
     stylesheet in `index.html`.

3. **A5** (clinical RPCs unauthenticated) is **intentionally not
   remediated** on the public demo. The decision is recorded inline in
   §4 of the security review under the A5 finding. A5 must be re-enabled
   before any deployment beyond C1 (demo) by reusing the existing
   `validateToken` helper at `ServerApi.Command.fs:72-121` behind a new
   `GENPRES_REQUIRE_AUTH=1` feature flag.

4. The deployment-time regression check for the security baseline is a
   live test suite maintained out-of-repo by the maintainer. It is
   deliberately not part of the repository because it encodes
   deployment assumptions (target URL, demo credentials, expected HTTP
   behaviour) rather than source-code invariants. The current suite
   verifies the items recorded in the `Update — 2026-04-11` section of
   the security review and is run before any production deploy.

5. The remaining items in §7.2, §7.3, and §7.4 of the security review
   remain open and **must** be addressed before any non-demo deployment,
   in particular:
   - **A1**, **A5**, **F1** (per-user identity, RBAC, tamper-evident
     audit trail) for any C2 / C3 rollout.
   - **B1** (TLS termination at the F# layer) if the deployment cannot
     guarantee an HTTPS-terminating reverse proxy.

## Consequences

- The security review document, the regression test suite, and this ADR
  together form a single chain of evidence: posture → verification → MDR
  record.
- Auditors and new contributors can find the authoritative security
  state in one place and trust that the MDR Design History references it.
- Any change to the demo's runtime security profile (headers, rate
  limit, CSP, auth) must be reflected in both the security review's
  `Update — YYYY-MM-DD` section and, if it changes the *baseline*, in a
  new ADR amending or superseding this one.
- The intentional A5 non-remediation is now traceable through both the
  security review and the Design History, so it cannot accidentally be
  inherited by a non-demo deployment.
- The risk-analysis files in `docs/mdr/risk-analysis/` are still the
  formal regulatory artefacts; the maintainer must map the resolved
  items into `risk-management-report.md` and the hazard-analysis
  spreadsheets through normal change control before any C2 deployment.
