namespace BallastLog.Mate.Models;

public class ShipProfileExportDto
{
    public ShipProfile Profile { get; set; } = new();
    public List<Tank> Tanks { get; set; } = new();
}