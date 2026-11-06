# Orchestra

*Man and bot, playing together.*

Process documentation for a business, with a chatbot that answers from it.

Folders hold **processes**, processes hold ordered **steps**, and each step names the
people responsible and can carry a screenshot. The Chat section puts a model on top of
that record and lets staff ask questions of it in plain language — grounded strictly in
what has actually been written up, so a question the documentation cannot answer comes
back as "that is not written up" rather than a plausible invention.

---

## Running it

Needs the **.NET 9 SDK** and **SQL Server LocalDB** (ships with Visual Studio; otherwise
install SQL Server Express with LocalDB).

```bash
dotnet run --project src/TeamPortal.Web
```

The app is **Orchestra**; the solution and namespaces are still `TeamPortal`, from
before it was named. That is a rename of every project, namespace and folder for no
behavioural gain, so it has not been done - the two names refer to the same thing.

The database is created and migrated on first start, then seeded with a small worked
example — a Client Onboarding folder, its processes and steps, and a handful of staff.
Sign in with the administrator from configuration (see below).

If the app cannot reach the database, LocalDB has usually stopped:

```bash
sqllocaldb stop MSSQLLocalDB; sqllocaldb start MSSQLLocalDB
```

## Configuration

Everything non-secret lives in `src/TeamPortal.Web/appsettings.json`.

**Secrets belong in user secrets, never in that file.** They are committed otherwise, and
git history keeps them long after the line is deleted.

```bash
cd src/TeamPortal.Web
dotnet user-secrets set "Smtp:Password"    "..."
dotnet user-secrets set "SeedAdmin:Password" "..."
dotnet user-secrets set "Chat:Gemini:ApiKey" "..."
```

| Section | What it does |
|---|---|
| `ConnectionStrings:Portal` | The database. LocalDB by default. |
| `App:BaseUrl` | Used to build the links inside confirmation emails. |
| `Smtp` | Outgoing mail: confirmations, password resets, approval notices. Port 465 is implicit TLS. |
| `SeedAdmin` | The administrator created on first run. |
| `Storage:RootPath` | Where uploaded screenshots go. Deliberately outside `wwwroot` — they are served through `/media` behind authentication. |
| `Chat` | Which model answers the chat. See below. |

### The chat

`Chat:Provider` picks who answers:

- **`Portal`** — the built-in retriever. No API key, no network call, and it cannot say
  anything that is not in the record. This is the default.
- **`Gemini`** / **`OpenAi`** — a real model, grounded in the same record.

Each provider has its own block (`Chat:Gemini`, `Chat:OpenAi`) holding an `ApiKey` and a
`Model`, so both sets of credentials can sit side by side and switching is one word.

Whatever is configured, the retriever stays registered underneath as a fallback. An
expired key, a spent quota or a dead network makes the chat do *less* — it answers from
the portal and says so — rather than failing every question until somebody notices.

What the model receives is the export shown on the **My Training Data** screen. That is
deliberate rather than convenient: what you can read on that page is exactly what the
model is working from, so when an answer looks wrong there is one place to go and check.

## Layout

```
src/
  TeamPortal.Domain           entities, no dependencies
  TeamPortal.Application      service interfaces, DTOs, validators, the retriever
  TeamPortal.Mock             in-memory implementations
  TeamPortal.Infrastructure   EF Core, Identity, SMTP, file storage, model providers
  TeamPortal.Web              Blazor Server UI
tests/
  TeamPortal.Services.Tests   contract tests, run against BOTH implementations
  TeamPortal.Infrastructure.Tests
  TeamPortal.Web.Tests        bUnit component tests
wiki/                         plain-language wiki pages and their screenshots
docs/
  developer/                  build plan and data model
build/                        lint scripts used by CI
deploy/                       Azure Bicep template
```

The wiki in `wiki/` is written for newcomers. Start at `wiki/Home.md`.

### The seam

The interfaces in `TeamPortal.Application.Abstractions` are implemented twice: once
in-memory by `TeamPortal.Mock`, once over EF Core by `TeamPortal.Infrastructure`. The
screens were built against the mock and the database was swapped in behind them without
the UI changing.

`Program.cs` is the whole of that swap — one call to `AddSqlData(connectionString)` where
Part A called `AddMockData()`.

It survives because the contract tests are abstract classes run against **both** sides:

```csharp
public sealed class MockChatTests : ChatContractTests { ... }
public sealed class SqlChatTests  : ChatContractTests { ... }
```

One implementation behaving like another is a claim, not a guarantee, and this is what
checks it.

## Tests

```bash
dotnet test
```

859 across the three projects, plus two skipped. Notes for anyone adding to them:

- **AwesomeAssertions**, not FluentAssertions. FluentAssertions 8+ requires a paid
  commercial licence, so it is pinned to `0.0.0-banned` in `Directory.Packages.props`.
  Leave that ban in place.
- **SQLite in-memory**, not the EF InMemory provider — also banned. InMemory does not
  enforce relational constraints, so it passes tests that a real database fails.
- The test host occasionally hangs *after* reporting success. The run is green; it is a
  shutdown problem, not a failure.

## CI

`azure-pipelines.yml` gates every pull request: restore, build with warnings as errors,
format check, tests at 80% line coverage, a MudBlazor-first lint, a check that no `.razor`
file carries an `@code` block, and an accessibility scan.

## Conventions

Longer form in `docs/developer/`, particularly `DataModel.md` and `BuildPlan.md`. The two that
bite hardest:

- **No `@code` blocks in `.razor` files.** Every component has a `.razor.cs` code-behind.
- **Never put a Razor comment inside an element's attribute list.** It is not a comment
  there — Razor emits it as an attribute *name*, `setAttribute` throws
  `InvalidCharacterError`, and the circuit dies on every render.

## Licence

None yet. Without a licence file nobody has permission to use this, which matters the
moment the repository becomes public.
