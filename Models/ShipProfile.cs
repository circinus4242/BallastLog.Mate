using System.ComponentModel.DataAnnotations;

namespace BallastLog.Mate.Models;

public class ShipProfile
{
    public int Id { get; set; } = 1;
    [Required, MaxLength(200)]
    public string ShipName { get; set; } = "";
    [MaxLength(100)]
    public string? ShipClass { get; set; }
    [Range(0, int.MaxValue)]
    public int MaxFlowRate { get; set; }
    public string? Custom1Label { get; set; }
    public string? Custom2Label { get; set; }
    public string? Custom3Label { get; set; }
    public string? Custom4Label { get; set; }
    public string? Custom5Label { get; set; }
}