```markdown Frontend\BuildPlan.md
# Phased Build Plan

## Purpose

The order in which the Team Portal gets built, with the tests each phase must ship.
Nothing here is optional: a phase is not finished until its tests are green and every
gate in `Frontend/CI.md` passes.

Read these first:

- `Frontend/DataModel.md` - the entities and the database.
- `Frontend/TeamConventions.md` - how components must be written.
- `docs/developer/UserFlow.html` - the screens and modals being built.

---

# The Two-Part Approach

The build splits in half.

**Part A - every screen, running on mock data.**
No database, no Identity, no migrations. The app runs from lists held in memory. By the
end of Part A you can click through all seven screens, open every modal, save, and see
the change appear - it just does not survive a restart.

**Part B - EF Core and Identity swapped in behind the same interfaces.**
The real implementations replace the mocks one service at a time. The screens do not
change. Nothing in `.razor` or `.razor.cs` is edited in Part B.

## Why this order

You find out whether a screen works by using it, not by reading a design. Building the UI
first means the layout, the empty states, the validation messages and the modal flow all
get discovered and fixed while they are cheap to change - before a single migration
exists to keep in step with them.

It also means the data model gets a second opinion. Part A will turn up fields the design
implies but `Frontend/DataModel.md` missed, and fields the model has that no screen ever
shows. Better to learn that from a working UI than from a schema that is already in SIT.

## The one thing that makes this work

**The service interfaces are the seam.** They are written once, in phase A1, and they do
not change in Part B. Both the mock and the EF Core implementation satisfy the same
contract, so swapping them is a one-line change in dependency injection:

```csharp
// Program.cs - Part A
builder.Services.AddMockData();

// Program.cs - Part B
builder.Services.AddSqlData(builder.Configuration.GetConnectionString("Portal")!);
```

If Part B has to change an interface, every component that used it needs revisiting and
the advantage is gone. Getting phase A1 right is the highest-value hour in the project.

## Four rules the mock services must obey

Sloppy mocks are worse than no mocks - they let the UI be written against behaviour the
real database will never reproduce.

1. **Async all the way.** Every method returns `Task<T>` and takes a
   `CancellationToken`, even though the mock answers instantly. A UI written against
   synchronous calls has no `await`, no loading spinner, and no cancellation - all of
   which have to be retrofitted in Part B.
2. **Enforce the real constraints.** The mock store rejects duplicate department names,
   rejects a second step with the same `StepNumber`, and returns steps in `StepNumber`
   order. Every rule in `Frontend/DataModel.md` that the database will enforce, the mock
   enforces too. Otherwise Part B is a week of surprises.
3. **Simulate latency and failure.** A configurable `Task.Delay(150)` on reads, and a
   switch that makes a save fail on demand. Without these nobody builds a disabled Save
   button, a spinner, or an error path - and those are the parts that break in production.
4. **Copy on read, copy on write.** Return a new DTO each time, never a reference into
   the backing list. EF Core will hand back detached copies; a mock that hands back live
   references lets the UI mutate "the database" by editing a bound field, and that bug
   only shows up after the swap.

---

# Ground Rules

| Decision | Choice | Why |
|----------|--------|-----|
| Hosting model | **Blazor Server** (`.NET 9`, interactive server render mode) | Real-time UI over SignalR, no API layer to write, the database stays behind the firewall |
| Component library | **MudBlazor** | Mandated by `Frontend/TeamConventions.md` |
| Data access | **EF Core 9, code-first** (Part B) | Migrations are reviewable in a PR |
| Database | SQL Server, LocalDB in DEV (Part B) | |
| Test runner | **xUnit** | |
| Assertions | **AwesomeAssertions** | Failure messages read like English. See the note below - this is deliberately *not* FluentAssertions |
| Fakes | **NSubstitute** | Less ceremony than Moq |
| Component tests | **bUnit** | Renders Blazor components without a browser |
| Coverage | **Coverlet**, 80% line minimum on Application + Infrastructure | `Frontend/CI.md` gate 4 |

## Why AwesomeAssertions and not FluentAssertions

FluentAssertions changed owner and licence at version 8. It is now Xceed commercial
software - the package's own metadata reads
`Copyright 2024-2026 Xceed Software Inc., all rights reserved`, and its project URL
points at a paid product page. Using it in this codebase would need a purchased
per-developer licence.

**AwesomeAssertions** is the Apache-2.0 community fork of FluentAssertions 7, with the
same namespace shape and the same API. Every `.Should().Be(...)` in this document
compiles against it unchanged, so no test in the plan needs rewriting.

`FluentAssertions` is pinned to a non-existent `0.0.0-banned` version in
`Directory.Packages.props`, so anyone who adds it by reflex gets a restore failure and
reads this section rather than quietly introducing a licence liability.

## The one Blazor Server trap you must not fall into

This bites in Part B, but decide it in phase B1.

A Blazor Server circuit lives for as long as the browser tab is open - often hours. A
`DbContext` is **not** thread-safe and is meant to live for one unit of work. Inject a
scoped `DbContext` straight into a component and two concurrent renders will use the same
context at the same time, and you get:

> A second operation was started on this context instance before a previous operation
> completed.

So: **never inject `PortalDbContext` into a component.** Register a factory instead, and
have services create and dispose a short-lived context per operation.

```csharp
builder.Services.AddDbContextFactory<PortalDbContext>(options =>
    options.UseSqlServer(connectionString));
```

The mock services in Part A are singletons with no such problem, which is precisely why
this rule has to be written down now rather than discovered in phase B2.

---

# Solution Layout

```text
TeamPortal.sln
|
+-- src/
|   +-- TeamPortal.Domain/           entities, enums, no dependencies       (Part B)
|   +-- TeamPortal.Application/      DTOs, service INTERFACES, validators   (Part A)
|   +-- TeamPortal.Mock/             in-memory store + mock services        (Part A)
|   +-- TeamPortal.Infrastructure/   DbContext, configurations, migrations  (Part B)
|   +-- TeamPortal.Web/              Blazor Server + MudBlazor              (Part A)
|
+-- tests/
    +-- TeamPortal.Mock.Tests/            mock services obey the contract   (Part A)
    +-- TeamPortal.Web.Tests/             bUnit component tests             (Part A)
    +-- TeamPortal.Domain.Tests/          pure logic, no database           (Part B)
    +-- TeamPortal.Application.Tests/     services against SQLite in-memory (Part B)
    +-- TeamPortal.Infrastructure.Tests/  mappings, migrations, seeding     (Part B)
```

Dependencies point inwards only. `Web` references `Application` and nothing else at
compile time - it never references `Mock` or `Infrastructure` directly, only their DI
extension method. That single restriction is what guarantees no screen can accidentally
depend on mock behaviour.

`TeamPortal.Mock` is **not** deleted at the end of Part B. It stays as the backing store
for the bUnit tests and for offline demos.

---

# Test Strategy

## Part A - what gets tested

| Layer | Test type | Covers |
|-------|-----------|--------|
| Mock | Plain unit | That the mock honours the contract: ordering, uniqueness, validation, copy semantics |
| Web | bUnit | Rendering, parameters, event callbacks, disabled states, error paths |

The bUnit tests written in Part A are the safety net for Part B. They run against the
mock services throughout, so if a Part B change breaks a screen, they catch it. **Do not
rewrite them in Part B.** If a Part A test has to change in Part B, an interface changed,
and that is a problem to fix rather than a test to update.

## Part B - what gets added

| Layer | Test type | Covers |
|-------|-----------|--------|
| Domain | Plain unit | Enum values, guard clauses, any calculation |
| Application | Integration against SQLite | Real services, transactions, ordering |
| Infrastructure | Integration | Entity configurations, indexes, delete behaviours, seeding |

### Which database do Part B tests use

**SQLite in-memory**, not the EF Core InMemory provider.

The InMemory provider is not a relational database. It ignores unique indexes, ignores
foreign keys, and ignores `HasFilter`. A test that "passes" against it will happily insert
two steps with the same `StepNumber` - exactly the bug the test was written to catch.
SQLite is a real relational engine, runs in-process, and starts in milliseconds.

```csharp
public sealed class DatabaseFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public IDbContextFactory<PortalDbContext> Factory { get; }

    public DatabaseFixture()
    {
        // ":memory:" lives only while the connection is open, so we hold it open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        Factory = new PooledDbContextFactory<PortalDbContext>(options);

        using var db = Factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
```

### The contract test suite

This is the trick that makes the swap safe. Write the behaviour tests **once**, against
the interface, in an abstract base class. Run them twice - once against the mock, once
against the EF Core implementation.

```csharp
public abstract class EmployeeServiceContractTests
{
    protected abstract Task<IEmployeeService> CreateSutAsync();

    [Fact]
    public async Task GetByDepartmentAsync_OrdersByFullName() { /* one implementation */ }

    [Fact]
    public async Task CreateAsync_WithBlankName_ReturnsValidationFailure() { /* ... */ }
}

// Part A
public class MockEmployeeServiceTests : EmployeeServiceContractTests
{
    protected override Task<IEmployeeService> CreateSutAsync() => /* mock */;
}

// Part B - not one line of test logic rewritten
public class SqlEmployeeServiceTests : EmployeeServiceContractTests
{
    protected override Task<IEmployeeService> CreateSutAsync() => /* SQLite-backed */;
}
```

Every difference between mock and database now shows up as a red test at exactly the
moment it is introduced.

## Naming and shape

`MethodName_Scenario_ExpectedResult`, arranged Arrange / Act / Assert:

```csharp
[Fact]
public async Task CreateStepAsync_WhenNameIsBlank_ReturnsValidationFailure()
{
    // Arrange
    var sut = await CreateSutAsync();
    var request = new CreateStepRequest(ProcessId: 1, Name: "   ");

    // Act
    var result = await sut.CreateStepAsync(request);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Errors.Should().ContainSingle(e => e.Field == nameof(request.Name));
}
```

## What is not worth testing

Do not write tests that assert EF Core works, that MudBlazor renders a button, or that a
property setter sets a property. Those tests cost time to write, time to run, and break
on every refactor while catching nothing. Test **your** rules.

---

# Phase Overview

| Phase | Delivers | Screens | Roughly |
|-------|----------|---------|---------|
| **Part A** | | | |
| A0 | Solution, CI pipeline, first green build | - | 2 days |
| A1 | DTOs, service interfaces, `Result<T>` - **the seam** | - | 2 days |
| A2 | Mock store and mock services, seeded with the design data | - | 3 days |
| A3 | App shell, theme, layout, shared components | - | 3 days |
| A4 | Login screen on a fake auth service | 01 | 2 days |
| A5 | Team screen, read-only | 05 | 3 days |
| A6 | Employee, department and unit modals | 06, 07 | 4 days |
| A7 | Process dashboard, read-only | 02 | 5 days |
| A8 | Add / Edit Process modal | 03 | 2 days |
| A9 | Add / Edit Step modal, owners, renumbering | 04 | 4 days |
| A10 | Screenshot upload against a mock file store | - | 2 days |
| A11 | Activity log, process history, notifications | - | 2 days |
| A12 | **Demo, review, freeze the interfaces** | all | 2 days |
| **Part B** | | | |
| B1 | Domain entities, DbContext, migration, seed | - | 4 days |
| B2 | EF Core services behind the same interfaces | - | 5 days |
| B3 | Identity swapped in for the fake auth service | 01 | 4 days |
| B4 | Real file storage swapped in | - | 2 days |
| B5 | Authorisation, accessibility, performance, release | - | 4 days |

Estimates assume one developer. They are planning numbers, not commitments.

---

# Definition of Done

Applies to **every** phase in both parts. No exceptions, no "we'll come back to it".

- [ ] `dotnet build -c Release` with zero warnings.
- [ ] `dotnet format --verify-no-changes` clean.
- [ ] `dotnet test` green, line coverage at or above 80% on Application, Mock and
      Infrastructure.
- [ ] Every component is a `.razor` + `.razor.cs` pair, no `@code { }` blocks.
- [ ] MudBlazor components used throughout, no raw HTML tables or hand-rolled dialogs.
- [ ] No hard-coded hex colours outside the theme file.
- [ ] Every `.razor.cs` carries the header comment from `Frontend/TeamConventions.md`.
- [ ] Screenshots attached to the PR for any UI change.
- [ ] Ticket number in the PR title, one peer approval, UI reviewer approval for UI work.

**Part B adds one more:** no file under `TeamPortal.Web/` may appear in the diff. If a
screen had to change, say so in the PR and explain why - it means an interface moved.

---

---

# PART A - SCREENS ON MOCK DATA

---

# Phase A0 - Solution & CI

**Goal:** an empty app that builds, runs, and is already guarded by the pipeline.

## Build

1. `dotnet new sln -n TeamPortal`.
2. Create `Application`, `Mock` and `Web` under `src`, and `Mock.Tests` and `Web.Tests`
   under `tests`. The Domain and Infrastructure projects wait for Part B.
3. Wire the project references. `Web` references `Application` only.
4. Add `Directory.Build.props` at the repo root:
   ```xml
   <Project>
     <PropertyGroup>
       <TargetFramework>net9.0</TargetFramework>
       <Nullable>enable</Nullable>
       <ImplicitUsings>enable</ImplicitUsings>
       <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
       <LangVersion>latest</LangVersion>
     </PropertyGroup>
   </Project>
   ```
5. Add `Directory.Packages.props` for central package version management. One version of
   every package across the solution, changed in one place.
6. Add `.editorconfig` so `dotnet format` has rules to enforce.
7. Install MudBlazor into `TeamPortal.Web`, register `AddMudServices()`, add the
   `MudThemeProvider`, `MudDialogProvider` and `MudSnackbarProvider` to `MainLayout`.
8. Write the `azure-pipelines.yml` covering all seven gates in `Frontend/CI.md`.

## Tests

One deliberately trivial test per project, purely to prove the runner and coverage
collector are wired up:

```csharp
[Fact]
public void TestInfrastructure_IsWiredUp() => true.Should().BeTrue();
```

## Done when

The pipeline runs end to end on a pull request and the app shows a blank MudBlazor page.

---

# Phase A1 - The Seam

**Goal:** every DTO and every service interface, agreed and frozen. This phase writes no
behaviour at all.

This is the most important phase in the plan. Everything after it - both parts - is built
against what gets decided here.

## Build

1. **DTOs**, one per thing a screen displays. Records, not classes. No navigation
   properties, no audit columns, nothing a screen does not render:
   ```csharp
   public record EmployeeCardDto(
       int Id, string FullName, string? JobTitle,
       string DepartmentName, string DepartmentColour,
       string? PhotoUrl, StaffType StaffType);

   public record DepartmentListItemDto(
       int Id, string Name, string ColourHex, int EmployeeCount);

   public record ProcessStepDto(
       int Id, int StepNumber, string Name, string? Description,
       string? DepartmentName, string? DepartmentColour,
       string StatusName, string StatusColour,
       IReadOnlyList<StepOwnerDto> Owners,
       IReadOnlyList<StepImageDto> Images);
   ```
2. **Request records** for every write - `CreateEmployeeRequest`, `UpdateDepartmentRequest`,
   `CreateStepRequest` and so on.
3. **`Result` and `Result<T>`** - the single return shape for every write. Validation
   failures are a normal outcome, not an exception:
   ```csharp
   public record ValidationError(string Field, string Message);

   public class Result<T>
   {
       public bool IsSuccess { get; }
       public T? Value { get; }
       public IReadOnlyList<ValidationError> Errors { get; }

       public static Result<T> Success(T value) => new(true, value, []);
       public static Result<T> Invalid(params ValidationError[] errors) => new(false, default, errors);
   }
   ```
4. **Service interfaces.** Async, cancellable, DTO in and DTO out:
   ```csharp
   public interface IEmployeeService
   {
       Task<IReadOnlyList<EmployeeCardDto>> GetByDepartmentAsync(int departmentId, CancellationToken ct = default);
       Task<IReadOnlyList<EmployeeCardDto>> SearchAsync(string term, CancellationToken ct = default);
       Task<EmployeeDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
       Task<Result<int>> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default);
       Task<Result> UpdateAsync(UpdateEmployeeRequest request, CancellationToken ct = default);
       Task<Result> DeleteAsync(int id, CancellationToken ct = default);
   }
   ```
   The full set: `IAuthService`, `IDepartmentService`,
   `IEmployeeService`, `IProcessFolderService`, `IProcessService`, `IProcessStepService`,
   `IFileStore`, `IActivityLogService`, `INotificationService`, `ICurrentUserService`.
   (`IBusinessUnitService` was drafted here too, then dropped at phase A12 once no
   screen turned out to need it - see the A12 note below.)
5. **FluentValidation validators** for every request. These live in `Application` and are
   shared by both implementations - validation is written once and never duplicated.

## Tests - `TeamPortal.Mock.Tests`

Validators only. There is nothing else yet to test.

| Test | Proves |
|------|--------|
| `CreateEmployeeValidator_WithBlankName_Fails` | |
| `CreateEmployeeValidator_WithNameOver150Chars_Fails` | Matches the column length |
| `UpdateDepartmentValidator_WithBadColourHex_Fails` | The `^#[0-9A-F]{6}$` rule |
| `CreateStepValidator_WithDuplicateOwnerIds_Fails` | |
| `CreateProcessValidator_WithNameUnder3Chars_Fails` | |

## Done when

Every interface is reviewed and signed off by whoever will build Part B. Changing one
after phase A3 costs UI rework, so the review is not a formality.

---

# Phase A2 - Mock Services

**Goal:** working implementations of every interface, seeded with the exact data in the
design screenshots.

## Build

1. `MockDataStore` - a singleton holding a `List<T>` per entity, guarded by a lock, seeded
   from `docs/design/*.png`: the Onboarding Process with its seven steps, the eight team
   members, the six departments, the two process folders.
2. One mock service per interface. Each one:
   - enforces the uniqueness rules from `Frontend/DataModel.md`,
   - runs the same FluentValidation validators the real service will,
   - returns copies, never references into the store,
   - awaits a configurable delay before answering,
   - assigns ids from a counter the way `IDENTITY(1,1)` would.
3. `MockOptions` - `ReadDelay`, `WriteDelay`, `FailNextWrite`. Wire `FailNextWrite` to a
   hidden dev-only toggle so error paths can be demonstrated on request.
4. `AddMockData()` extension method registering the lot.
5. `MockAuthenticationStateProvider` returning a seeded signed-in user, plus a dev-only
   role switcher so the UI can be checked as Admin, Manager, Member and Viewer.

```csharp
public class MockEmployeeService : IEmployeeService
{
    private readonly MockDataStore _store;
    private readonly MockOptions _options;
    private readonly IValidator<CreateEmployeeRequest> _validator;

    public async Task<Result<int>> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default)
    {
        await Task.Delay(_options.WriteDelay, ct);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<int>.Invalid(validation.ToErrors());

        lock (_store.Gate)
        {
            if (!_store.Departments.Any(d => d.Id == request.DepartmentId))
                return Result<int>.Invalid(new ValidationError(
                    nameof(request.DepartmentId), "That department does not exist."));

            var id = _store.NextEmployeeId();
            _store.Employees.Add(request.ToRecord(id));   // a copy, not the request
            _store.WriteActivity(ActivityAction.Created, "Employee", id, $"Added {request.FullName}");
            return Result<int>.Success(id);
        }
    }
}
```

## Tests - `TeamPortal.Mock.Tests`

The contract test base classes from the test strategy start here. Every one of these gets
inherited and re-run against EF Core in phase B2.

| Test | Proves |
|------|--------|
| `GetByDepartmentAsync_ReturnsOnlyThatDepartment` | |
| `GetByDepartmentAsync_ExcludesInactiveEmployees` | |
| `GetByDepartmentAsync_OrdersByFullName` | |
| `GetSteps_ReturnsInStepNumberOrder` | The strip is never out of order |
| `CreateDepartmentAsync_WithADuplicateName_Fails` | The mock enforces uniqueness |
| `UpdateDepartmentAsync_WithTheSameNameAsItself_Succeeds` | The classic uniqueness bug |
| `CreateAsync_AssignsSequentialIds` | Ids behave like `IDENTITY(1,1)` |
| `CreateAsync_WhenValidationFails_WritesNothingToTheStore` | |
| `GetById_MutatingTheResult_DoesNotChangeTheStore` | Copy-on-read really copies |
| `CreateStepAsync_AssignsTheNextStepNumber` | |
| `DeleteStepAsync_RenumbersTheRemainingSteps` | No gaps in the strip |
| `AllServiceMethods_AcceptACancellationToken` | Reflection sweep over the interfaces |

That copy-on-read test looks pedantic and is not:

```csharp
[Fact]
public async Task GetById_MutatingTheResult_DoesNotChangeTheStore()
{
    var sut = await CreateSutAsync();
    var first = await sut.GetByIdAsync(1);

    first! with { FullName = "Tampered" };          // or mutate, if the DTO is a class
    var second = await sut.GetByIdAsync(1);

    second!.FullName.Should().NotBe("Tampered");
}
```

## Done when

The whole app's data layer works, in memory, with no UI on top of it yet.

---

# Phase A3 - App Shell

**Goal:** the frame every screen sits in.

## Build

1. `MudTheme` with the full brand palette from `Frontend/TeamConventions.md` and the
   `Brand Sans` font stack from `Frontend/FontUsage.md`.
2. `MainLayout` - the dark purple `MudAppBar`, brand mark, notification bell, avatar menu.
3. `NavMenu` sidebar with the two folder sections.
4. `AppPageHeader.razor` - the mandatory overline + title + actions banner.
5. `app.css` with the shared rules: `.app-page-header*`, `.app-grid-header`, `.app-dialog`,
   the rounded-pill input outline.
6. `ConfirmDialog.razor` - one reusable delete confirmation for the whole app.
7. `LoadingPanel.razor` and `EmptyState.razor` - used on every screen from here on. Build
   them now and no screen ships without them.
8. `ErrorBoundary` around the body so one broken component does not kill the circuit.

## Tests - `TeamPortal.Web.Tests`

| Test | Proves |
|------|--------|
| `AppPageHeader_WithOverlineAndTitle_RendersBoth` | |
| `AppPageHeader_WithoutTitle_ThrowsArgumentException` | Required parameters are enforced |
| `AppPageHeader_WithChildContent_RendersTheActionsSlot` | |
| `ConfirmDialog_OnConfirm_ClosesWithTrue` | |
| `ConfirmDialog_OnCancel_ClosesWithFalse` | |
| `MainLayout_WhenSignedOut_DoesNotShowTheAvatarMenu` | |

```csharp
public class AppPageHeaderTests : TestContext
{
    public AppPageHeaderTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;   // MudBlazor calls JS on render
    }
}
```

`JSRuntimeMode.Loose` is required for any MudBlazor component test - MudBlazor makes JS
interop calls during render and the strict default fails them all.

---

# Phase A4 - Login Screen

**Goal:** screen `01`, complete, against a fake `IAuthService`. No Identity yet.

## Build

1. `MockAuthService` - accepts the seeded users, rejects everything else, counts failures
   and reports a lockout after five. Same rules as the real one, no cryptography.
2. `LoginPage.razor` + `.razor.cs` built to `01-login-screen.png`: split purple panel,
   `MudTextField` for email and password, show/hide adornment on the password,
   `MudCheckBox` for Remember me, full-width `MudButton`.
3. Forgot-password request and reset pages, wired to a no-op that logs to the console.
4. `MockAuthenticationStateProvider` signs the user in and out for real as far as
   `AuthorizeView` is concerned - the cascading auth state must genuinely change.

## Tests

`TeamPortal.Mock.Tests` - the contract tests reused verbatim in phase B3:

| Test | Proves |
|------|--------|
| `SignInAsync_WithCorrectCredentials_ReturnsSuccess` | |
| `SignInAsync_WithWrongPassword_ReturnsFailureAndIncrementsCount` | |
| `SignInAsync_AfterFiveFailures_ReturnsLockedOut` | |
| `SignInAsync_WithInactiveUser_ReturnsFailure` | |
| `SignInAsync_WithUnknownEmail_ReturnsTheSameMessageAsWrongPassword` | No account enumeration |

`TeamPortal.Web.Tests`:

| Test | Proves |
|------|--------|
| `LoginPage_OnFirstRender_SignInButtonIsDisabled` | Empty form cannot be submitted |
| `LoginPage_WithInvalidEmail_ShowsFieldError` | |
| `LoginPage_OnSubmit_CallsAuthServiceOnce` | No double submit |
| `LoginPage_WhileSubmitting_DisablesTheButton` | |
| `LoginPage_WhenAuthFails_ShowsTheErrorAndKeepsTheEmail` | The user does not retype it |

---

# Phase A5 - Team Screen, Read-Only

**Goal:** screen `05` renders the seeded team.

## Build

1. `TeamPage.razor` - department filter column, `MudGrid` of employee cards, search box.
2. `EmployeeCard.razor` - avatar or initials, name, role, department, Employee/Management chip.
3. Search filters on name, role and department, case-insensitive, debounced 300 ms.
4. `LoadingPanel` while the mock delay runs, `EmptyState` when a department has nobody.

## Tests - `TeamPortal.Web.Tests`

| Test | Proves |
|------|--------|
| `TeamPage_WithEmployees_RendersOneCardEach` | |
| `TeamPage_WhileLoading_ShowsTheLoadingPanel` | The delay bought us this test |
| `TeamPage_WithNoEmployees_ShowsTheEmptyState` | |
| `EmployeeCard_WithNoPhoto_ShowsTheInitialsAvatar` | |
| `EmployeeCard_ForManagement_ShowsTheManagementChip` | |
| `TeamPage_OnDepartmentClick_ReloadsWithThatFilter` | |
| `TeamPage_DepartmentCounts_MatchTheCardCount` | The `(7)` badge is right |

---

# Phase A6 - Team Modals

**Goal:** screens `06` and `07`. First writes.

## Build

1. `AddEmployeeDialog.razor` - photo upload, name, role, department select,
   Employee/Management radio group.
2. `EditDepartmentDialog.razor` - name field with a live preview chip, eight colour swatches.
3. `AddBusinessUnitDialog.razor` was also built here, then removed at phase A12 -
   nothing ever opened it once the Team sidebar was redesigned out. See the A12 note.
4. Every dialog: `Class="app-dialog"`, Save left, Cancel right, Save disabled while saving,
   green snackbar on success, parent refreshes its data on close.
5. Field-level error display driven by the `ValidationError` list coming back in `Result`.

## Tests - `TeamPortal.Web.Tests`

| Test | Proves |
|------|--------|
| `AddEmployeeDialog_WithEmptyName_KeepsSaveDisabled` | |
| `AddEmployeeDialog_OnSave_CallsTheServiceOnce` | |
| `AddEmployeeDialog_OnCancel_CallsNothing` | Cancel never writes |
| `AddEmployeeDialog_WhileSaving_DisablesSave` | No double-click duplicates |
| `AddEmployeeDialog_WhenTheServiceReturnsErrors_ShowsThemUnderTheFields` | |
| `AddEmployeeDialog_WhenTheSaveFails_StaysOpen` | Set `FailNextWrite` and check |
| `EditDepartmentDialog_OnColourPick_UpdatesThePreviewChip` | |
| `EditDepartmentDialog_OnOpen_PrefillsTheCurrentValues` | |
| `TeamPage_AfterASuccessfulSave_ReloadsTheCards` | |

---

# Phase A7 - Process Dashboard, Read-Only

**Goal:** screen `02` renders the seeded Onboarding Process.

## Build

1. `ProcessPage.razor` - folder sidebar, horizontal flow strip, step list, detail panel.
2. `ProcessStepCard.razor` - number badge, name, department tag in the department colour,
   status dot in the status colour.
3. Selecting a step loads its description, owners and screenshot placeholder.
4. Back / Next move between steps and disable correctly at each end.

## Tests - `TeamPortal.Web.Tests`

| Test | Proves |
|------|--------|
| `ProcessPage_WithSevenSteps_RendersSevenCards` | |
| `ProcessPage_OnFirstRender_SelectsStepOne` | |
| `ProcessPage_OnStepOne_DisablesBack` | |
| `ProcessPage_OnLastStep_DisablesNext` | |
| `ProcessStepCard_UsesTheDepartmentColourForItsTag` | |
| `ProcessStepCard_UsesTheStatusColourForItsDot` | |
| `ProcessPage_WithNoProcessSelected_ShowsTheEmptyState` | |
| `ProcessPage_FolderCounts_MatchTheProcessCount` | |

---

# Phase A8 - Add / Edit Process Modal

**Goal:** screen `03`.

## Build

1. `ProcessDialog.razor` - name, description, business unit tab strip, Delete on the far
   left, Cancel and Save on the right.
2. Delete routes through `ConfirmDialog`.
3. New processes land at the end of their folder's `SortOrder`.

## Tests

`TeamPortal.Mock.Tests` (contract):

| Test | Proves |
|------|--------|
| `CreateProcessAsync_WithDuplicateNameInSameFolder_Fails` | |
| `CreateProcessAsync_WithSameNameInAnotherFolder_Succeeds` | Uniqueness is per folder |
| `CreateProcessAsync_SetsSortOrderToTheEndOfTheFolder` | |
| `DeleteProcessAsync_AlsoRemovesItsSteps` | |

`TeamPortal.Web.Tests`:

| Test | Proves |
|------|--------|
| `ProcessDialog_OnDelete_OpensTheConfirmDialogFirst` | |
| `ProcessDialog_WhenConfirmIsCancelled_DeletesNothing` | |
| `ProcessDialog_OnSave_ClosesAndRefreshesTheSidebar` | |

---

# Phase A9 - Add / Edit Step Modal

**Goal:** screen `04`. The most intricate screen in the app.

## Build

1. `AddStepDialog.razor` - name, description, department select, owner chips with
   `MudAutocomplete` behind "Add Team Member", the upload drop zone.
2. `MockProcessStepService.CreateStepAsync` - step, owners, image and activity row all
   added together, or none of them.
3. `ResequenceAsync(processId)` - renumber `1..n` with no gaps after any insert or delete.

## Tests

`TeamPortal.Mock.Tests` (contract - every one re-runs against EF Core in B2):

| Test | Proves |
|------|--------|
| `CreateStepAsync_AssignsTheNextStepNumber` | |
| `CreateStepAsync_OnAnEmptyProcess_AssignsStepNumberOne` | Off-by-one at the boundary |
| `CreateStepAsync_WithThreeOwners_RecordsThreeOwners` | |
| `CreateStepAsync_WithTheSameOwnerTwice_RecordsOne` | |
| `CreateStepAsync_WithAnUnknownOwner_ReturnsValidationFailure` | |
| `CreateStepAsync_WhenTheImageSaveFails_LeavesNoPartialStep` | All-or-nothing |
| `DeleteStepAsync_RenumbersTheRemainingSteps` | No gaps in the strip |
| `DeleteStepAsync_OnTheMiddleStep_LeavesNoDuplicateNumbers` | |
| `InsertStepAt_ShiftsTheFollowingStepsDown` | |
| `ResequenceAsync_OnAlreadyCorrectSteps_ChangesNothing` | Cheap idempotency check |

`TeamPortal.Web.Tests`:

| Test | Proves |
|------|--------|
| `AddStepDialog_WithNoOwners_StillAllowsSave` | Owners are optional |
| `AddStepDialog_AddingTheSameOwnerTwice_ShowsOneChip` | |
| `AddStepDialog_RemovingAnOwnerChip_RemovesIt` | |
| `AddStepDialog_OnSave_RefreshesTheFlowStrip` | |

---

# Phase A10 - Screenshot Upload

**Goal:** the upload experience, against a mock file store.

## Build

1. `MockFileStore` - keeps bytes in a dictionary, hands back a fake path, and applies the
   **real** rules: PNG / JPG / WEBP only, 5 MB cap, signature checked not extension.
2. `MudFileUpload` drop zone in the Add Step dialog.
3. Thumbnail in the step card, full size in a lightbox.

Applying the real validation rules in the mock is the point of this phase. The rules move
to `Application` where both stores use them, so phase B4 is a storage swap and nothing more.

## Tests - `TeamPortal.Mock.Tests`

| Test | Proves |
|------|--------|
| `Upload_WithA6MbFile_IsRejected` | Size cap |
| `Upload_WithAPdfRenamedToPng_IsRejected` | Signature check, not extension check |
| `Upload_WithAValidPng_ReturnsAStoredPath` | |
| `Upload_GeneratesANewNameAndKeepsTheOriginalForDisplay` | |
| `Upload_WithAFilenameContainingDotDotSlash_IsSanitised` | Path traversal |

`TeamPortal.Web.Tests`:

| Test | Proves |
|------|--------|
| `AddStepDialog_WithAnOversizeFile_ShowsTheRejectionMessage` | |
| `AddStepDialog_AfterARejectedFile_StillAllowsAnotherAttempt` | |

---

# Phase A11 - History & Notifications

**Goal:** "View Process History", "View Activity Log" and the bell icon.

## Build

1. History and log panels as `MudTimeline` or `MudList`, newest first, paged.
2. Notification bell with an unread badge, mark-as-read on click.
3. Confirm every write path from A6 to A10 records an activity row - this phase is where
   the gaps show up.

## Tests

| Test | Proves |
|------|--------|
| `GetForEntityAsync_ReturnsNewestFirst` | |
| `GetForEntityAsync_IsPaged` | The log will not be small forever |
| `GetForEntityAsync_ForAnEntityWithNoHistory_ReturnsEmpty` | |
| `MarkAsReadAsync_OnlyAffectsThatUsersNotifications` | Cross-user leakage |
| `GetUnreadCountAsync_ExcludesReadNotifications` | |
| `EveryWriteMethod_RecordsAnActivityRow` | Reflection-driven sweep |

---

# Phase A12 - Demo & Freeze

**Goal:** sign-off on the UI, and a locked set of interfaces.

## Do

1. Demo all seven screens to the business. Every modal, every error path, the role
   switcher.
2. Log the feedback. **Fix the UI feedback now**, while there is no schema to keep in step.
3. Reconcile the interfaces against `Frontend/DataModel.md`:
   - Any DTO field with no entity behind it - add the column, or drop the field.
   - Any entity column no screen ever reads or writes - challenge whether it is needed.
   - Update `Frontend/DataModel.md` and `docs/developer/ErDiagram.html` with the answers.
4. Tag the repo `part-a-complete`.
5. Freeze the interfaces. From here, changing one is a ticket with a written reason.

## What this reconciliation actually found

- **`BusinessUnit` was dropped entirely.** It was drafted at A1 alongside
  `Department`, given its own service (`IBusinessUnitService`), its own dialog
  (`AddBusinessUnitDialog.razor`), and a tab strip on the Add Process modal. By the
  time the Team screen's sidebar was reworked (screenshot feedback during A9-A11),
  nothing on any screen read or wrote it any more: the Add Process modal's
  "Select Business Unit" tabs turned out to mean `Department`, not a separate
  concept above it. A table and a service with zero screens using them is not a
  feature kept in reserve, it is dead code with a migration attached, so it was
  removed - the DTO, the interface, the mock implementation, the dialog, its
  tests, and every `BusinessUnitId` foreign key on `Department` and `Process`.
  See `Frontend/DataModel.md`, the `BusinessUnit` section, for the full account.
- No other DTO field or entity column was found without a screen behind it.

## Done when

The business has seen the app and agrees it is the app they asked for, and the data model
document matches the interfaces exactly.

---

---

# PART B - EF CORE & IDENTITY

Ground rule for the whole of Part B: **no file under `TeamPortal.Web/` changes.** If one
has to, an interface moved, and that needs saying out loud in the PR.

---

# Phase B1 - Domain & Database

**Goal:** every table from `Frontend/DataModel.md` exists, seeded and migrated. Still no
UI change - the app runs on mocks throughout this phase.

## Build

1. Create `TeamPortal.Domain` and `TeamPortal.Infrastructure`.
2. `AuditableEntity`, `StaffType`, `ActivityAction`.
3. All ten domain entities. (Eleven until phase A12 dropped `BusinessUnit` - see the
   reconciliation note in that phase.)
4. `PortalDbContext` with every `DbSet`, `ApplyConfigurationsFromAssembly`, and the
   soft-delete query filters.
5. One `IEntityTypeConfiguration<T>` per entity - max lengths, indexes, delete behaviours.
6. `ICurrentUserService` and the `SaveChangesAsync` override that stamps the audit columns
   and converts hard deletes to soft deletes.
7. `DbSeeder` - statuses, roles, departments, employees, folders, processes and the steps
   of the first process. Idempotent. Seed the **same** data the mock store used, so the app
   looks identical after the swap.
8. `AddDbContextFactory`, never `AddDbContext`.
9. `dotnet ef migrations add InitialCreate`. **Read the generated SQL before committing.**

## Tests - `TeamPortal.Infrastructure.Tests`

| Test | Proves |
|------|--------|
| `Model_EveryEntity_HasAPrimaryKey` | No entity was forgotten in a configuration |
| `Model_EveryStringProperty_HasAMaxLength` | Nothing silently became `nvarchar(max)` |
| `ProcessStep_TwoStepsWithTheSameNumberInOneProcess_IsRejected` | The composite unique index really exists |
| `ProcessStep_TheSameNumberInADifferentProcess_IsAllowed` | Uniqueness is per process, not global |
| `Employee_ADuplicateEmail_IsRejected` | The filtered unique index works |
| `Employee_TwoEmployeesWithNoEmail_IsAllowed` | The filter on NULL works - email is optional |
| `Department_ADuplicateName_IsRejected` | Department names are unique |
| `Process_TwoProcessesAtTheSamePositionInAFolder_IsRejected` | Flow-strip order is deterministic |
| `StepOwner_TheSamePersonTwiceOnOneStep_IsRejected` | The composite primary key holds |
| `Process_WhenHardDeleted_TakesItsStepsWithIt` | `Cascade` is configured where intended |
| `Department_WithEmployeesInIt_CannotBeHardDeleted` | `Restrict` is configured where intended |
| `SaveChanges_OnAdd_StampsCreatedAtAndCreatedBy` | The audit override runs |
| `SaveChanges_OnRemove_SetsIsDeletedInsteadOfDeleting` | Soft delete works |
| `QueryFilter_SoftDeletedRows_AreHiddenFromNormalQueries` | The global filter is applied |
| `SoftDelete_FreesTheNameForReuse` | The unique indexes are filtered on `IsDeleted` |
| `ActivityLog_IsNotSoftDeleted_BecauseItIsAppendOnly` | The filter is not applied where it should not be |
| `Seeder_RunTwice_DoesNotDuplicateRows` | Seeding is idempotent |
| `Seeder_ProducesTheSameDepartmentsAsTheMockStore` | The swap is invisible to the user |
| `Seeder_ProducesTheSameEmployeesAsTheMockStore` | The swap is invisible to the user |
| `Seeder_PutsEachPersonInTheSameDepartmentAsTheMockStore` | The org chart survives the swap too |

The last three run the Part A mock behind its own `AddMockData()` registrations and hold its
output against the seeded database, so the comparison is against what the app actually ran
rather than against a second copy of the seed list.

The first two are reflection sweeps over `context.Model` and catch mistakes in entities
that do not exist yet - they keep paying off for the life of the project:

```csharp
[Fact]
public void Model_EveryStringProperty_HasAMaxLength()
{
    using var db = new DatabaseFixture().Factory.CreateDbContext();

    var unbounded = db.Model.GetEntityTypes()
        .SelectMany(t => t.GetProperties())
        .Where(p => p.ClrType == typeof(string) && p.GetMaxLength() is null)
        .Select(p => $"{p.DeclaringEntityType.ShortName()}.{p.Name}")
        .ToList();

    unbounded.Should().BeEmpty("every string column needs an explicit length");
}
```

---

# Phase B2 - EF Core Services

**Goal:** a real implementation of every data interface, swapped in one at a time.

## Build

Work service by service. For each one:

1. Write `SqlEmployeeService : IEmployeeService` in `Application`, taking
   `IDbContextFactory<PortalDbContext>`.
2. Reuse the **same** FluentValidation validator the mock used. It moves nowhere - it was
   always in `Application`.
3. Inherit the phase A2 contract tests into `SqlEmployeeServiceTests`. Run them. Fix the
   service until they are green. **Do not edit the tests.**
4. Register the real service in `AddSqlData()` and delete its line from `AddMockData()`.
5. Run the full bUnit suite. Still green means the screen never noticed.

Suggested order, easiest first, so the pattern is established before the hard one:
`IDepartmentService`, `IEmployeeService`, `IProcessFolderService`,
`IProcessService`, `IActivityLogService`, `INotificationService`, `IProcessStepService`.
(`IBusinessUnitService` is not in this list - it was removed at phase A12.)

`IProcessStepService` is last because it is the only one that writes four tables in one
transaction:

```csharp
public async Task<Result<int>> CreateStepAsync(CreateStepRequest request, CancellationToken ct = default)
{
    var validation = await _validator.ValidateAsync(request, ct);
    if (!validation.IsValid) return Result<int>.Invalid(validation.ToErrors());

    await using var db = await _factory.CreateDbContextAsync(ct);
    await using var tx = await db.Database.BeginTransactionAsync(ct);

    var next = await db.ProcessSteps
        .Where(s => s.ProcessId == request.ProcessId)
        .MaxAsync(s => (int?)s.StepNumber, ct) ?? 0;

    var step = new ProcessStep { /* ... */ StepNumber = next + 1 };
    db.ProcessSteps.Add(step);
    await db.SaveChangesAsync(ct);

    foreach (var employeeId in request.OwnerIds.Distinct())
        db.StepOwners.Add(new StepOwner { ProcessStepId = step.Id, EmployeeId = employeeId });

    if (request.Screenshot is not null)
    {
        var path = await _fileStore.SaveAsync(request.Screenshot, ct);
        db.StepImages.Add(new StepImage { ProcessStepId = step.Id, StoredPath = path /* ... */ });
    }

    db.ActivityLogs.Add(/* ... */);
    await db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);

    return Result<int>.Success(step.Id);
}
```

## Tests - `TeamPortal.Services.Tests`

The project that was `TeamPortal.Mock.Tests` in Part A. It was renamed at B2 because it now
holds both sides: every contract test in it runs twice, once against the mock and once
against EF Core over SQLite. Keeping them in one project means no test project has to
reference another one just to inherit a base class, and the name stops lying about what is
inside it.

`ServiceFixture` became abstract to make that work - `MockServiceFixture` calls
`AddMockData()`, `SqlServiceFixture` calls `AddSqlServices()` over a SQLite connection and
runs `DbSeeder`. The contract tests only ever see the base class, so not one of them changed.

Two tests moved out of the shared contract and into `MockEmployeeServiceTests`: the
`FailNextWrite` pair. That switch is a mock-only affordance for building screens against a
failing save, and EF Core has no equivalent - the transaction tests below cover the same
ground on that side.

Plus the ones that only make sense against a real database:

| Test | Proves |
|------|--------|
| `CreateStepAsync_WhenTheImageSaveFails_RollsBackTheWholeStep` | The transaction is real |
| `CreateStepAsync_UnderTwoConcurrentCalls_DoesNotDuplicateStepNumbers` | The unique index catches the race |
| `GetProcessDetailAsync_IssuesOneQueryNotOnePerStep` | No N+1 |
| `GetByDepartmentAsync_DoesNotTrackEntities` | `AsNoTracking()` is on every read |
| `Services_NeverInjectDbContextDirectly` | Reflection sweep for the Blazor Server trap |
| `MoveAsync_ToTheFront_RenumbersWithoutTrippingTheUniqueIndex` | The two-pass renumber is real |
| `ResequenceAsync_OnAlreadyCorrectSteps_ChangesNothing` | Resequencing is idempotent |
| `EveryDataServiceInterface_HasAnEfCoreImplementation` | Catches a half-finished swap at build time |

## What this phase actually found

Four things that only appear once there is a database behind the interfaces:

1. **`PortalDbContext` had two constructors.** An options-only one for `dotnet ef` and an
   options-plus-user one for runtime. Both accept a `DbContextOptions`, so
   `AddDbContextFactory` cannot choose and throws *"Multiple constructors accepting all
   given argument types have been found"*. Collapsed to one, with a nullable user - a null
   user is what a system write is, and it leaves the audit columns null.

2. **The context factory has to be scoped.** `AddDbContextFactory` registers a singleton by
   default, and the context needs `ICurrentUserService` to stamp the audit columns. A
   singleton holding a scoped user is a captive dependency, which is exactly what
   `BuildServiceProvider(validateScopes: true)` exists to catch.

3. **`InvariantGlobalization` had to be turned off.** `Microsoft.Data.SqlClient` asks for
   the `en-us` culture while opening a connection and throws `CultureNotFoundException`
   without it, and `dotnet ef` fails the same way. Harmless while the app ran on in-memory
   lists; a start-up crash the moment it pointed at SQL Server.

4. **`StepOwner` and `StepImage` needed query filters of their own.** Neither has an
   `IsDeleted` - they are hard-deleted with their step - but both sit on the required end of
   a relationship with a filtered entity, which EF warns about (10622) and which leaves a
   soft-deleted step's owners visible from the other direction.

Turning globalization back on also woke CA1304 and CA1311, which join CA1862 in the scoped
suppression in `.editorconfig`. All three say to pass a `StringComparison` or `CultureInfo`,
and all three are wrong inside an expression tree: EF Core translates the no-argument
`ToUpper()` to the database's `UPPER()` and has no translation for the culture-aware
overloads, so taking the advice trades a warning for a runtime *"could not be translated"*.
The database's collation decides casing here, not the process culture.

The rollback test is the one that earns its keep:

```csharp
[Fact]
public async Task CreateStepAsync_WhenTheImageSaveFails_RollsBackTheWholeStep()
{
    // Arrange
    await using var fixture = new DatabaseFixture();
    await fixture.SeedProcessWithStepsAsync(count: 3);

    var fileStore = Substitute.For<IFileStore>();
    fileStore.SaveAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
             .Returns<Task<string>>(_ => throw new IOException("disk full"));

    var sut = new SqlProcessStepService(fixture.Factory, new CreateStepValidator(), fileStore);

    // Act
    var act = () => sut.CreateStepAsync(RequestWithImage());

    // Assert
    await act.Should().ThrowAsync<IOException>();

    await using var db = await fixture.Factory.CreateDbContextAsync();
    db.ProcessSteps.Should().HaveCount(3, "the failed step must not be left behind");
    db.StepOwners.Should().BeEmpty();
}
```

## Done when

`AddMockData()` registers nothing but `IAuthService` and `IFileStore`, data survives a
restart, and every Part A bUnit test is still green and unedited.

---

# Phase B3 - Identity

**Goal:** screen `01` runs on real ASP.NET Core Identity. The screen itself is untouched.

## Build

1. `AppUser`, `AppRole` and the five plumbing classes; switch `PortalDbContext` to
   `IdentityDbContext<...>`; rename the tables to `AppUser` etc.
2. `AddIdentity` with the password, lockout and unique-email options from
   `Frontend/DataModel.md`.
3. Cookie authentication, `AddCascadingAuthenticationState`,
   `RevalidatingServerAuthenticationStateProvider` replacing the mock provider.
4. `IdentityAuthService : IAuthService` wrapping `SignInManager`. Same interface the login
   page has been calling since phase A4.
5. Real forgot-password and reset, using `GeneratePasswordResetTokenAsync` and an email
   sender.
6. Seed the first admin from user secrets. Never from a literal in the code.
7. Delete `MockAuthService` from the DI registration. Keep the class for tests.

## Tests

Every `IAuthService` contract test from phase A4, inherited and re-run against Identity.
Plus:

| Test | Proves |
|------|--------|
| `SignInAsync_OnSuccess_StampsLastLoginAt` | |
| `SignInAsync_OnSuccess_WritesLoggedInToActivityLog` | |
| `SignInAsync_AfterLockout_StillFailsWithTheCorrectPassword` | Lockout is real, not cosmetic |
| `PasswordHash_IsNeverReturnedByAnyDto` | Reflection sweep over the DTOs |
| `ResetToken_UsedTwice_IsRejectedTheSecondTime` | Single use |

The bUnit login tests from A4 run unchanged. If any of them fail here, `IAuthService`
changed shape and that is the bug.

---

# Phase B4 - Real File Storage

**Goal:** uploads land on disk or in blob storage instead of a dictionary.

## Build

1. `LocalDiskFileStore` for DEV, `AzureBlobFileStore` for SIT and above, both
   `: IFileStore`.
2. The validation rules do not move - they were written into `Application` in phase A10
   and both stores already call them.
3. Generate the stored name yourself; never reuse the user's filename on disk. Keep the
   original in `StepImage.FileName` for display only.
4. Serve images through a controller that checks authorisation, not from `wwwroot`.

## Tests

Every `IFileStore` contract test from A10, re-run against both real stores. Plus:

| Test | Proves |
|------|--------|
| `LocalDiskFileStore_WritesInsideTheConfiguredRootOnly` | |
| `LocalDiskFileStore_OnDelete_RemovesTheFileFromDisk` | No orphans |
| `ImageController_ForAnUnauthenticatedUser_Returns401` | |
| `ImageController_ForAnImageTheUserCannotSee_Returns403` | |

---

# Phase B5 - Hardening & Release

**Goal:** safe to put in front of real users.

## Build

1. `[Authorize]` on every page. Policies matching the roles in `Frontend/DataModel.md`.
2. Hide **and** block: a Member must not see the Add buttons, and must also get a 403 if
   they call the service directly. UI-only security is not security. The role switcher
   from phase A2 makes this quick to check by hand.
3. Antiforgery, HTTPS redirect, HSTS, secure cookies, a locked-down CSP.
4. Accessibility sweep - `aria-label` on every icon button, keyboard-only run through
   every modal, focus trapped inside open dialogs and returned on close.
5. Performance - check the generated SQL for N+1 queries, confirm `AsNoTracking()` on all
   read paths, add paging anywhere a list can grow without limit.
6. Blazor Server specifics - a `CircuitHandler` that logs disconnects, a friendly
   reconnection UI, and cancellation tokens threaded through every service call so
   abandoned circuits stop doing work.
7. Serilog to a file in DEV and to Application Insights above it.
8. Remove the dev-only role switcher and the `FailNextWrite` toggle from Release builds.

## Tests

| Test | Proves |
|------|--------|
| `EveryPage_HasAnAuthorizeAttribute` | Reflection sweep over the assembly |
| `CreateEmployeeAsync_AsAViewer_ThrowsForbidden` | Server-side enforcement |
| `AddEmployeeButton_ForAViewer_IsNotRendered` | UI matches the policy |
| `EveryIconButton_HasAnAriaLabel` | bUnit sweep across rendered pages |
| `DevOnlyToggles_AreNotPresentInRelease` | |
| `AllServiceMethods_AcceptACancellationToken` | Reflection sweep |

## Where B5 actually got to

**Done:**

- `[Authorize]` on the two business pages, `[AllowAnonymous]` on login, error and
  not-found, and `AuthorisationSweepTests` to keep it that way. The sweep checks that a
  page has *one or the other* rather than that every page has `[Authorize]` - the point is
  that somebody decided, not that a new page defaults to open.
- `AuthorizeRouteView` in `Routes.razor`. Endpoint authorisation covers the first request
  only; once the circuit is running, navigation happens inside it and never touches the
  endpoint again. With plain `RouteView`, `[Authorize]` stops meaning anything the moment
  the app goes interactive - which is most of the time.
- `AuthorisationGuard`, called by every write path in every EF Core service, plus
  `AuthorisationTests` calling those services directly the way a browser console would.
  This is the "hide **and** block" item: hiding a button stops an honest person, and in
  Blazor Server every button is a message on an open socket.
- Security headers including a CSP with no `unsafe-inline` on `script-src`.
- Cookie hardening: HttpOnly, SameSite=Lax, Secure outside development.

**Not done, and worth naming rather than quietly dropping:**

| Item | Why it matters |
|------|----------------|
| Accessibility sweep (item 4) | `aria-label` on every icon button, keyboard-only run through every modal, focus trapped and returned. Nothing here has been checked against a screen reader. |
| Serilog / Application Insights (item 7) | There is no structured logging anywhere. A production incident currently has nothing to read. |
| `CircuitHandler` for disconnects (item 6) | `ReconnectModal` exists; nothing logs why a circuit dropped. |
| Paging on the employee and process lists (item 5) | The activity log is paged. The others are not, and one of them is the Team screen. |
| Password rules vs. the seeded demo password | `Portal!2026` satisfies the rules today. A stricter policy would break seeding silently. |

### The `[Authorize]` finding

There was none, anywhere, before this phase - despite the plan saying `AuthorizeView` goes
in as each screen is built in Part A. It did not, and nothing noticed, because a Part A
screen on mock data has nothing to protect. That is the honest failure mode of building the
UI first: security work that is "part of every phase" is part of no phase.

### The sweep in this document had a bug

The `EveryPage_HasAnAuthorizeAttribute` sample above calls `GetCustomAttribute<RouteAttribute>()`,
singular. `ProcessPage` carries three `@page` directives and therefore three
`RouteAttribute`s, and the singular overload throws `AmbiguousMatchException` rather than
returning one. The shipped test uses `GetCustomAttributes<T>().Any()`.

The reflection sweeps are worth writing because they cover code that does not exist yet:

```csharp
[Fact]
public void EveryPage_HasAnAuthorizeAttribute()
{
    var unprotected = typeof(Program).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(ComponentBase)))
        .Where(t => t.GetCustomAttribute<RouteAttribute>() is not null)
        .Where(t => t.GetCustomAttribute<AuthorizeAttribute>() is null)
        .Where(t => t.GetCustomAttribute<AllowAnonymousAttribute>() is null)
        .Select(t => t.Name)
        .ToList();

    unprotected.Should().BeEmpty("every routable page must be explicitly protected or explicitly public");
}
```

## Done when

All four environments deploy the same artefact, SIT and UAT are signed off, and the
release readiness checklist in `Frontend/TeamConventions.md` is complete.

---

# Running the Tests

```bash
dotnet test --collect:"XPlat Code Coverage"
```

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=80 /p:ThresholdType=line
```

```bash
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

The pipeline runs the second command. A build under 80% line coverage on
`TeamPortal.Application`, `TeamPortal.Mock` or `TeamPortal.Infrastructure` fails and
cannot be merged.

---

# Risks

| Risk | Impact | What to do about it |
|------|--------|---------------------|
| An interface has to change in Part B | The UI rework the split was meant to avoid | Freeze in A12, and treat any change as a ticket with a written reason |
| Mocks are more forgiving than the database | Part B becomes a week of surprises | Contract tests run against both; mocks enforce uniqueness and ordering from A2 |
| Mock services written synchronously | No spinners, no cancellation, retrofitted under pressure | `Task<T>` and `CancellationToken` on every method from A1 |
| Mocks return live references | Bound fields silently mutate "the database", and it only breaks after the swap | The copy-on-read test in A2 |
| `DbContext` injected into a component | Random concurrency crashes that only appear under load | `AddDbContextFactory` in B1, plus the reflection sweep in B2 |
| EF InMemory used in tests instead of SQLite | Constraint bugs ship to production | Ban the package in `Directory.Packages.props` at A0 |
| Part A never ends | A polished demo with no database behind it | A12 is a hard gate with a date, not a phase that drifts |
| Authorisation left until B5 | A large, risky retrofit | `AuthorizeView` goes in as each screen is built in Part A; B5 only verifies |
| Screenshots stored in `wwwroot` | Anyone with the URL can read them | Serve through an authorised controller from B4 |
| Coverage gate turned off "temporarily" | It never goes back on | Not negotiable - `Frontend/CI.md` gate 4 |

---

# Precedence

If this document conflicts with another Frontend document:

1. Explicit user instructions in the current chat
2. `Frontend/StartHere.md`
3. `Frontend/TeamConventions.md`
4. `Frontend/CI.md`
5. `Frontend/DataModel.md`
6. `Frontend/BuildPlan.md` (this file)
7. `Frontend/FontUsage.md`
```
