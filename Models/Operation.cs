using System.ComponentModel.DataAnnotations;

namespace BallastLog.Mate.Models;

public class Operation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required]
    public DateTime StartLocal { get; set; }
    [Required]
    public DateTime StopLocal { get; set; }
    [Required, MaxLength(6)]
    public string TzOffset { get; set; } = "+00:00";
    [MaxLength(200)]
    public string? LocationStart { get; set; }
    [MaxLength(200)]
    public string? LocationStop { get; set; }
    public OpType Type { get; set; } = OpType.B;
    public bool BwtsUsed { get; set; }
    [MaxLength(1000)]
    public string? Remark { get; set; }
    public int? MinDepth { get; set; }
    public int? DistanceNearestLand { get; set; }
    public string? Custom1 { get; set; }
    public string? Custom2 { get; set; }
    public string? Custom3 { get; set; }
    public string? Custom4 { get; set; }
    public string? Custom5 { get; set; }
    public double TotalAmount { get; set; }
    public double FlowRate { get; set; }
    public bool RecordedToLogBook { get; set; }
    public bool RecordedToFm232 { get; set; }
    public OpState State { get; set; } = OpState.Ok;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<OperationLeg> Legs { get; set; } = new();
}