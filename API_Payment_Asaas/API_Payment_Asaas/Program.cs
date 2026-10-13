using API_Payment_Asaas.Data;
using API_Payment_Asaas.Middleware;
using API_Payment_Asaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var connectionString = builder.Configuration.GetConnectionString("PaymentDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'PaymentDatabase' is required.");
}

builder.Services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.Configure<AsaasOptions>(builder.Configuration.GetSection(AsaasOptions.SectionName));
builder.Services.AddHttpClient<IAsaasClient, AsaasClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AsaasOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");

    if (!string.IsNullOrWhiteSpace(options.ApiKey))
    {
        client.DefaultRequestHeaders.Add("access_token", options.ApiKey);
    }
});
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IWebhookInboxService, WebhookInboxService>();
builder.Services.AddScoped<IWebhookProcessor, WebhookProcessor>();
builder.Services.AddHostedService<WebhookProcessorService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
