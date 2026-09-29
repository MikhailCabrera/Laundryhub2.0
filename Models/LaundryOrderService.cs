namespace LaundryHub2._0.Models;

public class LaundryOrderService
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public LaundryOrder Order { get; set; } = null!;

    public int ServiceId { get; set; }
    public LaundryService Service { get; set; } = null!;

    /// <summary>Price per kg locked in at the time of booking — never changes even if the catalog price changes later.</summary>
    public decimal PricePerKgSnapshot { get; set; }
}
