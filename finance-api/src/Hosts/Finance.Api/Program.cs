using System.Text.Json.Serialization;
using Finance.Modules.Accounts.Infrastructure;
using Finance.Modules.Accounts.Presentation.Controllers;
using Finance.Modules.Transfers.Infrastructure;
using Finance.Modules.Transfers.Presentation.Controllers;
using Finance.Shared.Infrastructure.Persistence;
using Finance.Shared.Presentation;
using Finance.Shared.Presentation.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFinancePersistence(builder.Configuration);
builder.Services.AddAccountsModule(builder.Configuration);
builder.Services.AddTransfersModule(builder.Configuration);

builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(AccountsController).Assembly)
    .AddApplicationPart(typeof(TransfersController).Assembly)
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddFinancePresentation();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<FinanceDbContext>("database");

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Location", IdempotencyHeaders.IdempotentReplayed)));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

if (app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Finance API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
