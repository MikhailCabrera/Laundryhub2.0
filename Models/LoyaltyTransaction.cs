namespace LaundryHub2._0.Models;

public static class LoyaltyTransactionType
{
    public const string Earn = "Earn";
    public const string Redeem = "Redeem";
    public const string Adjust = "Adjust";
    public const string Expire = "Expire";
}

public class LoyaltyTransaction
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public ApplicationUser? Customer { get; set; }
    public int? OrderId { get; set; }
    public LaundryOrder? Order { get; set; }
    public string Type { get; set; } = LoyaltyTransactionType.Earn;
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
