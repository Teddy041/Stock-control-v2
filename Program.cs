using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StockControl.Data;
using System.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});

builder.Services.AddControllersWithViews();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
});
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(4);
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

static string GetAlternateCrashLogPath()
{
    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StockControl");
    try { Directory.CreateDirectory(dir); }
    catch { /* ignore */ }
    return Path.Combine(dir, "crash.log");
}

var crashLogPaths = new[] { Path.Combine(AppContext.BaseDirectory, "crash.log"), GetAlternateCrashLogPath() };

void WriteCrashLog(string title, Exception? ex = null)
{
    var sb = new StringBuilder();
    sb.AppendLine("=================================================");
    sb.AppendLine(DateTime.Now.ToString("u"));
    sb.AppendLine(title);
    if (ex is not null)
    {
        sb.AppendLine(ex.ToString());
    }

    var payload = sb.ToString();
    foreach (var crashLogPath in crashLogPaths)
    {
        try
        {
            File.AppendAllText(crashLogPath, payload);
        }
        catch
        {
            // Ignore per-path failures (ör. exe klasörü yazma kisitli olabilir).
        }
    }
}

AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    WriteCrashLog("UnhandledException", eventArgs.ExceptionObject as Exception);
};

TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
{
    WriteCrashLog("UnobservedTaskException", eventArgs.Exception);
    eventArgs.SetObserved();
};

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        // If an old database exists without Identity tables, recreate it once.
        var needsRecreate = false;
        if (dbContext.Database.CanConnect())
        {
            var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='AspNetRoles';";
            var result = await command.ExecuteScalarAsync();
            needsRecreate = result is null || result == DBNull.Value;
            await connection.CloseAsync();
        }

        if (needsRecreate)
        {
            await dbContext.Database.EnsureDeletedAsync();
        }

        await dbContext.Database.EnsureCreatedAsync();
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Categories" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Categories" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL
            );
            """);
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Categories_Name" ON "Categories" ("Name");
            """);
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "StockRequests" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_StockRequests" PRIMARY KEY AUTOINCREMENT,
                "ProductId" INTEGER NOT NULL,
                "RequestedByUserName" TEXT NOT NULL,
                "RequestedAmount" INTEGER NOT NULL,
                "Note" TEXT NULL,
                "IsHandled" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_StockRequests_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
            );
            """);
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_StockRequests_ProductId" ON "StockRequests" ("ProductId");
            """);
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Products_Code" ON "Products" ("Code");
            """);
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ActivityLogs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ActivityLogs" PRIMARY KEY AUTOINCREMENT,
                "CreatedAt" TEXT NOT NULL,
                "UserName" TEXT NOT NULL,
                "ActorRole" TEXT NULL,
                "EventType" INTEGER NOT NULL,
                "Summary" TEXT NOT NULL,
                "Details" TEXT NULL,
                "ProductId" INTEGER NULL
            );
            """);
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_ActivityLogs_CreatedAt" ON "ActivityLogs" ("CreatedAt");
            """);
        await SeedData.InitializeAsync(dbContext, userManager, roleManager);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Startup database initialization failed.");
    }
}

// Development'ta yakalanmayan hatalarda proses kopmasin — detay sayfasi gösterilir.
// Visual Studio ile F5 calistirirken "baglanmayi reddetti" cogu zaman buradan önce kopmadan yüzüdür.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();
app.UseStaticFiles();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Not: Burada try/catch ile tekrar throw yapmayin; Visual Studio'da
// "Thrown" exception break acikken surec debug oturumunun anormal bitmesine yol acabilir.
// Development'ta UseDeveloperExceptionPage yukarida zaten hatayi gosterir.

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

try
{
    app.Run();
}
catch (Exception ex)
{
    WriteCrashLog("AppRunException", ex);
    throw;
}
