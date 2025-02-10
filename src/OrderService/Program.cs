using Confluent.Kafka;
using System.Text.Json;
using OrderService.Models;


var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var config = new ProducerConfig
{
    BootstrapServers = "kafka:9092"
};

app.MapPost("/order", async (HttpContext context) =>
{
    var producer = new ProducerBuilder<Null, string>(config).Build();

    var order = await JsonSerializer.DeserializeAsync<Order>(context.Request.Body);

    if (order == null)
    {
        await context.Response.WriteAsync("Invalid order data");
        return;
    }



    var orderJson = JsonSerializer.Serialize(order);

    try
    {
        await producer.ProduceAsync("order_created", new Message<Null, string> { Value = orderJson });
        Console.WriteLine($"Order Created: {orderJson}");
    }
    catch (ProduceException<Null, string> ex)
    {
        Console.WriteLine($"Failed to send order to Kafka: {ex.Error.Reason}");
    }

});

app.Run();

