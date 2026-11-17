using Serilog;
using Microsoft.AspNetCore.Authorization;
using Sanfiqo.Identity;
using TextileConnect.Application.Businesses;
using TextileConnect.Api;
using TextileConnect.Application.Files;
using TextileConnect.Infrastructure.Files;
using TextileConnect.Application.Catalogue;
using TextileConnect.Infrastructure.Persistence;
using TextileConnect.Infrastructure.Businesses;
using TextileConnect.Infrastructure.Catalogue;
using TextileConnect.Application.Orders;
using TextileConnect.Infrastructure.Orders;
using TextileConnect.Application.Community;
using TextileConnect.Infrastructure.Community;
using TextileConnect.Application.Payments;
using TextileConnect.Infrastructure.Payments;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
}

ProductionSecretGuard.Validate(builder.Configuration, builder.Environment.EnvironmentName);

// Serilog prints nothing unless a sink is configured. Without a "Serilog:WriteTo" section in configuration, write to the console
// (so "Now listening" and the Startup lines are visible), and keep framework and per-query SQL logging quiet.
builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext();
    if (!context.Configuration.GetSection("Serilog:WriteTo").Exists())
        configuration.MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.Console();
});

// Add services to the container.

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddApiRateLimiter();
builder.Services.AddFrontendCors(builder.Configuration, builder.Environment);
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentBusiness, CurrentBusiness>();
builder.Services.AddSingleton<IAuthorizationHandler, ApprovedBusinessHandler>();
builder.Services.AddMarketplacePersistence(builder.Configuration);
builder.Services.AddScoped<IBusinessDirectory, EfBusinessDirectory>();
builder.Services.AddScoped<IProductDirectory, EfProductDirectory>();
builder.Services.AddScoped<ISellerProductService, EfSellerProductService>();
builder.Services.AddScoped<IProductPricing, EfProductPricing>();
builder.Services.AddScoped<ICartService, EfCartService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IOrderService, EfOrderService>();
builder.Services.AddScoped<IOrderMaintenance, EfOrderService>();
builder.Services.AddHostedService<OrderMaintenanceWorker>();
builder.Services.AddScoped<ICheckoutService, EfCheckoutService>();
builder.Services.AddScoped<IPaymentService, EfPaymentService>();
builder.Services.AddScoped<IFinanceService, EfFinanceService>();
builder.Services.AddScoped<ICommunityService, EfCommunityService>();
builder.Services.AddScoped<IAuthBusinessProfileReader, AuthBusinessProfileReader>();
if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IFileStorage, DevelopmentFileStorage>();
if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IPaymentGateway, DevelopmentPaymentGateway>();
else
    builder.Services.AddHttpClient<IPaymentGateway, RazorpayPaymentGateway>();
builder.Services.AddSanfiqoIdentity(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization(options => options.AddPolicy("ApprovedBusiness",
    policy => policy.RequireAuthenticatedUser().AddRequirements(new ApprovedBusinessRequirement())));

var app = builder.Build();

ProductionAdapterGuard.Validate(app.Services, app.Configuration, app.Environment.EnvironmentName);

if (!app.Environment.IsEnvironment("Testing"))
{
    // Each startup step logs when it begins and ends (with the time it took), so a slow or stuck step is easy to spot.
    async Task StartupStepAsync(string name, Func<Task> work)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        app.Logger.LogInformation("Startup: {Step} - begin", name);
        try { await work(); }
        catch (Exception exception) { app.Logger.LogError("Startup: {Step} - FAILED after {Ms} ms ({Type})", name, watch.ElapsedMilliseconds, exception.GetType().Name); throw; }
        app.Logger.LogInformation("Startup: {Step} - done in {Ms} ms", name, watch.ElapsedMilliseconds);
    }
    await StartupStepAsync("roles (identity database)", () => SanfiqoRoleSeeder.SeedAsync(app.Services));
    await StartupStepAsync("marketplace master data (states, cities, zones, categories, settings)", async () =>
    {
        using var seedScope = app.Services.CreateScope();
        await MarketplaceSeed.SeedAsync(seedScope.ServiceProvider.GetRequiredService<MarketplaceDbContext>());
    });
    if (app.Environment.IsDevelopment())
        await StartupStepAsync("development demo data (users, businesses, freight, products)", () => DevelopmentSeed.SeedAsync(app.Services)); // never in Production
}
app.Logger.LogInformation("Startup: seeding finished, starting hosted services and the web server");

app.UseMiddleware<ApiErrorMiddleware>();
app.UseRateLimiter();
app.UseCors(FrontendCors.PolicyName);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ApiCsrfMiddleware>();
app.UseAntiforgery();

app.MapSanfiqoAuthEndpoints();
app.MapBusinessEndpoints();
app.MapPincodeEndpoints();
app.MapAdminEndpoints();
app.MapProductEndpoints();
app.MapLandedCostEndpoints();
app.MapInvoiceEndpoints();
app.MapReturnClaimEndpoints();
app.MapNotificationEndpoints();
app.MapAccountEndpoints();
app.MapStorefrontEndpoints();
app.MapSellerDashboardEndpoints();
app.MapBulkProductEndpoints();
app.MapStockAlertEndpoints();
app.MapPayoutEndpoints();
app.MapAdminDashboardEndpoints();
app.MapAdminListEndpoints();
app.MapRetailerExtrasEndpoints();
app.MapAdminReportEndpoints();
app.MapProductModerationEndpoints();
app.MapCategoryBannerEndpoints();
app.MapBusinessSuspensionEndpoints();
app.MapCartEndpoints();
app.MapOrderEndpoints();
app.MapPaymentEndpoints();
app.MapCommunityEndpoints();
app.MapHealthChecks("/api/v1/health");

app.Run();
