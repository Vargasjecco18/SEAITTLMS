using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Auth;
using SEAITT_LMS.Components;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;
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
builder.Services.AddScoped<MaterialService>();
builder.Services.AddScoped<ProviderService>();
builder.Services.AddScoped<EvaluationService>();
builder.Services.AddScoped<SchoolSetupService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<GradebookService>();
builder.Services.AddScoped<GradeService>();
builder.Services.AddScoped<AchievementService>();
builder.Services.AddScoped<CalendarService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddSingleton<ProfilePhotoService>();
builder.Services.AddSingleton<SystemSettingsService>();
builder.Services.AddSingleton<SecurityLogService>();
builder.Services.AddSingleton<PresenceService>();
builder.Services.AddScoped<MessageLauncher>();
builder.Services.AddScoped<PeopleService>();
builder.Services.AddScoped<FriendService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<FileService>();
builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection(BackupOptions.SectionName));
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService<MaintenanceWorker>();

// A backup file can be big: the form of the upload page (the administrator's) takes a file of up to 2 GB.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024);
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

// The upload of a backup file may be as big as the multipart limit above (the server's own limit of 30 MB is lifted for this one address).
app.Use(async (http, next) =>
{
    if (http.Request.Path == "/admin/backups/upload" && http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
    {
        limit.MaxRequestBodySize = 2L * 1024 * 1024 * 1024;
    }

    await next();
});

// While the system is closed (maintenance mode, or a backup being restored) only an administrator may use it. Everybody else who is
// signed in is sent to the maintenance page; the sign-in page, signing out and the files the pages need still work.
app.Use(async (http, next) =>
{
    var closed = http.RequestServices.GetRequiredService<SystemSettingsService>().IsClosed;
    if (closed && http.User.Identity?.IsAuthenticated == true && !http.User.IsInRole(AppRoles.Admin) && !IsOpenWhileClosed(http.Request.Path))
    {
        if (HttpMethods.IsGet(http.Request.Method) && !http.Request.Path.StartsWithSegments("/_blazor"))
        {
            http.Response.Redirect("/maintenance");
        }
        else
        {
            http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }

        return;
    }

    await next();
});

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

    var signedOutUserId = int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    if (signedOutUserId is not null)
    {
        await http.RequestServices.GetRequiredService<SecurityLogService>().AddAsync(SecurityEvents.SignedOut, signedOutUserId, client: ClientInfo.From(http));
    }

    return Results.LocalRedirect("/login");
}).AllowAnonymous();

// The security log as a file the administrator can open in a spreadsheet (the same filter as the page: ?group=&q=&severity=&from=&to=).
app.MapGet("/admin/security-logs/export", async (HttpContext http, SecurityLogService logs, TimeProvider clock) =>
{
    var query = http.Request.Query;
    var filter = new SecurityLogFilter(
        query["group"], query["q"], DateOnly.TryParse(query["from"], out var from) ? from : null, DateOnly.TryParse(query["to"], out var to) ? to : null, query["severity"]);
    var rows = await logs.GetAllAsync(filter, ct: http.RequestAborted);

    var csv = new System.Text.StringBuilder();
    csv.Append('\uFEFF'); // so a spreadsheet reads the letters right
    csv.Append("When (Philippine time),Event,Kind,Email,Done by,IP address,Browser,Details\r\n");
    foreach (var r in rows)
    {
        csv.Append(string.Join(',', new[]
        {
            Csv(PhilippineTime.FromUtc(r.AtUtc).ToString("yyyy-MM-dd HH:mm:ss")), Csv(SecurityEvents.Display(r.Kind)), Csv(r.Severity),
            Csv(r.Email), Csv(r.ActorName), Csv(r.IpAddress), Csv(r.UserAgent), Csv(r.Details),
        })).Append("\r\n");
    }

    var name = $"security-log-{PhilippineTime.Now(clock):yyyyMMdd-HHmm}.csv";
    return Results.File(new System.Text.UTF8Encoding(false).GetBytes(csv.ToString()), "text/csv; charset=utf-8", name);

    // A text in quotes; one that a spreadsheet could read as a formula (=, +, -, @) gets a ' in front so it is only text.
    static string Csv(string? value)
    {
        value ??= "";
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}).RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.Admin });

// Backups: one is downloaded from the page, one is sent in (to be restored from the list). Only administrators.
app.MapGet("/admin/backups/download/{name}", (string name, BackupService backups) =>
{
    var path = backups.PathOf(name);
    return path is null ? Results.NotFound() : Results.File(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), "application/sql", name);
}).RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.Admin });

app.MapPost("/admin/backups/upload", async (HttpContext http, IFormFile? backup, BackupService backups) =>
{
    if (backup is null || backup.Length == 0)
    {
        return Results.LocalRedirect("/admin/backups?error=" + Uri.EscapeDataString("Choose a backup file first."));
    }

    var userId = int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    await using var stream = backup.OpenReadStream();
    var (name, error) = await backups.SaveUploadedAsync(userId, stream, http.RequestAborted);
    return Results.LocalRedirect(name is not null
        ? "/admin/backups?uploaded=" + Uri.EscapeDataString(name)
        : "/admin/backups?error=" + Uri.EscapeDataString(error ?? "The file could not be saved."));
}).RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.Admin });

// A file of the locker (Resources): the owner opens it, and so does anybody it was shared with. Always saved as a file (never shown in the
// page), so a file with a web page in it can't run as part of this site.
app.MapGet("/files/{id:int}", async (int id, HttpContext http, FileService files) =>
{
    var userId = int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : 0;
    var file = await files.OpenAsync(userId, id, http.RequestAborted);
    if (file is null)
    {
        return Results.NotFound();
    }

    http.Response.Headers.XContentTypeOptions = "nosniff";
    http.Response.Headers.CacheControl = "private, no-store";
    return Results.File(file.Data, "application/octet-stream", file.Name);
});

// A person's picture (settings menu). The address has the version of the picture (?v=), so the browser may keep it for good
// and asks again only when the picture changed. Only signed-in people get it (the fallback policy of the app).
app.MapGet("/photos/{userId:int}", async (int userId, ProfilePhotoService photos, HttpContext http) =>
{
    var photo = await photos.GetAsync(userId, http.RequestAborted);
    if (photo is null)
    {
        return Results.NotFound();
    }

    http.Response.Headers.CacheControl = http.Request.Query.ContainsKey("v") ? "private, max-age=31536000, immutable" : "private, no-cache";
    http.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(photo.Value.Data, photo.Value.ContentType);
});

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

    // Who has a picture (so an avatar is drawn without asking the database each time), and the settings of the administrator.
    await scope.ServiceProvider.GetRequiredService<ProfilePhotoService>().LoadAsync();
    await scope.ServiceProvider.GetRequiredService<SystemSettingsService>().LoadAsync();
}

app.Run();

// What still works for a person who is not an administrator while the system is closed.
static bool IsOpenWhileClosed(PathString path) =>
    path.StartsWithSegments("/maintenance") || path.StartsWithSegments("/login") || path.StartsWithSegments("/account")
    || path.StartsWithSegments("/_framework") || path.StartsWithSegments("/_content") || path.StartsWithSegments("/images")
    || path.Value is { } value && (value.EndsWith(".css") || value.EndsWith(".js") || value.EndsWith(".png") || value.EndsWith(".ico") || value.EndsWith(".map"));
