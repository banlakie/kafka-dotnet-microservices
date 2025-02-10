using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShippingService.Models;


public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConsumer<Ignore, string> _consumer;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = "kafka:9092",
            GroupId = "shipping_group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        _consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        _consumer.Subscribe("payment_completed"); 
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() =>
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var result = _consumer.Consume(stoppingToken);
                    var message = result.Message.Value;

                    _logger.LogInformation($"Received payment confirmation: {message}");

                    var order = JsonSerializer.Deserialize<Order>(message);
                    if (order != null)
                    {
                        _logger.LogInformation($"Shipping order {order.OrderId}...");
                        Task.Delay(2000).Wait();
                        _logger.LogInformation($"Order {order.OrderId} shipped!");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _consumer.Close();
            }
        }, stoppingToken);
    }

    public override void Dispose()
    {
        _consumer.Close();
        base.Dispose();
    }
}

