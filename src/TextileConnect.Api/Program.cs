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
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentBusiness, CurrentBusiness>();
builder.Services.AddSingleton<IAuthorizationHandler, ApprovedBusinessHandler>();
builder.Services.AddMarketplacePersistence(builder.Configuration);
builder.Services.AddScoped<IBusinessDirectory, EfBusinessDirectory>();
builder.Services.AddScoped<IProductDirectory, EfProductDirectory>();
builder.Services.AddScoped<ICartService, EfCartService>();
builder.Services.AddScoped<IOrderService, EfOrderService>();
builder.Services.AddScoped<IAuthBusinessProfileReader, AuthBusinessProfileReader>();
if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IFileStorage, DevelopmentFileStorage>();
builder.Services.AddSanfiqoIdentity(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization(options => options.AddPolicy("ApprovedBusiness",
    policy => policy.RequireAuthenticatedUser().AddRequirements(new ApprovedBusinessRequirement())));

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
    await SanfiqoRoleSeeder.SeedAsync(app.Services);

app.UseExceptionHandler();

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
app.MapProductEndpoints();
app.MapCartEndpoints();
app.MapOrderEndpoints();
app.MapHealthChecks("/api/v1/health");

app.Run();
