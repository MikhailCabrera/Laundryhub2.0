namespace LaundryHub2._0.Models;

public class MapSettings
{
    public string TileUrl { get; set; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
    public string Attribution { get; set; } = "&copy; <a href=\"https://www.openstreetmap.org/copyright\" target=\"_blank\" rel=\"noopener\">OpenStreetMap</a> contributors";
    public double InitialLatitude { get; set; } = 7.0731; // Davao City default
    public double InitialLongitude { get; set; } = 125.6128;
    public double InitialZoom { get; set; } = 13;
}
