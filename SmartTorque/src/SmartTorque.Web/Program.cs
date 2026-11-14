using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

using QuestPDF.Infrastructure;

using Serilog;

using SmartTorque.Data;
using SmartTorque.Web.Infrastructure;

// Bootstrap Serilog early so startup errors are captured
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting AssemblIQ web application");

    var builder = WebApplication.CreateBuilder(args);

    // Kestrel Server Hardening: Suppress server fingerprinting (NIST / OWASP)
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.AddServerHeader = false;
    });

    // Serilog replaces the default logger (P1.8)
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: "logs/smarttorque-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30));

    // --- Services ---
    builder.Services.AddMemoryCache();
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
            new[] { "image/svg+xml", "application/json", "application/javascript", "text/css" });
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    {
        options.Level = CompressionLevel.Fastest;
    });
    builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    {
        options.Level = CompressionLevel.Fastest;
    });

    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddIdentityServices();
    builder.Services.AddAuthorizationPolicies();
    builder.Services.AddMvcWithAntiForgery();
    builder.Services.AddApplicationServices(builder.Environment.WebRootPath);
    builder.Services.AddSignalR();

    // Industrial Reliability & Scalability Background Services (Watchdog & Async Channel Ingestion)
    builder.Services.AddSingleton<SmartTorque.Web.Infrastructure.BackgroundServices.ITelemetryIngestionChannel, SmartTorque.Web.Infrastructure.BackgroundServices.TelemetryIngestionChannel>();
    builder.Services.AddHostedService<SmartTorque.Web.Infrastructure.BackgroundServices.TelemetryIngestionWorker>();
    builder.Services.AddHostedService<SmartTorque.Web.Infrastructure.BackgroundServices.DeviceHeartbeatWatchdogService>();

    // Health check (P1.8)
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>("database");

    // TimeProvider for testable time (Architecture §15)
    builder.Services.AddSingleton(TimeProvider.System);

    // QuestPDF community licence (free for open/internal tools)
    QuestPDF.Settings.License = LicenseType.Community;

    // Rate limiting for device endpoints (Architecture §13, 20 req/s)
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("device-rate-limit", httpContext =>
        {
            var transmitterId = httpContext.Request.Headers["X-Transmitter-Id"].FirstOrDefault()
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous";
            return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                transmitterId,
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromSeconds(1),
                    QueueLimit = 0
                });
        });
    });

    var app = builder.Build();

    // --- Middleware pipeline ---
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseSerilogRequestLogging();

    // Industrial Security Headers (OWASP / NIST SP 800-82 / IEC 62443)
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Permissions-Policy", "camera=(self), microphone=(), geolocation=()");
        await next();
    });

    app.UseHttpsRedirection();
    app.UseResponseCompression();
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            var fileName = ctx.File.Name.ToLowerInvariant();
            if (fileName.EndsWith(".css") || fileName.EndsWith(".js") || fileName.EndsWith(".woff2") ||
                fileName.EndsWith(".woff") || fileName.EndsWith(".svg") || fileName.EndsWith(".png") ||
                fileName.EndsWith(".jpg") || fileName.EndsWith(".jpeg") || fileName.EndsWith(".ico"))
            {
                ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=604800, immutable");
            }
        }
    });
    app.UseRouting();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // Health endpoint (P1.8)
    app.MapHealthChecks("/health").AllowAnonymous();

    // SignalR hub for real-time readings and cockpit updates (Phase 5)
    app.MapHub<SmartTorque.Web.Hubs.LiveReadingHub>("/hubs/live");

    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    // Run migrations and seed on startup (dev only; production uses migration bundle)
    await RunMigrationsAndSeedAsync(app);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Separated to keep the top-level statements readable
static async Task RunMigrationsAndSeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Devices]') AND name = 'LastSeenSeq')
                BEGIN
                    ALTER TABLE [Devices] ADD [LastSeenSeq] BIGINT NOT NULL DEFAULT 0;
                END
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[JobUnits]') AND name = 'IntegrityHash')
                BEGIN
                    ALTER TABLE [JobUnits] ADD [IntegrityHash] NVARCHAR(128) NULL;
                END
            ");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Schema update for LastSeenSeq / IntegrityHash handled");
        }

        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied");

        await DataSeeder.SeedAsync(scope.ServiceProvider);
        logger.LogInformation("Database seeding complete");

        // Run heavy hierarchy and job-to-line mappings synchronization in background so application starts immediately
        _ = Task.Run(async () =>
        {
            try
            {
                using var bgScope = app.Services.CreateScope();
                var bgLogger = bgScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var hierarchyService = bgScope.ServiceProvider.GetRequiredService<SmartTorque.Services.IHierarchyService>();
                await hierarchyService.SyncHierarchyWithRelationalEntitiesAsync();
                bgLogger.LogInformation("Hierarchy entities synchronized successfully");

                var assemblyService = bgScope.ServiceProvider.GetRequiredService<SmartTorque.Services.IAssemblyService>();
                await assemblyService.SyncAllProductJobsWithMappingsAsync();
                bgLogger.LogInformation("Product jobs synchronized with line mappings successfully");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background hierarchy/job synchronization completed with warnings");
            }
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during database migration/seeding");
        throw;
    }
}