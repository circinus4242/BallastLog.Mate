using System.Globalization;
using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using BallastLog.Mate.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        public int Current { get; set; }
        public int Max { get; set; }
        public int Delta { get; set; }
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty] public string StartLocalStr { get; set; } = "";
    [BindProperty] public string StopLocalStr { get; set; } = "";

    [BindProperty] public Operation Op { get; set; } = new();
    [BindProperty] public List<LegVm> From { get; set; } = new();
    [BindProperty] public List<LegVm> To { get; set; } = new();
    [BindProperty] public int Total { get; set; }

    public int MaxFlowRate { get; set; }
    public List<Tank> TankChoices { get; set; } = new();

    public string C1Label { get; set; } = "";
    public string C2Label { get; set; } = "";
    public string C3Label { get; set; } = "";
    public string C4Label { get; set; } = "";
    public string C5Label { get; set; } = "";

    private async Task LoadLookupsAsync()
    {
        var prof = await _db.ShipProfiles.FirstAsync(p => p.Id == 1);
        MaxFlowRate = prof.MaxFlowRate;
        C1Label = string.IsNullOrWhiteSpace(prof.Custom1Label) ? "" : prof.Custom1Label!;
        C2Label = string.IsNullOrWhiteSpace(prof.Custom2Label) ? "" : prof.Custom2Label!;
        C3Label = string.IsNullOrWhiteSpace(prof.Custom3Label) ? "" : prof.Custom3Label!;
        C4Label = string.IsNullOrWhiteSpace(prof.Custom4Label) ? "" : prof.Custom4Label!;
        C5Label = string.IsNullOrWhiteSpace(prof.Custom5Label) ? "" : prof.Custom5Label!;
        TankChoices = await _db.Tanks.OrderBy(t => t.Order).ThenBy(t => t.Code).ToListAsync();
    }

    private void Normalize()
    {
        From = (From ?? new()).Where(l => l != null && (l.IsSea || l.TankId.HasValue)).ToList();
        To = (To ?? new()).Where(l => l != null && (l.IsSea || l.TankId.HasValue)).ToList();
        foreach (var l in From) l.Delta = Math.Max(0, l.Delta);
        foreach (var l in To) l.Delta = Math.Max(0, l.Delta);
        Total = Math.Max(0, Total);
    }

    private static bool TryParseLocal(string s, out DateTime dt)
        => DateTime.TryParseExact(s, "yyyy-MM-ddTHH:mm",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);

    private static HashSet<Guid> TankIds(IEnumerable<LegVm> legs)
        => legs.Where(l => !l.IsSea && l.TankId.HasValue).Select(l => l.TankId!.Value).ToHashSet();

    // reuse the distribution helpers from Create
    private static int FromCap(LegVm l) => l.IsSea ? int.MaxValue : l.Current;
    private static int ToCap(LegVm l) => l.IsSea ? int.MaxValue : (l.Max - l.Current);
    private static int DistributeRemainderKeepTotal(int desiredTotal, List<LegVm> legs, int lockIndex, bool sideFrom)
    {
        if (legs.Count == 0) return 0;
        Func<LegVm, int> capFn = sideFrom ? new Func<LegVm, int>(FromCap) : new Func<LegVm, int>(ToCap);
        if (lockIndex >= 0 && lockIndex < legs.Count)
            legs[lockIndex].Delta = Math.Min(Math.Max(0, legs[lockIndex].Delta), capFn(legs[lockIndex]));
        int locked = (lockIndex >= 0 && lockIndex < legs.Count) ? legs[lockIndex].Delta : 0;
        int remainder = Math.Max(0, desiredTotal - locked);
        var others = legs.Select((l, i) => new { l, i }).Where(x => x.i != lockIndex).ToList();
        foreach (var x in others) x.l.Delta = 0;
        if (others.Count == 0) return locked;
        int n = others.Count; int q = remainder / n; int r = remainder % n; int assigned = 0;
        for (int k = 0; k < n; k++)
        {
            var x = others[k]; int want = q + (k < r ? 1 : 0);
            int cap = capFn(x.l); int add = Math.Min(want, Math.Max(0, cap));
            x.l.Delta = add; assigned += add;
        }
        for (int pass = 0; assigned < remainder && pass < 2; pass++)
            for (int k = 0; k < n && assigned < remainder; k++)
            {
                var x = others[k]; int cap = capFn(x.l);
                if (x.l.Delta < cap) { x.l.Delta++; assigned++; }
            }
        return locked + assigned;
    }
    private static int DistributeEqual(int desiredTotal, List<LegVm> legs, bool sideFrom)
    {
        Func<LegVm, int> capFn = sideFrom ? new Func<LegVm, int>(FromCap) : new Func<LegVm, int>(ToCap);
        int n = legs.Count; if (n == 0) return 0;
        int q = desiredTotal / n, r = desiredTotal % n;
        for (int i = 0; i < n; i++) legs[i].Delta = Math.Min(capFn(legs[i]), q + (i < r ? 1 : 0));
        int achieved = legs.Sum(l => l.Delta);
        for (int i = 0; i < n && achieved < desiredTotal; i++)
        {
            int cap = capFn(legs[i]); int room = cap - legs[i].Delta;
            int add = Math.Min(room, desiredTotal - achieved);
            if (add > 0) { legs[i].Delta += add; achieved += add; }
        }
        return achieved;
    }

    public async Task<IActionResult> OnGet()
    {
        await LoadLookupsAsync();

        var entity = await _db.Operations
            .Include(o => o.Legs).ThenInclude(l => l.Tank)
            .FirstOrDefaultAsync(o => o.Id == Id);
        if (entity == null) return RedirectToPage("Index");

        Op = new Operation
        {
            Id = entity.Id,
            Type = entity.Type,
            BwtsUsed = entity.BwtsUsed,
            TzOffset = entity.TzOffset,
            LocationStart = entity.LocationStart,
            LocationStop = entity.LocationStop,
            Remark = entity.Remark,
            MinDepth = entity.MinDepth,
            DistanceNearestLand = entity.DistanceNearestLand,
            Custom1 = entity.Custom1,
            Custom2 = entity.Custom2,
            Custom3 = entity.Custom3,
            Custom4 = entity.Custom4,
            Custom5 = entity.Custom5
        };

        StartLocalStr = entity.StartLocal.ToString("yyyy-MM-ddTHH:mm");
        StopLocalStr = entity.StopLocal.ToString("yyyy-MM-ddTHH:mm");
        Total = entity.TotalAmount;

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
            var existsAny = TankIds(From); existsAny.UnionWith(TankIds(To));
            if (existsAny.Contains(id)) return Page();

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

        if (which == "from" && index >= 0 && index < From.Count)
        {
            int desired = Total;
            int got = DistributeRemainderKeepTotal(desired, From, index, sideFrom: true);
            Total = got;
            if (To.Count > 0) DistributeEqual(Total, To, sideFrom: false);
            return Page();
        }
        if (which == "to" && index >= 0 && index < To.Count)
        {
            int desired = Total;
            int got = DistributeRemainderKeepTotal(desired, To, index, sideFrom: false);
            Total = got;
            if (From.Count > 0) DistributeEqual(Total, From, sideFrom: true);
            return Page();
        }
        if (which == "total")
        {
            if (From.Count > 0) DistributeEqual(Total, From, sideFrom: true);
            if (To.Count > 0) DistributeEqual(Total, To, sideFrom: false);
            return Page();
        }

        int sum = Math.Max(From.Sum(l => l.Delta), To.Sum(l => l.Delta));
        Total = sum;
        if (From.Count > 0) DistributeEqual(Total, From, sideFrom: true);
        if (To.Count > 0) DistributeEqual(Total, To, sideFrom: false);
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

        // default BWTS by type (user may override)
        Op.BwtsUsed = Op.Type switch
        {
            OpType.B => true,
            OpType.DB => true,
            OpType.TR => false,
            _ => Op.BwtsUsed
        };

        if (Op.Type == OpType.B && From.Any(l => !l.IsSea))
            ModelState.AddModelError(string.Empty, "For BALLAST, FROM must be SEA only.");
        if (Op.Type == OpType.DB && To.Any(l => !l.IsSea))
            ModelState.AddModelError(string.Empty, "For DEBALLAST, TO must be SEA only.");
        if (Op.Type == OpType.TR && (From.Any(l => l.IsSea) || To.Any(l => l.IsSea)))
            ModelState.AddModelError(string.Empty, "For INTERNAL TRANSFER, SEA is not allowed.");

        var fromIds = TankIds(From);
        var toIds = TankIds(To);
        fromIds.IntersectWith(toIds);
        if (fromIds.Count > 0)
            ModelState.AddModelError(string.Empty, "The same tank cannot be in FROM and TO.");

        Total = Math.Max(Total, Math.Max(From.Sum(l => l.Delta), To.Sum(l => l.Delta)));
        if (Total == 0) ModelState.AddModelError(string.Empty, "Total amount must be > 0.");

        if (!ModelState.IsValid) return Page();

        var entity = await _db.Operations.Include(o => o.Legs).FirstOrDefaultAsync(o => o.Id == Id);
        if (entity == null) return RedirectToPage("Index");

        entity.StartLocal = start;
        entity.StopLocal = stop;
        entity.TzOffset = Op.TzOffset;
        entity.LocationStart = Op.LocationStart;
        entity.LocationStop = Op.LocationStop;
        entity.Type = Op.Type;
        entity.BwtsUsed = Op.BwtsUsed;
        entity.Remark = Op.Remark;
        entity.MinDepth = Op.MinDepth;
        entity.DistanceNearestLand = Op.DistanceNearestLand;
        entity.Custom1 = Op.Custom1; entity.Custom2 = Op.Custom2; entity.Custom3 = Op.Custom3;
        entity.Custom4 = Op.Custom4; entity.Custom5 = Op.Custom5;
        entity.TotalAmount = Total;
        entity.UpdatedUtc = DateTime.UtcNow;

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
    public List<SelectListItem> FromOptions { get; set; } = new();
    public List<SelectListItem> ToOptions { get; set; } = new();

    private void BuildOptions()
    {
        var excluded = TankIds(From);
        excluded.UnionWith(TankIds(To));

        FromOptions = new() { new SelectListItem("SEA", "SEA") };
        FromOptions.AddRange(
            TankChoices.Where(t => !excluded.Contains(t.Id))
                       .Select(t => new SelectListItem($"{t.Code} ({t.CurrentCapacity}/{t.MaxCapacity})", t.Id.ToString()))
        );

        ToOptions = new() { new SelectListItem("SEA", "SEA") };
        ToOptions.AddRange(
            TankChoices.Where(t => !excluded.Contains(t.Id))
                       .Select(t => new SelectListItem($"{t.Code} ({t.CurrentCapacity}/{t.MaxCapacity})", t.Id.ToString()))
        );
    }

}
