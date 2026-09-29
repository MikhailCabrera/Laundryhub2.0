namespace LaundryHub2._0.Models;

public class RiderLocationDto
{
    public int? OrderId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public long? ClientTimestamp { get; set; }
}

public class RiderLocationBroadcastPayload
{
    public int OrderId { get; set; }
    public string RiderId { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ActiveRiderLocationDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string RiderId { get; set; } = string.Empty;
    public string RiderName { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string TrackingType { get; set; } = string.Empty; // "pickup" or "delivery"
    public string Status { get; set; } = string.Empty;
    public decimal? OrderLatitude { get; set; }
    public decimal? OrderLongitude { get; set; }
}

