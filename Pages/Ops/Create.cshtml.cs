using System.ComponentModel.DataAnnotations;
using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using BallastLog.Mate.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BallastLog.Mate.Pages.Ops;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly RecalcService _recalc;

    public CreateModel(AppDbContext db, RecalcService recalc)
    { _db = db; _recalc = recalc; }

    public class LegVm
    {
        public string Label { get; set; } = "";
        public Guid? TankId { get; set; }
        public bool IsSea { get; set; }
        public int Current { get; set; }
        public int Max { get; set; }
        public int Delta { get; set; }
    }

    [BindProperty] public Operation Op { get; set; } = new();
    [BindProperty] public List<LegVm> From { get; set; } = new();
    [BindProperty] public List<LegVm> To { get; set; } = new();
    [BindProperty] public int Total { get; set; }

    public int MaxFlowRate { get; set; }
    public List<Tank> TankChoices { get; set; } = new();

    // Custom labels from ShipProfile
    public string C1Label { get; set; } = "Custom 1";
    public string C2Label { get; set; } = "Custom 2";
    public string C3Label { get; set; } = "Custom 3";
    public string C4Label { get; set; } = "Custom 4";
    public string C5Label { get; set; } = "Custom 5";

    public async Task OnGet()
    {
        await LoadLookupsAsync();

        // Defaults
        var now = DateTime.Now;
        now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

        Op.StartLocal = now.AddMinutes(-5);
        Op.StopLocal = now;
        Op.TzOffset = "+00:00";

        Op.Type = OpType.B;
        Op.BwtsUsed = true;
        // Lists start empty; user adds legs
    }

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

    // Add a tank/SEA to FROM or TO.
    // NOTE: page has two selects named targetFrom / targetTo to avoid collisions.
    public async Task<IActionResult> OnPostAddLeg(string side)
    {
        await LoadLookupsAsync();
        Normalize();
        // bring forward all current deltas as non-negative ints
        foreach (var l in From) l.Delta = Math.Max(0, l.Delta);
        foreach (var l in To) l.Delta = Math.Max(0, l.Delta);
        Total = Math.Max(0, Total);

        var choice = side == "from" ? Request.Form["targetFrom"].FirstOrDefault()
                                    : Request.Form["targetTo"].FirstOrDefault();

        if (string.IsNullOrEmpty(choice))
            return Page();

        if (choice == "SEA")
        {
            if (side == "from") From.Add(new LegVm { Label = "SEA", IsSea = true });
            else To.Add(new LegVm { Label = "SEA", IsSea = true });
            return Page();
        }

        if (Guid.TryParse(choice, out var id))
        {
            var t = TankChoices.FirstOrDefault(x => x.Id == id);
            if (t != null)
            {
                var leg = new LegVm { Label = t.Code, TankId = t.Id, Current = t.CurrentCapacity, Max = t.MaxCapacity };
                if (side == "from") From.Add(leg); else To.Add(leg);
            }
        }
        return Page();
    }

    // Per-row OK button or Total OK
    public async Task<IActionResult> OnPostRebalance(string? which, int index = -1)
    {
        await LoadLookupsAsync();
        Normalize();
        foreach (var l in From) l.Delta = Math.Max(0, l.Delta);
        foreach (var l in To) l.Delta = Math.Max(0, l.Delta);
        Total = Math.Max(0, Total);

        int sumFrom = From.Sum(l => l.Delta);
        int sumTo = To.Sum(l => l.Delta);

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

        // respect capacities on preview
        for (int i = 0; i < From.Count; i++)
            if (!From[i].IsSea && From[i].TankId.HasValue)
                From[i].Delta = Math.Min(From[i].Delta, From[i].Current);

        for (int i = 0; i < To.Count; i++)
            if (!To[i].IsSea && To[i].TankId.HasValue)
                To[i].Delta = Math.Min(To[i].Delta, To[i].Max - To[i].Current);

        return Page();

        static void Distribute(int total, List<LegVm> legs)
        {
            int n = legs.Count;
            if (n == 0) return;
            int q = total / n, r = total % n;
            for (int i = 0; i < n; i++) legs[i].Delta = q + (i < r ? 1 : 0);
        }
    }

    public async Task<IActionResult> OnPostSave()
    {
        await LoadLookupsAsync();
        Normalize();
        if (Op.StopLocal <= Op.StartLocal)
            ModelState.AddModelError(string.Empty, "Stop time must be after start time.");

        // Apply BWTS default
        Op.BwtsUsed = Op.Type switch
        {
            OpType.B => true,
            OpType.DB => true,
            OpType.TR => false,
            _ => Op.BwtsUsed
        };

        // Auto-insert SEA if missing for B/DB
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

        // compute totals
        Total = Math.Max(Total, Math.Max(From.Sum(l => l.Delta), To.Sum(l => l.Delta)));
        if (Total == 0)
            ModelState.AddModelError(string.Empty, "Total amount must be > 0.");

        if (!ModelState.IsValid) return Page();

        var entity = new Operation
        {
            StartLocal = Op.StartLocal,
            StopLocal = Op.StopLocal,
            TzOffset = Op.TzOffset,
            LocationStart = Op.LocationStart,
            LocationStop = Op.LocationStop,
            Type = Op.Type,
            BwtsUsed = Op.BwtsUsed,
            Remark = Op.Remark,
            Custom1 = Op.Custom1,
            Custom2 = Op.Custom2,
            Custom3 = Op.Custom3,
            Custom4 = Op.Custom4,
            Custom5 = Op.Custom5,
            TotalAmount = Total,
            UpdatedUtc = DateTime.UtcNow
        };

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

        _db.Operations.Add(entity);
        await _db.SaveChangesAsync();
        await _recalc.RecalculateAllAsync();

        return RedirectToPage("Index");
    }
    private void Normalize()
    {
        // keep only SEA or valid tank legs; clamp deltas
        From = (From ?? new()).Where(l => l.IsSea || l.TankId.HasValue).ToList();
        To = (To ?? new()).Where(l => l.IsSea || l.TankId.HasValue).ToList();
        foreach (var l in From) l.Delta = Math.Max(0, l.Delta);
        foreach (var l in To) l.Delta = Math.Max(0, l.Delta);
        Total = Math.Max(0, Total);
    }
}
