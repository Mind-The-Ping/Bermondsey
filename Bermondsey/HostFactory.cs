using Azure.Messaging.ServiceBus;
using Bermondsey.Options;
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

        builder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
            return new ServiceBusClient(options.ConnectionString);
        });

        builder.Services.AddHostedService<Worker>();

        return builder.Build();
    }
}
