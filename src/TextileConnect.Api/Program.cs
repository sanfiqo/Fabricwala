using Serilog;
using Sanfiqo.Identity;
using TextileConnect.Application.Businesses;
using TextileConnect.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentBusiness, CurrentBusiness>();
builder.Services.AddSingleton<IBusinessDirectory, InMemoryBusinessDirectory>();
builder.Services.AddSanfiqoIdentity(builder.Configuration, builder.Environment);

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
app.MapHealthChecks("/api/v1/health");

app.Run();
