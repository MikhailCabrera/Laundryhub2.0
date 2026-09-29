namespace LaundryHub2._0.Models;

public class CustomerNote
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public ApplicationUser? Customer { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
