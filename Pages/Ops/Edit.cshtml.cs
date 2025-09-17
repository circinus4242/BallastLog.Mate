using System.Globalization;
using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using BallastLog.Mate.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BallastLog.Mate.Pages.Ops;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly RecalcService _recalc;

    public EditModel(AppDbContext db, RecalcService recalc)
    { _db = db; _recalc = recalc; }

    public class LegVm
    {
        public string Label { get; set; } = "";
        public Guid? TankId { get; set; }
        public bool IsSea { get; set; }
        public int Current { get; set; }  // current BEFORE this op (preview only)
        public int Max { get; set; }
        public int Delta { get; set; }
    }

    // Route param / query
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    // bind datetime-local as strings, parsed manually
    [BindProperty] public string StartLocalStr { get; set; } = "";
    [BindProperty] public string StopLocalStr { get; set; } = "";

    [BindProperty] public Operation Op { get; set; } = new();
    [BindProperty] public List<LegVm> From { get; set; } = new();
    [BindProperty] public List<LegVm> To { get; set; } = new();
    [BindProperty] public int Total { get; set; }

    public int MaxFlowRate { get; set; }
    public List<Tank> TankChoices { get; set; } = new();

    // labels
    public string C1Label { get; set; } = "Custom 1";
    public string C2Label { get; set; } = "Custom 2";
    public string C3Label { get; set; } = "Custom 3";
    public string C4Label { get; set; } = "Custom 4";
    public string C5Label { get; set; } = "Custom 5";

    private async Task LoadLookupsAsync()
    {
        var prof = await _db.ShipProfiles.FirstAsync(p => p.Id == 1);
        MaxFlowRate = prof.MaxFlowRate;
        C1Label = string.IsNullOrWhiteSpace(prof.Custom1Label) ? "Custom 1" : prof.Custom1Label!;
        C2Label = string.IsNullOrWhiteSpace(prof.Custom2Label) ? "Custom 2" : prof.Custom2Label!;
        C3Label = string.IsNullOrWhiteSpace(prof.Custom3Label) ? "Custom 3" : prof.Custom3Label!;
        C4Label = string.IsNullOrWhiteSpace(prof.Custom4Label) ? "Custom 4" : prof.Custom4Label!;
        C5Label = string.IsNullOrWhiteSpace(prof.Custom5Label) ? "Custom 5" : prof.Custom5Label!;
        TankChoices = await _db.Tanks.OrderBy(t => t.Order).ThenBy(t => t.Code).ToListAsync();
    }

    private void Normalize()
    {
        From = (From ?? new()).Where(l => l.IsSea || l.TankId.HasValue).ToList();
        To = (To ?? new()).Where(l => l.IsSea || l.TankId.HasValue).ToList();
        foreach (var l in From) l.Delta = Math.Max(0, l.Delta);
        foreach (var l in To) l.Delta = Math.Max(0, l.Delta);
        Total = Math.Max(0, Total);
    }

    private static bool TryParseLocal(string s, out DateTime dt)
        => DateTime.TryParseExact(s, "yyyy-MM-ddTHH:mm",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);

    public async Task<IActionResult> OnGet()
    {
        await LoadLookupsAsync();

        var entity = await _db.Operations
            .Include(o => o.Legs).ThenInclude(l => l.Tank)
            .FirstOrDefaultAsync(o => o.Id == Id);
        if (entity == null) return RedirectToPage("Index");

        // Map op
        Op = new Operation
        {
            Id = entity.Id,
            Type = entity.Type,
            BwtsUsed = entity.BwtsUsed,
            TzOffset = entity.TzOffset,
            LocationStart = entity.LocationStart,
            LocationStop = entity.LocationStop,
            Remark = entity.Remark,
            Custom1 = entity.Custom1,
            Custom2 = entity.Custom2,
            Custom3 = entity.Custom3,
            Custom4 = entity.Custom4,
            Custom5 = entity.Custom5
        };

        StartLocalStr = entity.StartLocal.ToString("yyyy-MM-ddTHH:mm");
        StopLocalStr = entity.StopLocal.ToString("yyyy-MM-ddTHH:mm");
        Total = entity.TotalAmount;

        // Map legs
        foreach (var l in entity.Legs.Where(x => x.Direction == LegDir.From))
            From.Add(new LegVm
            {
                Label = l.IsSea ? "SEA" : l.Tank!.Code,
                TankId = l.IsSea ? null : l.TankId,
                IsSea = l.IsSea,
                Current = l.IsSea ? 0 : l.VolumeBefore,
                Max = l.IsSea ? 0 : l.Tank!.MaxCapacity,
                Delta = l.Delta
            });
        foreach (var l in entity.Legs.Where(x => x.Direction == LegDir.To))
            To.Add(new LegVm
            {
                Label = l.IsSea ? "SEA" : l.Tank!.Code,
                TankId = l.IsSea ? null : l.TankId,
                IsSea = l.IsSea,
                Current = l.IsSea ? 0 : l.VolumeBefore,
                Max = l.IsSea ? 0 : l.Tank!.MaxCapacity,
                Delta = l.Delta
            });

        return Page();
    }

    // Helpers used by Add/Remove/Rebalance/Save (same as Create)

    private static HashSet<Guid> TankIds(IEnumerable<LegVm> legs)
        => legs.Where(l => !l.IsSea && l.TankId.HasValue).Select(l => l.TankId!.Value).ToHashSet();

    public async Task<IActionResult> OnPostAddLeg(string side)
    {
        await LoadLookupsAsync();
        ModelState.Clear();
        Normalize();

        var choice = side == "from" ? Request.Form["targetFrom"].FirstOrDefault()
                                    : Request.Form["targetTo"].FirstOrDefault();
        if (string.IsNullOrEmpty(choice)) return Page();

        if (choice == "SEA")
        {
            var leg = new LegVm { Label = "SEA", IsSea = true };
            if (side == "from") From.Add(leg); else To.Add(leg);
            return Page();
        }

        if (Guid.TryParse(choice, out var id))
        {
            var exists = side == "from" ? TankIds(From) : TankIds(To);
            if (exists.Contains(id)) return Page();

            var t = TankChoices.FirstOrDefault(x => x.Id == id);
            if (t != null)
            {
                var leg = new LegVm { Label = t.Code, TankId = t.Id, Current = t.CurrentCapacity, Max = t.MaxCapacity };
                if (side == "from") From.Add(leg); else To.Add(leg);
            }
        }
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveLeg(string side, int index)
    {
        await LoadLookupsAsync();
        ModelState.Clear();
        Normalize();

        if (side == "from" && index >= 0 && index < From.Count) From.RemoveAt(index);
        if (side == "to" && index >= 0 && index < To.Count) To.RemoveAt(index);
        return Page();
    }

    public async Task<IActionResult> OnPostRebalance(string? which, int index = -1)
    {
        await LoadLookupsAsync();
        ModelState.Clear();
        Normalize();

        int sumFrom = From.Sum(l => l.Delta);
        int sumTo = To.Sum(l => l.Delta);

        static void Distribute(int total, List<LegVm> legs)
        {
            int n = legs.Count; if (n == 0) return;
            int q = total / n, r = total % n;
            for (int i = 0; i < n; i++) legs[i].Delta = q + (i < r ? 1 : 0);
        }

        if (which == "total")
        {
            Distribute(Total, From);
            Distribute(Total, To);
        }
        else if (which == "from" && index >= 0 && index < From.Count)
        {
            sumFrom = From.Sum(l => l.Delta);
            Distribute(sumFrom, To);
            Total = sumFrom;
        }
        else if (which == "to" && index >= 0 && index < To.Count)
        {
            sumTo = To.Sum(l => l.Delta);
            Distribute(sumTo, From);
            Total = sumTo;
        }
        else
        {
            var sum = Math.Max(sumFrom, sumTo);
            Distribute(sum, From);
            Distribute(sum, To);
            Total = sum;
        }

        // clamp preview to capacities
        for (int i = 0; i < From.Count; i++)
            if (!From[i].IsSea && From[i].TankId.HasValue)
                From[i].Delta = Math.Min(From[i].Delta, From[i].Current);
        for (int i = 0; i < To.Count; i++)
            if (!To[i].IsSea && To[i].TankId.HasValue)
                To[i].Delta = Math.Min(To[i].Delta, To[i].Max - To[i].Current);

        return Page();
    }

    public async Task<IActionResult> OnPostSave()
    {
        await LoadLookupsAsync();
        ModelState.Clear();
        Normalize();

        if (!TryParseLocal(StartLocalStr, out var start))
            ModelState.AddModelError(string.Empty, "Start time is invalid.");
        if (!TryParseLocal(StopLocalStr, out var stop))
            ModelState.AddModelError(string.Empty, "Stop time is invalid.");
        if (ModelState.ErrorCount == 0 && stop <= start)
            ModelState.AddModelError(string.Empty, "Stop time must be after start time.");

        // BWTS default
        Op.BwtsUsed = Op.Type switch
        {
            OpType.B => true,
            OpType.DB => true,
            OpType.TR => false,
            _ => Op.BwtsUsed
        };

        // Auto SEA for B/DB if missing
        if (Op.Type == OpType.B && !From.Any())
            From.Add(new LegVm { Label = "SEA", IsSea = true, Delta = Math.Max(Total, To.Sum(l => l.Delta)) });
        if (Op.Type == OpType.DB && !To.Any())
            To.Add(new LegVm { Label = "SEA", IsSea = true, Delta = Math.Max(Total, From.Sum(l => l.Delta)) });

        // Rules
        if (Op.Type == OpType.B && From.Any(l => !l.IsSea))
            ModelState.AddModelError(string.Empty, "For BALLAST, FROM must be SEA only.");
        if (Op.Type == OpType.DB && To.Any(l => !l.IsSea))
            ModelState.AddModelError(string.Empty, "For DEBALLAST, TO must be SEA only.");
        if (Op.Type == OpType.TR && (From.Any(l => l.IsSea) || To.Any(l => l.IsSea)))
            ModelState.AddModelError(string.Empty, "For INTERNAL TRANSFER, SEA is not allowed.");

        Total = Math.Max(Total, Math.Max(From.Sum(l => l.Delta), To.Sum(l => l.Delta)));
        if (Total == 0) ModelState.AddModelError(string.Empty, "Total amount must be > 0.");

        if (!ModelState.IsValid) return Page();

        var entity = await _db.Operations
            .Include(o => o.Legs)
            .FirstOrDefaultAsync(o => o.Id == Id);
        if (entity == null) return RedirectToPage("Index");

        // Update fields
        entity.StartLocal = start;
        entity.StopLocal = stop;
        entity.TzOffset = Op.TzOffset;
        entity.LocationStart = Op.LocationStart;
        entity.LocationStop = Op.LocationStop;
        entity.Type = Op.Type;
        entity.BwtsUsed = Op.BwtsUsed;
        entity.Remark = Op.Remark;
        entity.Custom1 = Op.Custom1; entity.Custom2 = Op.Custom2; entity.Custom3 = Op.Custom3;
        entity.Custom4 = Op.Custom4; entity.Custom5 = Op.Custom5;
        entity.TotalAmount = Total;
        entity.UpdatedUtc = DateTime.UtcNow;

        // Replace legs
        _db.OperationLegs.RemoveRange(entity.Legs);
        entity.Legs.Clear();
        foreach (var f in From)
            entity.Legs.Add(new OperationLeg
            {
                IsSea = f.IsSea,
                TankId = f.IsSea ? null : f.TankId,
                Direction = LegDir.From,
                Delta = Math.Max(0, f.Delta)
            });
        foreach (var t in To)
            entity.Legs.Add(new OperationLeg
            {
                IsSea = t.IsSea,
                TankId = t.IsSea ? null : t.TankId,
                Direction = LegDir.To,
                Delta = Math.Max(0, t.Delta)
            });

        await _db.SaveChangesAsync();
        await _recalc.RecalculateAllAsync();
        return RedirectToPage("Index");
    }
}