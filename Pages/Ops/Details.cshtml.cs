using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace BallastLog.Mate.Pages.Ops;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    public DetailsModel(AppDbContext db) { _db = db; }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Operation Op { get; set; } = null!;
    public ShipProfile Prof { get; set; } = null!;

    public List<Fm232Row> Fm232 { get; set; } = new();
    public List<OldLogRow> OldLog { get; set; } = new();
    public List<NewLogRow> NewLog { get; set; } = new();

    public class Fm232Row
    {
        public int TankOrder { get; set; }
        public string TankCode { get; set; } = "";
        public string DateStop { get; set; } = "";
        public string Location { get; set; } = "";
        public string TimeStart { get; set; } = "";
        public double Initial { get; set; }
        public string EstUptakeSea { get; set; } = "-";
        public string EstIntakeReception { get; set; } = "-";
        public string EstCirculated { get; set; } = "-";
        public string EstDischargedSea { get; set; } = "-";
        public string EstDischargedReception { get; set; } = "-";
        public double Final { get; set; }
        public string TimeCompleted { get; set; } = "";
        public string Method { get; set; } = "";
        public string SeaDepth { get; set; } = "";
        public string DistNearestLand { get; set; } = "";
        public string Oic { get; set; } = "CO";
        public string Remarks { get; set; } = "";
    }

    public class OldLogRow
    {
        public string Date { get; set; } = "";   // only on first line per op
        public string Item { get; set; } = "";
        public string Record { get; set; } = "";
    }

    public class NewLogRow
    {
        public string Date { get; set; } = "";   // only on first line per op
        public string Code { get; set; } = "";   // only on first line per op
        public string Item { get; set; } = "";   // number or empty
        public string Record { get; set; } = "";
    }

    public async Task<IActionResult> OnGet()
    {
        Op = await _db.Operations
            .Include(o => o.Legs).ThenInclude(l => l.Tank)
            .FirstAsync(o => o.Id == Id);

        Prof = await _db.ShipProfiles.FirstAsync(p => p.Id == 1);

        BuildFm232();
        BuildOldLog();
        BuildNewLog();

        return Page();
    }

    // mark-as-done buttons
    public async Task<IActionResult> OnPostMarkFm232(Guid id)
    {
        var op = await _db.Operations.FindAsync(id);
        if (op != null) { op.RecordedToFm232 = true; op.UpdatedUtc = DateTime.UtcNow; await _db.SaveChangesAsync(); }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostMarkLog(Guid id)
    {
        var op = await _db.Operations.FindAsync(id);
        if (op != null) { op.RecordedToLogBook = true; op.UpdatedUtc = DateTime.UtcNow; await _db.SaveChangesAsync(); }
        return RedirectToPage(new { id });
    }

    // ---------- FM-232 ----------
    private void BuildFm232()
    {
        // tanks in this operation (either side, not SEA)
        var legs = Op.Legs.Where(l => !l.IsSea && l.Tank != null).ToList();

        // build per tank rows (sorted by tank order)
        foreach (var g in legs.GroupBy(l => l.TankId).Select(gr => gr.ToList())
                              .OrderBy(list => list.First().Tank!.Order))
        {
            var any = g.First();
            var tank = any.Tank!;

            double initial = g.Any(x => x.Direction == LegDir.From) ? g.Where(x => x.Direction == LegDir.From).First().VolumeBefore
                                                                 : g.Where(x => x.Direction == LegDir.To).First().VolumeBefore;

            double final = g.Any(x => x.Direction == LegDir.To) ? g.Where(x => x.Direction == LegDir.To).Last().VolumeAfter
                                                             : g.Where(x => x.Direction == LegDir.From).Last().VolumeAfter;

            double deltaTo = g.Where(x => x.Direction == LegDir.To).Sum(x => x.Delta);
            double deltaFrom = g.Where(x => x.Direction == LegDir.From).Sum(x => x.Delta);

            var row = new Fm232Row
            {
                TankOrder = tank.Order,
                TankCode = tank.Code,
                DateStop = Op.StopLocal.ToString("dd'/'MM'/'yy"),
                Location = OneLocationText(Op),
                TimeStart = Op.StartLocal.ToString("HH':'mm"),
                Initial = initial,
                EstUptakeSea = Op.Type == OpType.B ? (deltaTo > 0 ? deltaTo.ToString() : "-") : "-",
                EstIntakeReception = "-",
                EstCirculated = (Op.BwtsUsed || Op.Type == OpType.TR) ? (deltaTo + deltaFrom > 0 ? (deltaTo + deltaFrom).ToString() : "-") : "-",
                EstDischargedSea = Op.Type == OpType.DB ? (deltaFrom > 0 ? deltaFrom.ToString() : "-") : "-",
                EstDischargedReception = "-",
                Final = final,
                TimeCompleted = Op.StopLocal.ToString("HH':'mm"),
                Method = MethodText(Op),
                SeaDepth = Op.MinDepth.HasValue ? Op.MinDepth.Value.ToString() : "",
                DistNearestLand = Op.DistanceNearestLand.HasValue ? Op.DistanceNearestLand.Value.ToString() : "",
                Oic = "CO",
                Remarks = BuildFm232Remark(tank.Code)
            };

            Fm232.Add(row);
        }
    }

    private string BuildFm232Remark(string tankCode)
    {
        if (Op.Type == OpType.TR)
        {
            bool isFrom = Op.Legs.Any(l => !l.IsSea && l.Tank != null && l.Tank!.Code == tankCode && l.Direction == LegDir.From);
            bool isTo = Op.Legs.Any(l => !l.IsSea && l.Tank != null && l.Tank!.Code == tankCode && l.Direction == LegDir.To);

            if (isFrom && !isTo) return "TR FM " + tankCode;
            if (!isFrom && isTo) return "TR TO " + tankCode;
            if (isFrom && isTo) return "TR FM & TO " + tankCode;
            return "TR";
        }

        // for B/DB show custom fields (only those with labels)
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Prof.Custom1Label) && !string.IsNullOrWhiteSpace(Op.Custom1)) parts.Add($"{Prof.Custom1Label}: {Op.Custom1}");
        if (!string.IsNullOrWhiteSpace(Prof.Custom2Label) && !string.IsNullOrWhiteSpace(Op.Custom2)) parts.Add($"{Prof.Custom2Label}: {Op.Custom2}");
        if (!string.IsNullOrWhiteSpace(Prof.Custom3Label) && !string.IsNullOrWhiteSpace(Op.Custom3)) parts.Add($"{Prof.Custom3Label}: {Op.Custom3}");
        if (!string.IsNullOrWhiteSpace(Prof.Custom4Label) && !string.IsNullOrWhiteSpace(Op.Custom4)) parts.Add($"{Prof.Custom4Label}: {Op.Custom4}");
        if (!string.IsNullOrWhiteSpace(Prof.Custom5Label) && !string.IsNullOrWhiteSpace(Op.Custom5)) parts.Add($"{Prof.Custom5Label}: {Op.Custom5}");
        return string.Join("\n", parts);
    }

    // ---------- Old Logbook ----------
    private void BuildOldLog()
    {
        string date = Op.StopLocal.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
        string codeBase = Op.Type switch { OpType.B => "3.1", OpType.DB => "3.3", OpType.TR => "3.2", _ => "3.X" };

        string l1;
        if (LooksLikeSea(Op.LocationStart) || LooksLikeSea(Op.LocationStop))
        {
            l1 = $"START: {Op.StartLocal:HH:mm}LT ({Op.LocationStart}); STOP: {Op.StopLocal:HH:mm}LT ({Op.LocationStop}).";
        }
        else
        {
            // in port
            var loc = SameOr(Op.LocationStart, Op.LocationStop);
            l1 = $"START: {Op.StartLocal:HH:mm}LT; STOP: {Op.StopLocal:HH:mm}LT. {loc}.";
        }

        // Header line (x.1)
        OldLog.Add(new OldLogRow { Date = date, Item = $"{codeBase}.1", Record = l1 });

        if (Op.Type == OpType.B || Op.Type == OpType.DB)
        {
            // x.2 tank line and metrics
            var tankLine = BuildOldBallastDeballastTankLine();
            OldLog.Add(new OldLogRow { Date = "", Item = $"{codeBase}.2", Record = tankLine });

            if (LooksLikeSea(Op.LocationStart) || LooksLikeSea(Op.LocationStop))
            {
                var env = $"MINIMUM DEPTH - {NumOrDash(Op.MinDepth)} m, DISTANCE TO NEAREST LAND - {NumOrDash(Op.DistanceNearestLand)} nm.";
                OldLog.Add(new OldLogRow { Date = "", Item = "", Record = env });
            }

            var initFinal = BuildInitialFinalPairs();
            OldLog.Add(new OldLogRow { Date = "", Item = "", Record = initFinal });

            // trailing lines
            if (Op.Type == OpType.DB)
            {
                OldLog.Add(new OldLogRow { Date = "", Item = $"{codeBase}.3", Record = "YES" });
                OldLog.Add(new OldLogRow { Date = "", Item = $"{codeBase}.4", Record = "CO" });
            }
            else
            {
                OldLog.Add(new OldLogRow { Date = "", Item = $"{codeBase}.3", Record = "CO" });
            }
        }
        else if (Op.Type == OpType.TR)
        {
            // x.2 block with 1..4 shapes
            foreach (var r in BuildTransferRows()) OldLog.Add(new OldLogRow { Date = "", Item = $"{codeBase}.2", Record = r });
            OldLog.Add(new OldLogRow { Date = "", Item = $"{codeBase}.3", Record = "YES" });
            OldLog.Add(new OldLogRow { Date = "", Item = $"{codeBase}.4", Record = "CO" });
        }
        else
        {
            // MISC minimal
            OldLog.Add(new OldLogRow { Date = "", Item = $"{codeBase}.2", Record = (Op.Remark ?? "").Trim() });
            OldLog.Add(new OldLogRow { Date = "", Item = $"{codeBase}.3", Record = "CO" });
        }
    }

    // ---------- New Logbook ----------
    private void BuildNewLog()
    {
        string date = Op.StopLocal.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
        string codeLetter = Op.Type switch { OpType.B => "A", OpType.DB => "B", OpType.TR => "H", _ => "F or H" };

        if (Op.Type == OpType.B || Op.Type == OpType.DB)
        {
            // 1-2 with UTC/SMT
            var (utcStart, utcStop) = ToUtc(Op);
            var (smtStart, smtStop) = (Op.StartLocal, Op.StopLocal);

            string at1 = LooksLikeSea(Op.LocationStart) ? $"({Op.LocationStart})" : Op.LocationStart;
            string at2 = LooksLikeSea(Op.LocationStop) ? $"({Op.LocationStop})" : Op.LocationStop;

            string maybeDate1 = utcStart.Date != smtStart.Date ? $" {smtStart:dd-MMM-yyyy}" : "";
            string maybeDate2 = utcStop.Date != smtStop.Date ? $" {smtStop:dd-MMM-yyyy}" : "";

            NewLog.Add(new NewLogRow
            {
                Date = date,
                Code = codeLetter,
                Item = "1",
                Record = $"START - {utcStart:HHmm} HRS (UTC) ({smtStart:HHmm} SMT{maybeDate1}) at {at1}"
            });
            NewLog.Add(new NewLogRow
            {
                Date = "",
                Code = "",
                Item = "2",
                Record = $"COMPLETION - {utcStop:HHmm} HRS (UTC) ({smtStop:HHmm} SMT{maybeDate2}) at {at2}"
            });

            // 3 tanks used
            var tanks = TanksUsedOneLine();
            NewLog.Add(new NewLogRow { Date = "", Code = "", Item = "3", Record = tanks });

            // 4 quantity and final retained
            double qty = Op.Type == OpType.B ? Op.Legs.Where(l => l.Direction == LegDir.To && !l.IsSea).Sum(l => l.Delta)
                                          : Op.Legs.Where(l => l.Direction == LegDir.From && !l.IsSea).Sum(l => l.Delta);
            double finalOnboard = FinalOnboardAfterThisOperation();
            string word = Op.Type == OpType.B ? "UPTAKE" : "DISCHARGED";
            NewLog.Add(new NewLogRow
            {
                Date = "",
                Code = "",
                Item = "4",
                Record = $"{word} {qty} m3. FINAL QUANTITY RETAINED: {finalOnboard} m3"
            });

            // 5-6 BWMS lines
            string line5 = Op.Type == OpType.B ? "YES. BALLASTING AS PER BWMP FOR D-2 COMPLIANCE THROUGH BWMS"
                                               : "YES. DEBALLASTING AS PER BWMP FOR D-2 COMPLIANCE THROUGH BWTS";
            NewLog.Add(new NewLogRow { Date = "", Code = "", Item = "5", Record = line5 });
            NewLog.Add(new NewLogRow { Date = "", Code = "", Item = "6", Record = "APPROVED BWMS" });

            if (!string.IsNullOrWhiteSpace(Op.Remark))
                NewLog.Add(new NewLogRow { Date = "", Code = "", Item = "6", Record = Op.Remark!.Trim() });

            NewLog.Add(new NewLogRow { Date = "", Code = "", Item = "", Record = "SIGN, NAME, RANK" });
        }
        else if (Op.Type == OpType.TR)
        {
            var line1 = LooksLikeSea(Op.LocationStart) || LooksLikeSea(Op.LocationStop)
                ? $"START: {Op.StartLocal:HH:mm}LT ({Op.LocationStart}); STOP: {Op.StopLocal:HH:mm}LT ({Op.LocationStop})."
                : $"START: {Op.StartLocal:HH:mm}LT; STOP: {Op.StopLocal:HH:mm}LT. {SameOr(Op.LocationStart, Op.LocationStop)}.";

            double qty = Op.Legs.Where(l => !l.IsSea).Sum(l => l.Delta);
            var fromNames = FullTankNames(Direction: LegDir.From);
            var toNames = FullTankNames(Direction: LegDir.To);

            NewLog.Add(new NewLogRow { Date = date, Code = "H", Item = "", Record = line1 });
            NewLog.Add(new NewLogRow { Date = "", Code = "", Item = "", Record = $"{qty} m3 OF BALLAST WATER TRANSFERRED FROM {fromNames} TO {toNames}" });
            NewLog.Add(new NewLogRow { Date = "", Code = "", Item = "", Record = "SIGN, NAME, RANK" });
        }
        else
        {
            NewLog.Add(new NewLogRow { Date = date, Code = "F or H", Item = "", Record = (Op.Remark ?? "").Trim() });
            NewLog.Add(new NewLogRow { Date = "", Code = "", Item = "", Record = "SIGN, NAME, RANK" });
        }
    }

    // -------- helpers for composing text --------
    private static bool LooksLikeSea(string s) => s.Any(char.IsDigit);
    private static string SameOr(string a, string b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase) ? a : $"{a} / {b}";
    private static string NumOrDash(int? v) => v.HasValue ? v.Value.ToString() : "-";
    private string OneLocationText(Operation op)
        => SameOr(op.LocationStart, op.LocationStop);

    private string MethodText(Operation op)
    {
        var parts = new List<string>();
        if (op.BwtsUsed) parts.Add("BWTS");
        if (op.Type == OpType.TR) parts.Add("TR");
        return string.Join("; ", parts);
    }

    private (DateTime utcStart, DateTime utcStop) ToUtc(Operation op)
    {
        // Op.TzOffset like +02:00 or -03:30
        if (TimeSpan.TryParse(op.TzOffset, out var offset))
        {
            var start = DateTime.SpecifyKind(op.StartLocal - offset, DateTimeKind.Utc);
            var stop = DateTime.SpecifyKind(op.StopLocal - offset, DateTimeKind.Utc);
            return (start, stop);
        }
        return (op.StartLocal, op.StopLocal);
    }

    private string TanksUsedOneLine()
    {
        // For B use To tanks, for DB use From tanks
        var legs = (Op.Type == OpType.B)
            ? Op.Legs.Where(l => l.Direction == LegDir.To && !l.IsSea && l.Tank != null)
            : Op.Legs.Where(l => l.Direction == LegDir.From && !l.IsSea && l.Tank != null);

        var grouped = GroupPairs(legs.Select(l => l.Tank!));
        return JoinOxford(grouped);
    }

    private string FullTankNames(LegDir Direction)
    {
        var tanks = Op.Legs.Where(l => l.Direction == Direction && !l.IsSea && l.Tank != null)
                           .Select(l => l.Tank!).ToList();
        return JoinOxford(GroupPairs(tanks));
    }

    private static IEnumerable<string> GroupPairs(IEnumerable<Tank> tanks)
    {
        // Pair "base (P&S)" when both sides present, else full name
        var dict = new Dictionary<string, List<string>>();
        foreach (var t in tanks)
        {
            var (baseName, side) = SplitPortStbd(t.Name);
            if (!dict.TryGetValue(baseName, out var list)) { list = new(); dict[baseName] = list; }
            if (!string.IsNullOrEmpty(side)) list.Add(side);
            else list.Add(""); // no side
        }
        foreach (var kv in dict)
        {
            var sides = kv.Value.Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s).ToList();
            if (sides.Contains("Port") && sides.Contains("Stbd"))
                yield return $"{kv.Key} (P&S)";
            else if (sides.Count == 1)
                yield return $"{kv.Key} ({(sides[0] == "Port" ? "Port" : "Stbd")})";
            else
                yield return kv.Key;
        }
    }

    private static (string baseName, string side) SplitPortStbd(string fullName)
    {
        // expects "... (Port)" or "... (Stbd)" else returns whole
        var s = fullName.Trim();
        if (s.EndsWith("(Port)", StringComparison.OrdinalIgnoreCase))
            return (s[..^("(Port)".Length)].Trim(), "Port");
        if (s.EndsWith("(Stbd)", StringComparison.OrdinalIgnoreCase))
            return (s[..^("(Stbd)".Length)].Trim(), "Stbd");
        return (s, "");
    }

    private string BuildOldBallastDeballastTankLine()
    {
        // Example: "No.6 WDBT (P&S) BALLASTED 1120m3 + 1120m3 BY PUMP VIA BWTS"
        bool isBallast = Op.Type == OpType.B;
        var legs = Op.Legs.Where(l => !l.IsSea && l.Tank != null &&
                                      (isBallast ? l.Direction == LegDir.To : l.Direction == LegDir.From))
                          .ToList();

        var groups = legs.GroupBy(l => SplitPortStbd(l.Tank!.Name).baseName);

        var sb = new StringBuilder();
        var verb = isBallast ? "BALLASTED" : "DEBALLASTED";

        foreach (var g in groups.OrderBy(x => x.First().Tank!.Order))
        {
            var items = g.ToList();
            var ports = items.Where(i => SplitPortStbd(i.Tank!.Name).side == "Port").ToList();
            var stbds = items.Where(i => SplitPortStbd(i.Tank!.Name).side == "Stbd").ToList();

            string tankLabel;
            string qtyText;

            if (ports.Any() && stbds.Any())
            {
                tankLabel = $"{g.Key} (P&S)";
                qtyText = $"{ports.Sum(i => i.Delta)}m3 + {stbds.Sum(i => i.Delta)}m3";
            }
            else if (ports.Any())
            {
                tankLabel = $"{g.Key} (Port)";
                qtyText = $"{ports.Sum(i => i.Delta)}m3";
            }
            else if (stbds.Any())
            {
                tankLabel = $"{g.Key} (Stbd)";
                qtyText = $"{stbds.Sum(i => i.Delta)}m3";
            }
            else
            {
                tankLabel = g.Key;
                qtyText = $"{items.Sum(i => i.Delta)}m3";
            }

            if (sb.Length > 0) sb.AppendLine();
            sb.Append($"{tankLabel} {verb} {qtyText} BY PUMP");
            if (Op.BwtsUsed) sb.Append(" VIA BWTS");
        }

        return sb.ToString();
    }

    private string BuildInitialFinalPairs()
    {
        // "INITIAL: 80m3 + 80m3; FINAL : 1200m3 + 1200m3"
        bool isBallast = Op.Type == OpType.B;
        var legs = Op.Legs.Where(l => !l.IsSea && l.Tank != null &&
                                      (isBallast ? l.Direction == LegDir.To : l.Direction == LegDir.From))
                          .OrderBy(l => l.Tank!.Order).ToList();

        var groups = legs.GroupBy(l => SplitPortStbd(l.Tank!.Name).baseName);
        var lines = new List<string>();

        foreach (var g in groups)
        {
            var items = g.OrderBy(i => SplitPortStbd(i.Tank!.Name).side).ToList();

            double initPort = items.Where(i => SplitPortStbd(i.Tank!.Name).side == "Port").Select(i => i.VolumeBefore).DefaultIfEmpty(0).First();
            double initStbd = items.Where(i => SplitPortStbd(i.Tank!.Name).side == "Stbd").Select(i => i.VolumeBefore).DefaultIfEmpty(0).First();
            double finPort = items.Where(i => SplitPortStbd(i.Tank!.Name).side == "Port").Select(i => i.VolumeAfter).DefaultIfEmpty(0).First();
            double finStbd = items.Where(i => SplitPortStbd(i.Tank!.Name).side == "Stbd").Select(i => i.VolumeAfter).DefaultIfEmpty(0).First();

            string name = g.Key;
            string pairInit = (initPort > 0 || initStbd > 0)
                ? (items.Count > 1 ? $"{initPort}m3 + {initStbd}m3" : $"{items.First().VolumeBefore}m3")
                : $"{items.First().VolumeBefore}m3";
            string pairFinal = (finPort > 0 || finStbd > 0)
                ? (items.Count > 1 ? $"{finPort}m3 + {finStbd}m3" : $"{items.First().VolumeAfter}m3")
                : $"{items.First().VolumeAfter}m3";

            lines.Add($"INITIAL: {pairInit}; FINAL : {pairFinal}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private IEnumerable<string> BuildTransferRows()
    {
        // produce the 1->1, 1->2, 2->1, 2->2 verb + initial/final lines as required
        double total = Op.Legs.Where(l => !l.IsSea).Sum(l => l.Delta);

        var fromGroups = Op.Legs.Where(l => !l.IsSea && l.Direction == LegDir.From).GroupBy(l => SplitPortStbd(l.Tank!.Name).baseName);
        var toGroups = Op.Legs.Where(l => !l.IsSea && l.Direction == LegDir.To).GroupBy(l => SplitPortStbd(l.Tank!.Name).baseName);

        string fromLabel = JoinOxford(GroupPairs(fromGroups.SelectMany(g => g.Select(x => x.Tank!))));
        string toLabel = JoinOxford(GroupPairs(toGroups.SelectMany(g => g.Select(x => x.Tank!))));

        var list = new List<string>();
        list.Add($"{SumString(Op.Legs.Where(l => l.Direction == LegDir.From && !l.IsSea))} INTERNALLY TRANSFERRED FROM {fromLabel} to {toLabel} BY PUMP.");

        foreach (var g in fromGroups)
        {
            var items = g.ToList();
            string name = items.Count > 1 ? $"{g.Key} (P&S)" : items.First().Tank!.Name;
            string pair = PairInitFinal(items);
            list.Add($"{name} - {pair}");
        }
        foreach (var g in toGroups)
        {
            var items = g.ToList();
            string name = items.Count > 1 ? $"{g.Key} (P&S)" : items.First().Tank!.Name;
            string pair = PairInitFinal(items);
            list.Add($"{name} - {pair}");
        }

        return list;

        static string SumString(IEnumerable<OperationLeg> legs)
        {
            var ports = legs.Where(i => i.Tank != null && SplitPortStbd(i.Tank!.Name).side == "Port").Sum(i => i.Delta);
            var stbds = legs.Where(i => i.Tank != null && SplitPortStbd(i.Tank!.Name).side == "Stbd").Sum(i => i.Delta);
            if (ports > 0 && stbds > 0) return $"{ports}m3 + {stbds}m3";
            return $"{legs.Sum(i => i.Delta)}m3";
        }
        static string PairInitFinal(List<OperationLeg> items)
        {
            if (items.Count > 1)
            {
                var p = items.FirstOrDefault(i => SplitPortStbd(i.Tank!.Name).side == "Port");
                var s = items.FirstOrDefault(i => SplitPortStbd(i.Tank!.Name).side == "Stbd");
                return $"INITIAL: {p?.VolumeBefore ?? 0}m3 + {s?.VolumeBefore ?? 0}m3; FINAL: {p?.VolumeAfter ?? 0}m3 + {s?.VolumeAfter ?? 0}m3.";
            }
            var one = items.First();
            return $"INITIAL: {one.VolumeBefore}m3; FINAL: {one.VolumeAfter}m3.";
        }
    }

    private string JoinOxford(IEnumerable<string> names)
    {
        var list = names.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (list.Count <= 1) return list.FirstOrDefault() ?? "";
        return string.Join(", ", list.Take(list.Count - 1)) + " and " + list.Last();
    }

    private double FinalOnboardAfterThisOperation()
    {
        // Sum of all tanks final volumes immediately after this op (use legs' VolumeAfter where available; for tanks not touched in op, keep their current)
        // Simple approach: use Tank.CurrentCapacity after Recalc of all operations up to this one.
        // If you store a snapshot on operation, use it. Here we approximate by summing VolumeAfter for all legs per tank, or fall back to Tank.CurrentCapacity.
        var tankFinals = new Dictionary<Guid, double>();
        foreach (var l in Op.Legs.Where(x => !x.IsSea && x.TankId.HasValue))
        {
            tankFinals[l.TankId!.Value] = l.VolumeAfter;
        }
        // include other tanks not in op using their current capacity
        var others = _db.Tanks.AsNoTracking().ToList();
        foreach (var t in others)
        {
            if (!tankFinals.ContainsKey(t.Id)) tankFinals[t.Id] = t.CurrentCapacity;
        }
        return tankFinals.Values.Sum();
    }
}
