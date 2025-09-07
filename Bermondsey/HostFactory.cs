using Azure.Messaging.ServiceBus;
using Bermondsey.Clients;
using Bermondsey.Clients.Stratford;
using Bermondsey.Clients.Waterloo;
using Bermondsey.Options;
using Bermondsey.Repositories;
using Microsoft.Extensions.Options;

namespace Bermondsey;
public static class HostFactory
{
    public static IHost CreateHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

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
            var options = sp.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
            return new ServiceBusClient(options.ConnectionString);
        });

        builder.Services.AddHttpClient();
        builder.Services.AddScoped<TokenProvider>();
        builder.Services.AddScoped<MessageFormatter>();
        builder.Services.AddScoped<ISmsClient, RealSmsClient>();
        builder.Services.AddScoped<IWaterlooClient, WaterlooClient>();
        builder.Services.AddScoped<IStratfordClient, StratfordClient>();
        builder.Services.AddScoped<IUserNotifiedRepository, UserNotifiedRepository>();
        builder.Services.AddScoped<DisruptionNotifier>();

        builder.Services.AddHostedService<Worker>();

        return builder.Build();
    }
}
