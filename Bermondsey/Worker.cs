using Azure.Messaging.ServiceBus;
using Bermondsey.Messages;
using Bermondsey.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Bermondsey
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ServiceBusClient _client;
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceBusOptions _serviceBusOptions;
        private readonly List<ServiceBusProcessor> _processors = [];

        public Worker(ILogger<Worker> logger,
                      ServiceBusClient client,
                       IServiceProvider serviceProvider,
                      IOptions<ServiceBusOptions> serviceBusOptions)
        {
            _logger = logger;
            _client = client;
            _serviceBusOptions = serviceBusOptions.Value;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            if (_logger.IsEnabled(LogLevel.Information)) {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            await CreateQueueProcessor(_serviceBusOptions.Queues.Disruptions, stoppingToken);

            var topic = _serviceBusOptions.Topics.DisruptionEnds;
            await CreateTopicProcessor(topic.Name, topic.Subscription, stoppingToken); 

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Service Bus processing error");
            return Task.CompletedTask;
        }

        private async Task CreateQueueProcessor(string queueName, CancellationToken stoppingToken)
        {
            var processor = _client.CreateProcessor(queueName);

            processor = _client.CreateProcessor(queueName);
            processor.ProcessMessageAsync += async args =>
            {
                using var scope = _serviceProvider.CreateScope();
                var disruptionNotifier = scope.ServiceProvider.GetRequiredService<DisruptionNotifier>();

                var json = args.Message.Body.ToArray();
                var message = JsonSerializer.Deserialize<Disruption>(json);

                await disruptionNotifier.NotifyDisruptionAsync(message!);
                await args.CompleteMessageAsync(args.Message);
            };

            processor.ProcessErrorAsync += ErrorHandler;

            _processors.Add(processor);

            await processor.StartProcessingAsync(stoppingToken);
        }

        private async Task CreateTopicProcessor(string topicName, string subscriptionName, CancellationToken stoppingToken)
        {
            var processor = _client.CreateProcessor(topicName, subscriptionName);
            processor.ProcessMessageAsync += async args =>
            {
                using var scope = _serviceProvider.CreateScope();
                var disruptionNotifier = scope.ServiceProvider.GetRequiredService<DisruptionNotifier>();

                var json = args.Message.Body.ToArray();
                var message = JsonSerializer.Deserialize<DisruptionEnd>(json);

                await disruptionNotifier.NotifyDisruptionResolvedAsync(message!);
                await args.CompleteMessageAsync(args.Message);
            };

            processor.ProcessErrorAsync += ErrorHandler;

            _processors.Add(processor);

            await processor.StartProcessingAsync(stoppingToken);
        }
    }
}
