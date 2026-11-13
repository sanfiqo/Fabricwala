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

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiter => { limiter.PermitLimit = 10; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; });
    options.AddFixedWindowLimiter("search", limiter => { limiter.PermitLimit = 60; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; });
});
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy.WithOrigins(builder.Configuration["Cors:FrontendOrigin"] ?? "http://localhost:5173").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentBusiness, CurrentBusiness>();
builder.Services.AddSingleton<IAuthorizationHandler, ApprovedBusinessHandler>();
builder.Services.AddMarketplacePersistence(builder.Configuration);
builder.Services.AddScoped<IBusinessDirectory, EfBusinessDirectory>();
builder.Services.AddScoped<IProductDirectory, EfProductDirectory>();
builder.Services.AddScoped<ICartService, EfCartService>();
builder.Services.AddScoped<IOrderService, EfOrderService>();
builder.Services.AddScoped<ICheckoutService, EfCheckoutService>();
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

if (!app.Environment.IsEnvironment("Testing"))
{
    await SanfiqoRoleSeeder.SeedAsync(app.Services);
    using var seedScope = app.Services.CreateScope();
    await MarketplaceSeed.SeedAsync(seedScope.ServiceProvider.GetRequiredService<MarketplaceDbContext>());
}

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler();
app.UseRateLimiter();
app.UseCors("frontend");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAntiforgery();
app.UseAuthorization();

app.MapControllers();
app.MapSanfiqoAuthEndpoints();
app.MapBusinessEndpoints();
app.MapAdminEndpoints();
app.MapProductEndpoints();
app.MapLandedCostEndpoints();
app.MapInvoiceEndpoints();
app.MapReturnClaimEndpoints();
app.MapNotificationEndpoints();
app.MapStorefrontEndpoints();
app.MapSellerDashboardEndpoints();
app.MapCartEndpoints();
app.MapOrderEndpoints();
app.MapPaymentEndpoints();
app.MapCommunityEndpoints();
app.MapHealthChecks("/api/v1/health");

app.Run();
