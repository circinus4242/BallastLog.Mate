using System.Text.Json;
using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using Microsoft.EntityFrameworkCore;

namespace BallastLog.Mate.Services;

public class ProfileIoService
{
    private readonly AppDbContext _db;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public ProfileIoService(AppDbContext db) => _db = db;

    public async Task<byte[]> ExportAsync()
    {
        var dto = new ShipProfileExportDto
        {
            Profile = await _db.ShipProfiles.FirstAsync(p => p.Id == 1),
            Tanks = await _db.Tanks.OrderBy(t => t.Order).ThenBy(t => t.Code).ToListAsync()
        };
        return JsonSerializer.SerializeToUtf8Bytes(dto, JsonOpts);
    }

    public async Task ImportAsync(Stream json)
    {
        var dto = await JsonSerializer.DeserializeAsync<ShipProfileExportDto>(json)
                  ?? throw new InvalidOperationException("Invalid JSON.");

        var prof = await _db.ShipProfiles.FirstAsync(p => p.Id == 1);
        prof.ShipName = dto.Profile.ShipName ?? "";
        prof.ShipClass = dto.Profile.ShipClass;
        prof.MaxFlowRate = dto.Profile.MaxFlowRate;
        prof.Custom1Label = dto.Profile.Custom1Label;
        prof.Custom2Label = dto.Profile.Custom2Label;
        prof.Custom3Label = dto.Profile.Custom3Label;
        prof.Custom4Label = dto.Profile.Custom4Label;
        prof.Custom5Label = dto.Profile.Custom5Label;

        _db.Tanks.RemoveRange(_db.Tanks);
        foreach (var t in dto.Tanks)
        {
            t.Id = Guid.NewGuid();
            t.CurrentCapacity = t.InitialCapacity;
            _db.Tanks.Add(t);
        }
        await _db.SaveChangesAsync();
    }
}