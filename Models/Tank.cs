using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BallastLog.Mate.Models;

public class Tank
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(16)]
    public string Code { get; set; } = "";
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";
    [Range(typeof(decimal), "0.0", "99999.9")]
    public double MaxCapacity { get; set; }
    [Range(typeof(decimal), "0.0", "99999.9")]
    public double InitialCapacity { get; set; }
    [Range(typeof(decimal), "0.0", "99999.9")]
    public double CurrentCapacity { get; set; }
    public bool IsActive { get; set; } = true;
    public int Order { get; set; }
    public Guid? TankTypeId { get; set; }
    public TankType? TankType { get; set; }
}