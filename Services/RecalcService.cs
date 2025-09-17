using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using Microsoft.EntityFrameworkCore;

namespace BallastLog.Mate.Services;

public class RecalcService
{
    private readonly AppDbContext _db;
    public RecalcService(AppDbContext db) => _db = db;

    public async Task RecalculateAllAsync()
    {
        var profile = await _db.ShipProfiles.FirstAsync(p => p.Id == 1);
        var tanks = await _db.Tanks.OrderBy(t => t.Order).ThenBy(t => t.Code).ToListAsync();
        var tankVol = tanks.ToDictionary(t => t.Id, t => t.InitialCapacity);

        var ops = await _db.Operations
            .Include(o => o.Legs).ThenInclude(l => l.Tank)
            .OrderBy(o => o.StartLocal).ThenBy(o => o.CreatedUtc)
            .ToListAsync();

        foreach (var op in ops)
        {
            op.State = OpState.Ok;

            foreach (var leg in op.Legs.Where(l => !l.IsSea && l.TankId.HasValue))
            {
                leg.VolumeBefore = tankVol[leg.TankId!.Value];
            }

            var totalFrom = op.Legs.Where(l => l.Direction == LegDir.From).Sum(l => l.Delta);
            var totalTo = op.Legs.Where(l => l.Direction == LegDir.To).Sum(l => l.Delta);

            if (totalFrom != totalTo || totalFrom != op.TotalAmount)
                op.State = OpState.InvalidTotals;

            foreach (var leg in op.Legs.Where(l => !l.IsSea && l.TankId.HasValue))
            {
                var signed = leg.Delta * (int)leg.Direction;
                var cur = tankVol[leg.TankId!.Value] + signed;

                if (cur < 0) op.State = OpState.Underflow;
                if (cur > leg.Tank!.MaxCapacity) op.State = OpState.Overflow;

                cur = Math.Clamp(cur, 0, leg.Tank!.MaxCapacity);
                tankVol[leg.TankId!.Value] = cur;
                leg.VolumeAfter = cur;
            }

            var hours = Math.Max((op.StopLocal - op.StartLocal).TotalHours, 1.0 / 60.0);
            op.FlowRate = op.TotalAmount / hours;
            if (profile.MaxFlowRate > 0 && op.FlowRate > profile.MaxFlowRate)
                op.State = OpState.FlowExceeded;
        }

        foreach (var t in tanks)
            t.CurrentCapacity = tankVol[t.Id];

        await _db.SaveChangesAsync();
    }
}