using System.Globalization;
using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using BallastLog.Mate.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        public double Current { get; set; }
        public double Max { get; set; }
        public double Delta { get; set; }
    }

    // bind datetime-local as strings, parse manually
    [BindProperty] public string StartLocalStr { get; set; } = "";
    [BindProperty] public string StopLocalStr { get; set; } = "";

    [BindProperty] public Operation Op { get; set; } = new();
    [BindProperty] public List<LegVm> From { get; set; } = new();
    [BindProperty] public List<LegVm> To { get; set; } = new();
    [BindProperty] public double Total { get; set; }

    public int MaxFlowRate { get; set; }
    public List<Tank> TankChoices { get; set; } = new();

    // labels from Setup
    public string C1Label { get; set; } = "Custom 1";
    public string C2Label { get; set; } = "Custom 2";
    public string C3Label { get; set; } = "Custom 3";
    public string C4Label { get; set; } = "Custom 4";
    public string C5Label { get; set; } = "Custom 5";

    // live flow preview
    public string FlowText { get; set; } = "-";
    public bool FlowTooHigh { get; set; }

    public async Task OnGet()
    {
        await LoadLookupsAsync();

        var now = DateTime.Now;
        now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        StartLocalStr = now.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm");
        StopLocalStr = now.ToString("yyyy-MM-ddTHH:mm");

        Op.Type = OpType.B;
        Op.BwtsUsed = true;         // default for BALLAST/DEBALLAST
        Op.TzOffset = "+02:00";

        UpdateFlow();
    }

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
        // remove null placeholders and empty rows; clamp deltas
        From = (From ?? new()).Where(l => l != null && (l.IsSea || l.TankId.HasValue)).ToList();
        To = (To ?? new()).Where(l => l != null && (l.IsSea || l.TankId.HasValue)).ToList();
        foreach (var l in From) l.Delta = Math.Max(0, l.Delta);
        foreach (var l in To) l.Delta = Math.Max(0, l.Delta);
        Total = Math.Max(0, Total);
        UpdateFlow();
    }

    private void UpdateFlow()
    {
        if (TryParseLocal(StartLocalStr, out var s) &&
            TryParseLocal(StopLocalStr, out var e) && e > s && Total > 0)
        {
            var hours = (e - s).TotalHours;
            if (hours <= 0) { FlowText = "-"; FlowTooHigh = false; return; }
            var flow = Total / hours;
            FlowTooHigh = MaxFlowRate > 0 && flow > MaxFlowRate;
            FlowText = $"{flow:0.##} m3/h";
        }
        else
        {
            FlowText = "-";
            FlowTooHigh = false;
        }
    }

    private static bool TryParseLocal(string s, out DateTime dt)
        => DateTime.TryParseExact(s, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture,
                                  DateTimeStyles.None, out dt);

    private static HashSet<Guid> TankIds(IEnumerable<LegVm> legs)
        => legs.Where(l => !l.IsSea && l.TankId.HasValue).Select(l => l.TankId!.Value).ToHashSet();

    // capacities
    private static double FromCap(LegVm l) => l.IsSea ? int.MaxValue : l.Current;
    private static double ToCap(LegVm l) => l.IsSea ? int.MaxValue : (l.Max - l.Current);

    // keep TOTAL; adjust other legs on same side; mirror to opposite side
    private static double DistributeRemainderKeepTotal(double desiredTotal, List<LegVm> legs, int lockIndex, bool sideFrom)
    {
        if (legs.Count == 0) return 0;

        Func<LegVm, double> capFn = sideFrom ? new Func<LegVm, double>(FromCap)
                                         : new Func<LegVm, double>(ToCap);

        if (lockIndex >= 0 && lockIndex < legs.Count)
            legs[lockIndex].Delta = Math.Min(Math.Max(0, legs[lockIndex].Delta), capFn(legs[lockIndex]));

        double locked = (lockIndex >= 0 && lockIndex < legs.Count) ? legs[lockIndex].Delta : 0;
        double remainder = Math.Max(0, desiredTotal - locked);

        var others = legs.Select((l, i) => new { l, i }).Where(x => x.i != lockIndex).ToList();
        foreach (var x in others) x.l.Delta = 0;

        if (others.Count == 0) return locked;

        int n = others.Count;
        double q = remainder / n;
        double r = remainder % n;
        double assigned = 0;

        for (int k = 0; k < n; k++)
        {
            var x = others[k];
            double want = q + (k < r ? 1 : 0);
            double cap = capFn(x.l);
            double add = Math.Min(want, Math.Max(0, cap));
            x.l.Delta = add;
            assigned += add;
        }

        for (int pass = 0; assigned < remainder && pass < 2; pass++)
        {
            for (int k = 0; k < n && assigned < remainder; k++)
            {
                var x = others[k];
                double cap = capFn(x.l);
                if (x.l.Delta < cap)
                {
                    x.l.Delta++;
                    assigned++;
                }
            }
        }

        return locked + assigned;
    }

    // equal distribution across all legs
    private static double DistributeEqual(double desiredTotal, List<LegVm> legs, bool sideFrom)
    {
        Func<LegVm, double> capFn = sideFrom ? new Func<LegVm, double>(FromCap)
                                         : new Func<LegVm, double>(ToCap);

        int n = legs.Count;
        if (n == 0) return 0;

        double q = desiredTotal / n, r = desiredTotal % n;
        for (int i = 0; i < n; i++)
            legs[i].Delta = Math.Min(capFn(legs[i]), q + (i < r ? 1 : 0));

        double achieved = legs.Sum(l => l.Delta);
        for (int i = 0; i < n && achieved < desiredTotal; i++)
        {
            double cap = capFn(legs[i]);
            double room = cap - legs[i].Delta;
            double add = Math.Min(room, desiredTotal - achieved);
            if (add > 0) { legs[i].Delta += add; achieved += add; }
        }
        return achieved;
    }

    private void KeepTotalAndPropagateFrom(int index)
    {
        double desired = Total;
        double got = DistributeRemainderKeepTotal(desired, From, index, sideFrom: true);
        Total = got;
        if (To.Count > 0) DistributeEqual(Total, To, sideFrom: false);
        UpdateFlow();
    }
    private void KeepTotalAndPropagateTo(int index)
    {
        double desired = Total;
        double got = DistributeRemainderKeepTotal(desired, To, index, sideFrom: false);
        Total = got;
        if (From.Count > 0) DistributeEqual(Total, From, sideFrom: true);
        UpdateFlow();
    }

    public async Task<IActionResult> OnPostAddLeg(string side)
    {
        await LoadLookupsAsync();
        ModelState.Clear();
        Normalize();

        var choice = side == "from" ? Request.Form["targetFrom"].FirstOrDefault()
                                    : Request.Form["targetTo"].FirstOrDefault();
        if (string.IsNullOrEmpty(choice)) { UpdateFlow(); return Page(); }

        if (choice == "SEA")
        {
            var leg = new LegVm { Label = "SEA", IsSea = true };
            if (side == "from") From.Add(leg); else To.Add(leg);
            UpdateFlow();
            return Page();
        }

        if (Guid.TryParse(choice, out var id))
        {
            // prevent duplicates across BOTH sides
            var existsAny = TankIds(From);
            existsAny.UnionWith(TankIds(To));
            if (existsAny.Contains(id)) { UpdateFlow(); return Page(); }

            var t = TankChoices.FirstOrDefault(x => x.Id == id);
            if (t != null)
            {
                var leg = new LegVm { Label = t.Code, TankId = t.Id, Current = t.CurrentCapacity, Max = t.MaxCapacity };
                if (side == "from") From.Add(leg); else To.Add(leg);
            }
        }
        UpdateFlow();
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveLeg(string side, int index)
    {
        await LoadLookupsAsync();
        ModelState.Clear();
        Normalize();

        if (side == "from" && index >= 0 && index < From.Count) From.RemoveAt(index);
        if (side == "to" && index >= 0 && index < To.Count) To.RemoveAt(index);

        UpdateFlow();
        return Page();
    }

    public async Task<IActionResult> OnPostRebalance(string? which, int index = -1)
    {
        await LoadLookupsAsync();
        ModelState.Clear();
        Normalize();

        if (which == "from" && index >= 0 && index < From.Count)
        {
            KeepTotalAndPropagateFrom(index);
            return Page();
        }
        if (which == "to" && index >= 0 && index < To.Count)
        {
            KeepTotalAndPropagateTo(index);
            return Page();
        }
        if (which == "total")
        {
            if (From.Count > 0) DistributeEqual(Total, From, sideFrom: true);
            if (To.Count > 0) DistributeEqual(Total, To, sideFrom: false);
            UpdateFlow();
            return Page();
        }

        double sum = Math.Max(From.Sum(l => l.Delta), To.Sum(l => l.Delta));
        Total = sum;
        if (From.Count > 0) DistributeEqual(Total, From, sideFrom: true);
        if (To.Count > 0) DistributeEqual(Total, To, sideFrom: false);
        UpdateFlow();
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

        // Default BWTS by type (user can override via checkbox)
        Op.BwtsUsed = Op.Type switch
        {
            OpType.B => true,
            OpType.DB => true,
            OpType.TR => false,
            _ => Op.BwtsUsed
        };

        // auto SEA if missing on B/DB
        if (Op.Type == OpType.B && !From.Any()) From.Add(new LegVm { Label = "SEA", IsSea = true, Delta = Math.Max(Total, To.Sum(l => l.Delta)) });
        if (Op.Type == OpType.DB && !To.Any()) To.Add(new LegVm { Label = "SEA", IsSea = true, Delta = Math.Max(Total, From.Sum(l => l.Delta)) });

        // rules
        if (Op.Type == OpType.B && From.Any(l => !l.IsSea))
            ModelState.AddModelError(string.Empty, "For BALLAST, FROM must be SEA only.");
        if (Op.Type == OpType.DB && To.Any(l => !l.IsSea))
            ModelState.AddModelError(string.Empty, "For DEBALLAST, TO must be SEA only.");
        if (Op.Type == OpType.TR && (From.Any(l => l.IsSea) || To.Any(l => l.IsSea)))
            ModelState.AddModelError(string.Empty, "For INTERNAL TRANSFER, SEA is not allowed.");

        // duplicates across sides
        var fromIds = TankIds(From);
        var toIds = TankIds(To);
        fromIds.IntersectWith(toIds);
        if (fromIds.Count > 0)
            ModelState.AddModelError(string.Empty, "The same tank cannot be in FROM and TO.");

        Total = Math.Max(Total, Math.Max(From.Sum(l => l.Delta), To.Sum(l => l.Delta)));
        if (Total == 0) ModelState.AddModelError(string.Empty, "Total amount must be > 0.");

        if (!ModelState.IsValid) { UpdateFlow(); return Page(); }

        var entity = new Operation
        {
            StartLocal = start,
            StopLocal = stop,
            TzOffset = Op.TzOffset,
            LocationStart = Op.LocationStart,
            LocationStop = Op.LocationStop,
            Type = Op.Type,
            BwtsUsed = Op.BwtsUsed,
            Remark = Op.Remark,
            // new optional fields (nullable ints in Operation)
            MinDepth = Op.MinDepth,
            DistanceNearestLand = Op.DistanceNearestLand,
            // keep custom fields if labels used
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
