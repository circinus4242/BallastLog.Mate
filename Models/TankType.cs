using System.ComponentModel.DataAnnotations;

namespace BallastLog.Mate.Models;

public class TankType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(32)]
    public string Name { get; set; } = "";
    [Required, RegularExpression("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    public string ColorHex { get; set; } = "#6c757d";
}
