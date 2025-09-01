using Azure.Messaging.ServiceBus;
using Bermondsey.Options;
using Microsoft.Extensions.Options;

namespace Bermondsey
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ServiceBusClient _client;
        private readonly ServiceBusOptions _serviceBusOptions;

        public Worker(ILogger<Worker> logger,
                      ServiceBusClient client,
                      IOptions<ServiceBusOptions> serviceBusOptions)
        {
            _logger = logger;
            _client = client;
            _serviceBusOptions = serviceBusOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
