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
builder.Services.AddScoped<EnrollmentImportService>();
builder.Services.AddScoped<FileDownloads>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddSingleton<LiveUpdates>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<EmailWorker>();
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
builder.Services.AddScoped<CourseScheduleService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<GradebookService>();
builder.Services.AddScoped<GradeService>();
builder.Services.AddScoped<AchievementService>();
builder.Services.AddScoped<PopupGate>();
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
builder.Services.AddScoped<AttachmentService>();
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

// A list with a live button on every line (the registrar's enrollments: two islands a line) tells the server about all of its
// islands in one message when the page is opened from another page. The default limit of 32 KB is too small for that.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 1024 * 1024);

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
app.MapPost("/account/logout", async (HttpContext http, IAntiforgery antiforgery, AuthService auth) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(http);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    // The account is free for the next sign-in (one sign-in at a time).
    if (int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
    {
        await auth.EndSessionAsync(id, http.User.FindFirstValue(AppClaimTypes.SessionId), http.RequestAborted);
    }

    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    return Results.LocalRedirect("/login");
}).AllowAnonymous();

// A page that is open found that its sign-in is not the open one any more (the account was signed in somewhere else): the sign-in is ended here and the login page says why.
app.MapGet("/account/session-ended", async (HttpContext http, AuthService auth) =>
{
    if (int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        && !await auth.TouchSessionAsync(id, http.User.FindFirstValue(AppClaimTypes.SessionId), http.RequestAborted))
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.LocalRedirect("/login?alert=other-device");
    }

    return Results.LocalRedirect("/");
}).AllowAnonymous();

// After a person changed their own password (Settings) the session they are in is over: the security stamp of the cookie is not the account's any more.
// This ends it at once (the cookie check would only notice it after a while) and goes to the login page. It does nothing for a session that is still valid.
app.MapGet("/account/password-changed", async (HttpContext http, IDbContextFactory<AppDbContext> dbFactory) =>
{
    var stamp = http.User.FindFirstValue(AppClaimTypes.SecurityStamp);
    if (int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && stamp is not null)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var current = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.SecurityStamp).FirstOrDefaultAsync(http.RequestAborted);
        if (current is not null && current != stamp)
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/login?notice=password-changed");
        }
    }

    return Results.LocalRedirect("/");
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

// The Analytics pages as Excel files (Export to Excel): the student analytics of the administrator, the guidance counselor and a teacher; the analytics of the
// academic provider; the teacher evaluation analytics of the academic coordinator. The file has the numbers of the page the person is allowed to see.
app.MapGet("/analytics/export", async (HttpContext http, AnalyticsService reports, TimeProvider clock) =>
{
    var id = int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : 0;
    var role = http.User.FindFirstValue(ClaimTypes.Role) ?? "";
    var user = new CurrentUserInfo(id, role, http.User.Identity?.Name ?? "", http.User.FindFirstValue(ClaimTypes.Email) ?? "");
    if (await reports.GetAsync(user, http.RequestAborted) is not { } report)
    {
        return Results.NotFound();
    }

    var now = PhilippineTime.Now(clock).DateTime;
    return Results.File(AnalyticsExport.ForStudents(report, user.Name, now), FileDownloads.XlsxType, AnalyticsExport.FileName("Student analytics", now));
}).RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.Admin + "," + AppRoles.GuidanceCounselor + "," + AppRoles.Teacher });

app.MapGet("/provider/analytics/export", async (HttpContext http, ProviderService provider, TimeProvider clock) =>
{
    var data = await provider.GetAnalyticsAsync(http.RequestAborted);
    var now = PhilippineTime.Now(clock).DateTime;
    return Results.File(AnalyticsExport.ForProvider(data, now), FileDownloads.XlsxType, AnalyticsExport.FileName("Provider analytics", now));
}).RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.AcademicProvider });

app.MapGet("/coordinator/analytics/export", async (HttpContext http, EvaluationService evaluations, TimeProvider clock) =>
{
    var data = await evaluations.GetAnalyticsAsync(int.TryParse(http.Request.Query["evaluation"], out var evaluation) ? evaluation : null, http.RequestAborted);
    if (data.EvaluationId is null)
    {
        return Results.LocalRedirect("/coordinator/analytics");
    }

    // The page opens one teacher at a time (their courses, their questions, what the students wrote); the file has all of them.
    var details = new List<TeacherDetail>();
    foreach (var teacher in data.Teachers)
    {
        if (await evaluations.GetTeacherDetailAsync(data.EvaluationId.Value, teacher.TeacherId, http.RequestAborted) is { } detail)
        {
            details.Add(detail);
        }
    }

    var now = PhilippineTime.Now(clock).DateTime;
    return Results.File(AnalyticsExport.ForEvaluation(data, now, details), FileDownloads.XlsxType, AnalyticsExport.FileName("Teacher evaluation analytics", now));
}).RequireAuthorization(new AuthorizeAttribute { Roles = AppRoles.AcademicCoordinator });

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

    return ServeFile(http, file.Name, file.Data);
});

// A file attached to a syllabus, a module or an activity (the provider's library, a teacher's course): the provider, the teacher of the course and
// the students of the course (once it is posted) open it. A PDF, a picture, a text file, a sound or a video opens in the browser tab, where the browser's
// own viewer has the download and print buttons; "?download=1" saves it instead, and every other kind of file is saved.
app.MapGet("/files/a/{id:int}", async (int id, HttpContext http, AttachmentService attachments) =>
{
    var userId = int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : 0;
    var file = await attachments.OpenAsync(userId, id, http.RequestAborted);
    return file is null ? Results.NotFound() : ServeFile(http, file.Name, file.Data);
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

// Hands a file to the browser: shown in the tab when it is a kind the browser can show (and "?download=1" was not asked), otherwise saved.
static IResult ServeFile(HttpContext http, string name, byte[] data)
{
    var (type, canShow) = FilePreview.Decide(name, data);
    var show = canShow && http.Request.Query["download"] != "1";
    http.Response.Headers.XContentTypeOptions = "nosniff";
    http.Response.Headers.CacheControl = "private, no-store";
    http.Response.Headers.ContentDisposition = FilePreview.Disposition(show, name);
    return Results.File(data, show ? type : "application/octet-stream", enableRangeProcessing: show);
}

app.Run();

// What still works for a person who is not an administrator while the system is closed.
static bool IsOpenWhileClosed(PathString path) =>
    path.StartsWithSegments("/maintenance") || path.StartsWithSegments("/login") || path.StartsWithSegments("/account")
    || path.StartsWithSegments("/_framework") || path.StartsWithSegments("/_content") || path.StartsWithSegments("/images")
    || path.Value is { } value && (value.EndsWith(".css") || value.EndsWith(".js") || value.EndsWith(".png") || value.EndsWith(".ico") || value.EndsWith(".map"));
