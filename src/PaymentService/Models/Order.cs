namespace PaymentService.Models
{
    public class Order
    {
        public string OrderId { get; set; } = string.Empty;
        public int Amount { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
