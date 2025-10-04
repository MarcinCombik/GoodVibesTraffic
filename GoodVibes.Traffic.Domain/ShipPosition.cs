namespace GoodVibes.Traffic.Domain;

public class ShipPosition
{
    public float? LAT { get; set; }
    public float? LON { get; set; }
    public float? SPEED { get; set; }
    public float? COURSE { get; set; }
    public float? HEADING { get; set; }
    public float? ELAPSED { get; set; }
    public string? DESTINATION { get; set; }
    public string? FLAG { get; set; }
    public float? LENGTH { get; set; }
    public float? ROT { get; set; }
    public string? SHIPNAME { get; set; }
    public int? SHIPTYPE { get; set; }
    public string? SHIP_ID { get; set; }
    public float? WIDTH { get; set; }
    public long? L_FORE { get; set; }
    public long? W_LEFT { get; set; }
    public long? DWT { get; set; }
    public long? GT_SHIPTYPE { get; set; }
}