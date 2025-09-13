using Azure.Messaging.ServiceBus;
using Bermondsey;
using Bermondsey.Clients;
using Bermondsey.Clients.Stratford;
using Bermondsey.Clients.Waterloo;
using Bermondsey.Options;
using Bermondsey.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.Configure<ServiceBusOptions>(
          builder.Configuration.GetSection("ServiceBus"));

builder.Services.Configure<RedisOptions>(
    builder.Configuration.GetSection("Redis"));

builder.Services.Configure<MessageTemplatesOptions>(
    builder.Configuration.GetSection("MessageTemplates"));

builder.Services.Configure<SmsOptions>(
   builder.Configuration.GetSection("Sms"));

builder.Services.Configure<JwtOptions>(
   builder.Configuration.GetSection("Jwt"));

builder.Services.Configure<WaterlooOptions>(
    builder.Configuration.GetSection("Waterloo"));

builder.Services.Configure<StratfordOptions>(
   builder.Configuration.GetSection("Stratford"));

builder.Services.AddSingleton(sp =>
{
    var serviceBusConnection = Environment.GetEnvironmentVariable("ServiceBusConnection");
    return new ServiceBusClient(serviceBusConnection);
});

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
    var configOptions = ConfigurationOptions.Parse(options.Connection);
    configOptions.AbortOnConnectFail = false;

    return ConnectionMultiplexer.Connect(configOptions);
});

builder.Services.AddHttpClient();
builder.Services.AddScoped<TokenProvider>();
builder.Services.AddScoped<MessageFormatter>();
builder.Services.AddScoped<ISmsClient, RealSmsClient>();
builder.Services.AddScoped<IWaterlooClient, WaterlooClient>();
builder.Services.AddScoped<IStratfordClient, StratfordClient>();
builder.Services.AddScoped<IUserNotifiedRepository, UserNotifiedRepository>();
builder.Services.AddScoped<DisruptionNotifier>();

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
