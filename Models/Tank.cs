using System.ComponentModel.DataAnnotations;

namespace BallastLog.Mate.Models;

public class Tank
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(16)]
    public string Code { get; set; } = "";
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";
    [Range(0, int.MaxValue)]
    public int MaxCapacity { get; set; }
    [Range(0, int.MaxValue)]
    public int InitialCapacity { get; set; }
    [Range(0, int.MaxValue)]
    public int CurrentCapacity { get; set; }
    public bool IsActive { get; set; } = true;
    public int Order { get; set; }
}