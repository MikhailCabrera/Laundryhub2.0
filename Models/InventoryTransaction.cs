namespace LaundryHub2._0.Models;

public enum InventoryStockAction
{
    Add = 0,
    Deduct = 1
}

public class InventoryTransaction
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;
    public InventoryStockAction Action { get; set; }
    public decimal Quantity { get; set; }
    public decimal PreviousStock { get; set; }
    public decimal NewStock { get; set; }
    public string? PerformedByUserId { get; set; }
    public ApplicationUser? PerformedBy { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
