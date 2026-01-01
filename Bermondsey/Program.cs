using Bermondsey;
using Bermondsey.Clients.NotificationClient;
using Bermondsey.Clients.SmsClient;
using Bermondsey.MessageTemplate;
using Bermondsey.NotificationOrchestrator;
using Bermondsey.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.Configure<MessageTemplatesOptions>(
    builder.Configuration.GetSection("MessageTemplates"));

builder.Services.Configure<SmsOptions>(
   builder.Configuration.GetSection("Sms"));

builder.Services.Configure<NotificationSentByOptions>(
    builder.Configuration.GetSection("NotificationStatusDatabase"));

builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration["NotificationHub:ConnectionString"];
    var hubname = builder.Configuration["NotificationHub:HubName"];

    return new NotificationHubClient(connectionString, hubname);
});

builder.Services.AddHttpClient();
builder.Services.AddScoped<MessageFormatter>();
builder.Services.AddScoped<ISmsClient, RealSmsClient>();
builder.Services.AddScoped<INotifcationClient, NotificationClient>();
builder.Services.AddScoped<INotificationSentByRepository, NotificationSentByRepository>();
builder.Services.AddScoped<INotificationOrchestrator, NotificationOrchestrator>();

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Logging
    .ClearProviders()
    .AddConsole()
    .AddApplicationInsights();

builder.Build().Run();
