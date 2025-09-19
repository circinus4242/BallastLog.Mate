namespace BallastLog.Mate.Models;

public class OperationLeg
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OperationId { get; set; }
    public Operation Operation { get; set; } = default!;
    public Guid? TankId { get; set; }
    public Tank? Tank { get; set; }
    public bool IsSea { get; set; }
    public LegDir Direction { get; set; }
    public double Delta { get; set; }
    public double VolumeBefore { get; set; }
    public double VolumeAfter { get; set; }
}