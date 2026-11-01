# Order Intake AI – Repository Instructions

## What this repo is
A portfolio demo: PDF purchase orders arrive by e-mail, an AI service extracts them into structured JSON, a Power Automate cloud flow creates the order in an existing panel API (separate repo: `BartuCelik/order-inventory-panel`) and posts Teams alerts for low stock.

This repo (`BartuCelik/order-intake-ai`, private) contains **only**:
- `src/OrderIntake.Api/` – .NET minimal API exposing `POST /orders/parse`
- `tests/OrderIntake.Tests/` – xUnit tests
- `connectors/` – OpenAPI (Swagger 2.0) files for Power Automate custom connectors
- `flow/` – exported Power Platform solution (.zip), added later
- `samples/` – fictional order PDFs
- `docs/` – architecture diagram, screenshots

Order creation and stock logic live in the panel repo. Do not reimplement them here.

## End-to-end flow (owner's target, 2026-11-01)
E-mail with a PDF order → Power Automate → this API evaluates the request → if it is OK the order lands in the
panel, and it is tracked there.

1. The flow picks up the e-mail and sends the PDF to `POST /orders/parse` here.
2. This API judges the **document**: extraction, deterministic validation, `confidence`, `missingFields`.
   It is stateless and does not know the panel.
3. The flow sends the result to the panel, which judges it against **business data** (customer and SKUs exist,
   products are Finished, no duplicate `externalOrderNo`) and creates the sales order. The panel additions
   (order requests, exact lookups, external order number, service credential for the flow) are built in the panel repo.
4. Teams messages: order created, needs review, low stock.

Open decision (owner): whether requests that are not OK also land in the panel as a review queue, or are handled
only in Power Automate (Teams approval).

## Architecture rules
- The Power Automate flow only orchestrates. All AI extraction logic lives in this API.
- PDF text extraction: **PdfPig**. Text-based PDFs only (no OCR in v1). If no text can be extracted, return a ProblemDetails error, never an empty "successful" result.
- LLM: **Azure OpenAI** (`Azure.AI.OpenAI`) with structured output (strict JSON schema), temperature 0.
- Auth: `X-Api-Key` header, compared in constant time. `/health` and the OpenAPI document are anonymous.
- All errors are returned as RFC 7807 ProblemDetails.
- OpenAPI output must be **Swagger 2.0** (Power Automate custom connectors import 2.0). Every operation has an explicit `operationId` (e.g. `ParseOrder`) and summary.

## Parse response contract (do not change without updating the connector and flow)
```json
{
  "customerName": "",
  "customerEmail": "",
  "orderDate": "YYYY-MM-DD",
  "dueDate": "YYYY-MM-DD",
  "externalOrderNo": "",
  "items": [{ "sku": "", "description": "", "quantity": 0, "unit": "" }],
  "confidence": 0.0,
  "missingFields": []
}
```
- The LLM must never invent values. Unknown string fields are `""` (never `null`, so the flow's Parse JSON step does not break) and their path is added to `missingFields` (e.g. `"externalOrderNo"`, `"items[1].sku"`).
- `dueDate` (added 2026-11-01, the panel requires it on a sales order) follows the same rule: not in the PDF → `""` and listed in `missingFields`; it is never derived from `orderDate`. A `dueDate` before `orderDate` is invalid.
- `confidence` is 0–1. The API does **not** trust the model's self-reported value blindly: deterministic validation (missing required fields, quantity <= 0, invalid date, invalid e-mail) adds to `missingFields` and caps `confidence`.
- Flow decision rule: `missingFields` not empty → manual review (no order created); otherwise `confidence < 0.8` → human approval; otherwise the order is created automatically.

## Code conventions
- C#, latest language features, nullable enabled, file-scoped namespaces.
- `TreatWarningsAsErrors` and `AnalysisLevel latest-recommended` (`Directory.Build.props`), like the panel — keep the build clean.
- Tests: xUnit v3 on Microsoft.Testing.Platform (`global.json`); names `Method_Scenario_ExpectedResult`.
- Code, identifiers, comments, commit messages and README: **English**. Commits: Conventional Commits.
- Small, focused services behind interfaces (`IPdfTextExtractor`, `IOrderExtractor`, `IOrderValidator`) so they can be unit tested; the LLM client is always mocked in tests.
- Options pattern for configuration; secrets via `dotnet user-secrets` locally and app settings in Azure. Never commit keys; keep `appsettings.json` free of secrets.
- Never log PDF contents or extracted customer data; log file name, size, timing, and outcome only.
- Prefer minimal dependencies. Ask before adding a new NuGet package not listed here.
  Approved: PdfPig, `Azure.AI.OpenAI`, xunit.v3.

## Data rules
- All test data is fictional (invented companies, SKUs, people, `.example` e-mail domains).
- Never use or reference real employer code, data, tenants or names.

## How to work
- Work in small steps. After each step: build, run tests, summarize what changed, and stop for review.
- If a requirement is ambiguous or a design choice looks weak, say so before implementing.

## Current status
- [x] Step 1 — Skeleton: solution, minimal API with `/health`, `X-Api-Key` middleware (SHA-256 + `FixedTimeEquals`,
      key validated at startup, min. 32 chars), ProblemDetails, xUnit tests.
- [ ] Step 2 — OpenAPI as Swagger 2.0 (check whether the built-in generator + `Microsoft.OpenApi` can serialize 2.0
      before asking for Swashbuckle).
- [ ] PDF text extraction (PdfPig), size/page limit, validator, LLM extractor, `POST /orders/parse`.
