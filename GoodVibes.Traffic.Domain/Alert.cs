namespace GoodVibes.Traffic.Domain;

public class Alert
{
    public string ALERT_TYPE { get; set; }
    public List<string> SHIP_IDS { get; set; }
    public string REASON { get; set; }
}