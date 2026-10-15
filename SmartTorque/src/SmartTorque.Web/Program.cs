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
    Log.Information("Starting SmartTorque web application");

    var builder = WebApplication.CreateBuilder(args);

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
    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddIdentityServices();
    builder.Services.AddAuthorizationPolicies();
    builder.Services.AddMvcWithAntiForgery();
    builder.Services.AddApplicationServices(builder.Environment.WebRootPath);
    builder.Services.AddSignalR();

    // Health check (P1.8)
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>("database");

    // TimeProvider for testable time (Architecture §15)
    builder.Services.AddSingleton(TimeProvider.System);

    // QuestPDF community licence (free for open/internal tools)
    QuestPDF.Settings.License = LicenseType.Community;

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
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
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
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied");

        await DataSeeder.SeedAsync(scope.ServiceProvider);
        logger.LogInformation("Database seeding complete");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during database migration/seeding");
        throw;
    }
}