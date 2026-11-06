using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using TeamPortal.Domain.Identity;
using TeamPortal.Infrastructure;
using TeamPortal.Infrastructure.Chat;
using TeamPortal.Infrastructure.Data;
using TeamPortal.Infrastructure.Email;
using TeamPortal.Infrastructure.Identity;
using TeamPortal.Infrastructure.Messaging;
using TeamPortal.Web.Components;
using TeamPortal.Web.Endpoints;
using TeamPortal.Web.Security;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// -----------------------------------------------------------------------------
// THE SWAP. These lines are the whole of Part B's integration surface.
//
//   Part A : AddMockData()  - in-memory lists, nothing persisted.
//   Part B : AddSqlData(connectionString) - EF Core, same interfaces.
//
// B2 moved the data services across, B3 took sign-in and B4 took file storage.
// The swap is finished: this project no longer references TeamPortal.Mock at all,
// and AuthorisationSweepTests keeps it that way.
//
// See Frontend/BuildPlan.md. Nothing else in TeamPortal.Web may reference
// TeamPortal.Infrastructure - only these calls.
// -----------------------------------------------------------------------------
string connectionString =
    builder.Configuration.GetConnectionString("Portal")
    ?? throw new InvalidOperationException(
        "No connection string named 'Portal'. See appsettings.json.");

builder.Services.AddSqlData(connectionString);

// Phase B3. Sign-in is real Identity now; screen 01 did not change, because it
// has only ever talked to IAuthService.
builder.Services.AddPortalIdentityCore();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IClaimsPrincipalAccessor, HttpContextClaimsPrincipalAccessor>();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    // "/" is the process dashboard and is itself behind [Authorize]. Pointing
    // LoginPath there sends an anonymous visitor into a redirect back to a page
    // that redirects them again.
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.ReturnUrlParameter = "returnUrl";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    // The cookie is the credential. Nothing else may read it, and it must not
    // travel over plain HTTP outside development.
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// See the class comment: a circuit that stays open all day must not keep
// permissions the user has since lost.
builder.Services.AddScoped<AuthenticationStateProvider,
    RevalidatingIdentityAuthenticationStateProvider>();

// Phase B4. Uploads land on disk, outside wwwroot, and are served through the
// authorised /media endpoint below. Nothing in the app references TeamPortal.Mock
// any more - the Part A implementations are all gone.
// Outgoing email: registration confirmations, password resets, approval notices.
// The password belongs in user secrets, never in appsettings.json.
var smtp = new SmtpOptions();
builder.Configuration.GetSection("Smtp").Bind(smtp);

builder.Services.AddSmtpEmail(
    smtp,
    builder.Configuration["App:BaseUrl"]
    ?? throw new InvalidOperationException(
        "App:BaseUrl is needed to build the links in confirmation emails. See appsettings.json."));

// Who answers the Chat section. Portal (the built-in retriever, no network call
// and no key), Gemini, or OpenAI. The API key belongs in user secrets next to
// the SMTP password - see appsettings.json for the shape and nothing else.
//
// Whatever is configured, the retriever is registered underneath as the
// fallback, so an expired key or a dead network makes the chat do less rather
// than fail every question.
var chat = new ChatOptions();
builder.Configuration.GetSection(ChatOptions.SectionName).Bind(chat);

builder.Services.AddChat(chat);

// The bot on WhatsApp. Off unless WhatsApp:Enabled says otherwise, so a
// deployment nobody configured for it cannot start answering strangers.
var whatsApp = new WhatsAppOptions();
builder.Configuration.GetSection(WhatsAppOptions.SectionName).Bind(whatsApp);

builder.Services.AddWhatsApp(whatsApp);

builder.Services.AddLocalDiskFileStore(
    builder.Configuration["Storage:RootPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "uploads"));

WebApplication app = builder.Build();

// The database has to exist before the first request. In DEV that means creating
// and seeding it; anywhere else migrations are applied by the release pipeline,
// deliberately, rather than by whichever instance happens to start first.
if (app.Environment.IsDevelopment())
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

    IDbContextFactory<PortalDbContext> contextFactory =
        scope.ServiceProvider.GetRequiredService<IDbContextFactory<PortalDbContext>>();

    await using PortalDbContext db = await contextFactory.CreateDbContextAsync();

    try
    {
        await db.Database.MigrateAsync();
    }
    catch (SqlException ex) when (ex.Number is 1801 or 5170)
    {
        // 1801 "database already exists" / 5170 "cannot create file ... already
        // exists". Both mean the same thing: the .mdf is on disk but LocalDB has
        // no catalogue entry for it, so EF tried CREATE DATABASE over a file that
        // is already there. It happens when a debug run is killed part-way, or
        // when the LocalDB instance is reset.
        //
        // Deliberately not repaired automatically. Attaching a stray .mdf found
        // on disk is exactly the kind of guesswork a start-up path should not do,
        // so this says what happened and what to run instead.
        throw new InvalidOperationException(
            "The TeamPortal database files exist on disk but are not attached to " +
            @"(localdb)\MSSQLLocalDB, so the database could not be created." +
            Environment.NewLine + Environment.NewLine +
            "To reattach them and keep the data, run:" +
            Environment.NewLine +
            @"  sqlcmd -S ""(localdb)\MSSQLLocalDB"" -Q ""CREATE DATABASE TeamPortal " +
            @"ON (FILENAME = 'C:\Users\<you>\TeamPortal.mdf'), " +
            @"(FILENAME = 'C:\Users\<you>\TeamPortal_log.ldf') FOR ATTACH;""" +
            Environment.NewLine + Environment.NewLine +
            "To start over instead, delete both files and run again - the seeder " +
            "rebuilds the demo data." +
            Environment.NewLine + Environment.NewLine +
            "Original error: " + ex.Message,
            ex);
    }

    // The demo password comes from configuration, never from a literal here.
    // In DEV that is user secrets; see Frontend/BuildPlan.md, phase B3.
    var seedAdmin = new SeedAdminOptions();
    builder.Configuration.GetSection("SeedAdmin").Bind(seedAdmin);

    await DbSeeder.SeedAsync(
        db,
        scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
        scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>(),
        builder.Configuration["Seed:DemoPassword"] ?? DbSeeder.DefaultDemoPassword,
        seedAdmin);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseSecurityHeaders(app.Environment.IsDevelopment());

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();

// Order matters: who you are, then what you may do, then the anti-forgery check.
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAccountEndpoints();
app.MapMediaEndpoints();
app.MapWhatsAppEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();

/// <summary>
/// Exposed so TeamPortal.Web.Tests can reach the entry assembly for its
/// reflection sweeps, e.g. "every routable page has an [Authorize] attribute".
/// </summary>
public partial class Program;
