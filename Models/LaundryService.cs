namespace LaundryHub2._0.Models;

public class LaundryService
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PricePerKg { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RequiresWashing { get; set; } = true;
    public bool RequiresDrying { get; set; } = true;
    public decimal DetergentMlPerKg { get; set; } = 0m;
    public decimal SoftenerMlPerKg { get; set; } = 0m;

    // Navigation
    public ICollection<LaundryOrderService> OrderServices { get; set; } = new List<LaundryOrderService>();
}
