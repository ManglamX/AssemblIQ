using SmartTorque.Core.Interfaces;
using SmartTorque.Services;

namespace SmartTorque.Web.Infrastructure;

/// <summary>
/// Registers all application services from SmartTorque.Services.
/// </summary>
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        string webRootPath)
    {
        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IColourMapService, ColourMapService>();
        services.AddScoped<IBmImportService, BmImportService>();
        services.AddScoped<IBmPreviewService, BmPreviewService>();
        services.AddScoped<IJobImportService, JobImportService>();
        services.AddSingleton<IPriorityService, PriorityService>();
        services.AddSingleton<IImageUploadService>(_ => new ImageUploadService(webRootPath));
        services.AddSingleton<IBmFileStore>(_ => new BmFileStore(webRootPath));
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddScoped<IMeasurementEvaluator, ToleranceService>();
        return services;
    }
}