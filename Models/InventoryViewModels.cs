using System.ComponentModel.DataAnnotations;

namespace LaundryHub2._0.Models;

public class InventoryItemInputModel : IValidatableObject
{
    [Required(ErrorMessage = "Item name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Item name must be 2-100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Unit of measurement is required.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "Unit must be 1-20 characters.")]
    public string Unit { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "Stock must be between 0 and 999,999.")]
    public decimal CurrentStock { get; set; }

    [Range(typeof(decimal), "0.01", "999999", ErrorMessage = "Max capacity must be greater than 0.")]
    public decimal MaxCapacity { get; set; }

    [Range(typeof(decimal), "0", "999999", ErrorMessage = "Low-stock threshold must be between 0 and 999,999.")]
    public decimal LowStockThreshold { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (LowStockThreshold > MaxCapacity)
            yield return new ValidationResult("Low-stock threshold cannot exceed max capacity.", new[] { nameof(LowStockThreshold) });
        if (CurrentStock > MaxCapacity)
            yield return new ValidationResult("Current stock cannot exceed max capacity.", new[] { nameof(CurrentStock) });
    }
}

public class InventoryStockAdjustmentInputModel
{
    [Required(ErrorMessage = "Item is required.")]
    public int ItemId { get; set; }

    [Required(ErrorMessage = "Action is required.")]
    public InventoryStockAction Action { get; set; }

    [Range(typeof(decimal), "0.01", "999999", ErrorMessage = "Quantity must be greater than 0.")]
    public decimal Quantity { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }
}
