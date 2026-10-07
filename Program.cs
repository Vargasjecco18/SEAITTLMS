using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Auth;
using SEAITT_LMS.Components;
using SEAITT_LMS.Data;
using SEAITT_LMS.Services;

var builder = WebApplication.CreateBuilder(args);

// Private settings for this computer, ignored by git: copy appsettings.Local.example.json to appsettings.Local.json and
// put your passwords in it. If the file exists it wins over every other setting. It doesn't exist on a server.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

// Database (MySQL). The password does not belong in appsettings.json, see README.md ("First-time setup").
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not set. See README.md.");

// Ask MySQL which version it is. This is also the first time the app connects, so a wrong password, a stopped MySQL
// or a missing schema shows up here: say so in plain words instead of a crash dump.
ServerVersion serverVersion;
try
{
    serverVersion = ServerVersion.AutoDetect(connectionString);
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Can't connect to MySQL.");
    Console.Error.WriteLine($"  Reason: {ex.GetBaseException().Message}");
    Console.Error.WriteLine("  Check that:");
    Console.Error.WriteLine("   - MySQL is running,");
    Console.Error.WriteLine("   - the schema 'seaittlms' exists (create it in MySQL Workbench),");
    Console.Error.WriteLine("   - the user and password in ConnectionStrings:Default are right. Keep your password in");
    Console.Error.WriteLine("     appsettings.Local.json (copy appsettings.Local.example.json), see README.md, \"First-time setup\".");
    Console.Error.WriteLine();
    Environment.Exit(1);
    return;
}

builder.Services.AddDbContextFactory<AppDbContext>(options => options
    .UseMySql(connectionString, serverVersion)
    .UseSnakeCaseNamingConvention());

// Our own login: PBKDF2 password hashing + the built-in cookie authentication. No Identity package.
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserAccountService>();
builder.Services.Configure<EnrollmentOptions>(builder.Configuration.GetSection(EnrollmentOptions.SectionName));
builder.Services.AddScoped<EnrollmentService>();
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddSingleton<LiveUpdates>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<TeachingService>();
builder.Services.AddScoped<ModuleService>();
builder.Services.AddScoped<WorkService>();
builder.Services.AddScoped<QuizService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<ReferralService>();
builder.Services.AddScoped<StudentLifecycleService>();
builder.Services.AddScoped<AnnouncementService>();
builder.Services.AddScoped<SchoolSetupService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<GradebookService>();
builder.Services.AddScoped<GradeService>();
builder.Services.AddScoped<AchievementService>();
builder.Services.AddScoped<CalendarService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddHostedService<AutomationWorker>();
builder.Services.AddScoped<DatabaseInitializer>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "seaitt-lms.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Plain http://localhost is fine while developing, everywhere else the cookie is https-only.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.ReturnUrlParameter = "returnUrl";
        options.ExpireTimeSpan = TimeSpan.FromHours(authOptions.SessionHours);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = AuthCookieEvents.ValidatePrincipalAsync;
    });

builder.Services.AddAuthorization(options =>
{
    // Every page needs a signed-in user unless it says [AllowAnonymous] (the login page does).
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

// An address nothing answers to shows the "Page not found" page (still with the 404 status).
app.UseStatusCodePagesWithReExecute("/not-found");

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Signing out has to clear a cookie, which a Blazor component can't do from inside a live circuit,
// so it is a plain form post. The form carries an antiforgery token, checked here.
app.MapPost("/account/logout", async (HttpContext http, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(http);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
}).AllowAnonymous();

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    try
    {
        await initializer.InitializeAsync();
    }
    catch
    {
        // The initializer has already logged what went wrong and what to do. Stop here, without a second crash dump.
        Environment.Exit(1);
    }
}

app.Run();
