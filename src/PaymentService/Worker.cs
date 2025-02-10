using Confluent.Kafka;
using System;
using System.Text.Json;
using PaymentService.Models;


public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly IProducer<Null, string> _successProducer;
    private readonly IProducer<Null, string> _failureProducer;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = "kafka:9092",
            GroupId = "payment_group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = "kafka:9092"
        };

        _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        _consumer.Subscribe("order_created");

        _successProducer = new ProducerBuilder<Null, string>(producerConfig).Build();
        _failureProducer = new ProducerBuilder<Null, string>(producerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentService started listening for orders");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);
                if (result != null)
                {
                    await ProcessPayment(result.Message.Value);
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError($"Kafka consume error: {ex.Error.Reason}");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("PaymentService shutting down...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error: {ex.Message}");
            }
        }

        _consumer.Close();
        _successProducer.Dispose();
        _failureProducer.Dispose();
    }

    private async Task ProcessPayment(string message)
    {
        try
        {
            var order = JsonSerializer.Deserialize<Order>(message);
            if (order == null)
            {
                _logger.LogWarning("Received an invalid order message");
                return;
            }

            _logger.LogInformation($"Processing Payment for Order: {order.OrderId} | Amount: {order.Amount}");
            await Task.Delay(2000);

            if (order.Amount > 300) // I put this here deliberately to be able to simulate a payment failure
            {
                _logger.LogWarning($"Payment FAILED for Order: {order.OrderId}");

                var paymentFailedEvent = JsonSerializer.Serialize(order);
                _failureProducer.Produce("payment_failed", new Message<Null, string> { Value = paymentFailedEvent });

                _logger.LogInformation($"Sent event to Kafka: payment_failed for Order {order.OrderId}");
            }
            else
            {
                _logger.LogInformation($"Payment completed for Order: {order.OrderId}");

                var paymentCompletedEvent = JsonSerializer.Serialize(order);
                _successProducer.Produce("payment_completed", new Message<Null, string> { Value = paymentCompletedEvent });

                _logger.LogInformation($"Sent event to Kafka: payment_completed for Order {order.OrderId}");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError($"JSON deserialization error: {ex.Message}");
        }
        catch (ProduceException<Null, string> ex)
        {
            _logger.LogError($"Failed to send message to Kafka: {ex.Error.Reason}");
        }
    }
}

