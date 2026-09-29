namespace LaundryHub2._0.Models;

/// <summary>
/// Daily sidecar for atomic order-number reservation. One row per UTC date;
/// NextSeq is reserved via INSERT ... ON DUPLICATE KEY UPDATE + LAST_INSERT_ID().
/// </summary>
public class LaundryOrderSequence
{
    public DateTime OrderDate { get; set; }
    public int NextSeq { get; set; }
}
