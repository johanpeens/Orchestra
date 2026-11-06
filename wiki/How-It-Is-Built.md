# How It Is Built

This page is for people who want to peek under the bonnet. You do not need it to *use* Orchestra.

## The short version

Orchestra is one website. It is built in **layers**, like a cake. Each layer only talks to the layer below it. That keeps the code tidy and easy to test.

| Layer | Folder | Job |
|---|---|---|
| **Website** | `src/TeamPortal.Web` | The pages, buttons and windows you see. |
| **Rules** | `src/TeamPortal.Application` | The list of things the app can do (called *interfaces*), plus the checks on what people type. |
| **Real work** | `src/TeamPortal.Infrastructure` | Talks to the database, sends email, stores files, calls the AI. |
| **Things** | `src/TeamPortal.Domain` | The basic objects: process, step, employee, department. |
| **Practice version** | `src/TeamPortal.Mock` | A pretend copy that keeps everything in memory. Good for tests. |

## The folders in the repository

```
Team Portal/
  README.md      the short intro
  wiki/          this wiki (plain language) and its pictures
  docs/
    developer/   the build plan and the data model, for people writing code
  src/           the code
  tests/         the checks that prove the code works
  build/         small scripts that check the code is tidy
  deploy/        a recipe for putting the app on Azure
```

## Two copies of the same plan

The app was first built with the **Practice version** (`Mock`). That way the screens could be made before the database was ready. Then the real database version (`Infrastructure`) was swapped in. The screens did not change at all.

To make sure both copies behave the same, the tests are written once and run against **both**. If the copies ever disagree, a test fails.

## What the app uses

| Part | Tool |
|---|---|
| Website | Blazor Server on .NET 9 |
| Look and feel | MudBlazor |
| Database | SQL Server, through Entity Framework Core |
| Sign-in | ASP.NET Core Identity |
| Email | MailKit |
| Checking input | FluentValidation |
| Tests | xUnit, bUnit and SQLite in memory |
| Putting it online | Azure Bicep and Azure Pipelines |

## Rules the team follows

- Every page has **two files**: one for how it looks (`.razor`) and one for what it does (`.razor.cs`). No code inside the look file.
- Use **MudBlazor** parts first instead of building your own.
- Never save a secret in the settings file. See [Settings and Secrets](Settings-and-Secrets.md).
- The build plan and data model are in `docs/developer`.

## How a click travels

1. You click a button in your browser.
2. The browser tells the server through a live connection (SignalR).
3. The page code asks a **service** to do the job.
4. The service checks your role, then opens the database and does it.
5. The answer travels back and the page updates.

## Where files go

Screenshots are saved on the server's disk, **outside** the public folder. The only way to see one is through the `/media` address, and that asks you to be signed in first.

## Tests

```bash
dotnet test
```

The tests are in three groups: service tests (run against both copies), database tests, and screen tests that check the pages.

## Putting it online

The `deploy` folder has an Azure Bicep recipe, and `azure-pipelines.yml` describes how every change is built, tested and checked before it is accepted.

See the [Glossary](Glossary.md) if any word here was new.
