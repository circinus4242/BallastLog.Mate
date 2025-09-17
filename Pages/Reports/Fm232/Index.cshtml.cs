using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Text;

namespace BallastLog.Mate.Pages.Reports.Fm232;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? TankId { get; set; }

    public List<Tank> Tanks { get; set; } = new();
    public List<Row> Rows { get; set; } = new();
    public ShipProfile Prof { get; set; } = null!;

    public class Row
    {
        public DateTime StopLocal { get; set; }
        public int TankOrder { get; set; }
        public string TankCode { get; set; } = "";
        public string Location { get; set; } = "";
        public string TimeStart { get; set; } = "";
        public int Initial { get; set; }
        public string EstUptakeSea { get; set; } = "-";
        public string EstIntakeReception { get; set; } = "-";
        public string EstCirculated { get; set; } = "-";
        public string EstDischargedSea { get; set; } = "-";
        public string EstDischargedReception { get; set; } = "-";
        public int Final { get; set; }
        public string TimeCompleted { get; set; } = "";
        public string Method { get; set; } = "";
        public string SeaDepth { get; set; } = "";
        public string DistNearestLand { get; set; } = "";
        public string Oic { get; set; } = "CO";
        public string Remarks { get; set; } = "";
        public Guid OpId { get; set; }
    }

    public async Task OnGet()
    {
        Prof = await _db.ShipProfiles.FirstAsync(p => p.Id == 1);
        Tanks = await _db.Tanks.OrderBy(t => t.Order).ToListAsync();

        var (from, to) = NormalizeRange(FromDate, ToDate);
        var q = _db.Operations.Include(o => o.Legs).ThenInclude(l => l.Tank)
                              .Where(o => o.StopLocal >= from && o.StopLocal <= to);

        var data = await q.OrderBy(o => o.StopLocal).ToListAsync();

        foreach (var op in data)
        {
            var legs = op.Legs.Where(l => !l.IsSea && l.Tank != null).ToList();
            if (TankId.HasValue) legs = legs.Where(l => l.TankId == TankId.Value).ToList();
            foreach (var g in legs.GroupBy(l => l.TankId))
            {
                var any = g.First();
                var tank = any.Tank!;
                int initial = g.Any(x => x.Direction == LegDir.From) ? g.First(x => x.Direction == LegDir.From).VolumeBefore
                                                                     : g.First(x => x.Direction == LegDir.To).VolumeBefore;
                int final = g.Any(x => x.Direction == LegDir.To) ? g.Last(x => x.Direction == LegDir.To).VolumeAfter
                                                                 : g.Last(x => x.Direction == LegDir.From).VolumeAfter;
                int deltaTo = g.Where(x => x.Direction == LegDir.To).Sum(x => x.Delta);
                int deltaFrom = g.Where(x => x.Direction == LegDir.From).Sum(x => x.Delta);

                Rows.Add(new Row
                {
                    StopLocal = op.StopLocal,
                    TankOrder = tank.Order,
                    TankCode = tank.Code,
                    Location = SameOr(op.LocationStart, op.LocationStop),
                    TimeStart = op.StartLocal.ToString("HH':'mm"),
                    Initial = initial,
                    EstUptakeSea = op.Type == OpType.B ? (deltaTo > 0 ? deltaTo.ToString() : "-") : "-",
                    EstIntakeReception = "-",
                    EstCirculated = (op.BwtsUsed || op.Type == OpType.TR) ? (deltaTo + deltaFrom > 0 ? (deltaTo + deltaFrom).ToString() : "-") : "-",
                    EstDischargedSea = op.Type == OpType.DB ? (deltaFrom > 0 ? deltaFrom.ToString() : "-") : "-",
                    EstDischargedReception = "-",
                    Final = final,
                    TimeCompleted = op.StopLocal.ToString("HH':'mm"),
                    Method = MethodText(op),
                    SeaDepth = op.MinDepth?.ToString() ?? "",
                    DistNearestLand = op.DistanceNearestLand?.ToString() ?? "",
                    Oic = "CO",
                    Remarks = BuildRemarks(op, Prof),
                    OpId = op.Id
                });
            }
        }

        Rows = Rows.OrderBy(r => r.StopLocal)  // old on top
                   .ThenBy(r => r.TankOrder)
                   .ToList();
    }

    public async Task<FileResult> OnPostExportCsv(DateTime? fromDate, DateTime? toDate, Guid? tankId)
    {
        FromDate = fromDate; ToDate = toDate; TankId = tankId;
        await OnGet(); // fill Rows

        var sb = new StringBuilder();
        sb.AppendLine("Tank,Date,Location,TimeStart,Initial,EstUptakeSea,EstIntakeReception,EstCirculated,EstDischargedSea,EstDischargedPRF,Final,TimeCompleted,Method,SeaDepth,DistNearestLand,OIC,Remarks");
        foreach (var r in Rows)
        {
            sb.AppendLine(string.Join(",",
                Csv(r.TankCode), Csv(r.StopLocal.ToString("dd'/'MM'/'yy")),
                Csv(r.Location), Csv(r.TimeStart), r.Initial,
                Csv(r.EstUptakeSea), Csv(r.EstIntakeReception), Csv(r.EstCirculated), Csv(r.EstDischargedSea), Csv(r.EstDischargedReception),
                r.Final, Csv(r.TimeCompleted), Csv(r.Method), Csv(r.SeaDepth), Csv(r.DistNearestLand), Csv(r.Oic), Csv(r.Remarks)
            ));
        }
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", "fm232.csv");
    }

    public async Task<FileResult> OnPostExportPdf(DateTime? fromDate, DateTime? toDate, Guid? tankId)
    {
        FromDate = fromDate; ToDate = toDate; TankId = tankId;
        await OnGet(); // prepare Rows

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Text($"FM-232 (from {FromDate:dd-MMM-yyyy} to {ToDate:dd-MMM-yyyy})").SemiBold().FontSize(12).AlignCenter();

                page.Content().Table(t =>
                {
                    string[] headers = { "Tank", "Date", "Location", "Start", "Initial", "UptakeSea", "IntakePRF", "Circ/Treated", "DischSea", "DischPRF", "Final", "Completed", "Method", "Depth", "Dist NL", "OIC", "Remarks" };
                    t.ColumnsDefinition(cols =>
                    {
                        foreach (var _ in headers) cols.RelativeColumn();
                    });

                    t.Header(h =>
                    {
                        foreach (var head in headers) h.Cell().Element(CellHeader).Text(head);
                        static IContainer CellHeader(IContainer c) => c.DefaultTextStyle(s => s.SemiBold()).Padding(2).Background(Colors.Grey.Lighten2);
                    });

                    foreach (var r in Rows)
                    {
                        t.Cell().Padding(2).Text(r.TankCode);
                        t.Cell().Padding(2).Text(r.StopLocal.ToString("dd/MM/yy"));
                        t.Cell().Padding(2).Text(r.Location);
                        t.Cell().Padding(2).Text(r.TimeStart);
                        t.Cell().Padding(2).Text(r.Initial.ToString());
                        t.Cell().Padding(2).Text(r.EstUptakeSea);
                        t.Cell().Padding(2).Text(r.EstIntakeReception);
                        t.Cell().Padding(2).Text(r.EstCirculated);
                        t.Cell().Padding(2).Text(r.EstDischargedSea);
                        t.Cell().Padding(2).Text(r.EstDischargedReception);
                        t.Cell().Padding(2).Text(r.Final.ToString());
                        t.Cell().Padding(2).Text(r.TimeCompleted);
                        t.Cell().Padding(2).Text(r.Method);
                        t.Cell().Padding(2).Text(r.SeaDepth);
                        t.Cell().Padding(2).Text(r.DistNearestLand);
                        t.Cell().Padding(2).Text(r.Oic);
                        t.Cell().Padding(2).Text(r.Remarks ?? "");
                    }
                });
            });
        });

        var pdf = doc.GeneratePdf();
        return File(pdf, "application/pdf", "fm232.pdf");
    }

    private static (DateTime from, DateTime to) NormalizeRange(DateTime? from, DateTime? to)
    {
        var f = (from?.Date ?? DateTime.MinValue.Date);

        DateTime t;
        if (to.HasValue)
            t = to.Value.Date.AddDays(1).AddTicks(-1);
        else
            t = DateTime.MaxValue;

        if (t < f) (f, t) = (t, f);
        return (f, t);
    }

    private static string SameOr(string a, string b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase) ? a : $"{a} / {b}";
    private static string MethodText(Operation op)
    {
        var parts = new List<string>();
        if (op.BwtsUsed) parts.Add("BWTS");
        if (op.Type == OpType.TR) parts.Add("TR");
        return string.Join("; ", parts);
    }
    private static string BuildRemarks(Operation op, ShipProfile prof)
    {
        if (op.Type == OpType.TR) return "TR";
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(prof.Custom1Label) && !string.IsNullOrWhiteSpace(op.Custom1)) parts.Add($"{prof.Custom1Label}: {op.Custom1}");
        if (!string.IsNullOrWhiteSpace(prof.Custom2Label) && !string.IsNullOrWhiteSpace(op.Custom2)) parts.Add($"{prof.Custom2Label}: {op.Custom2}");
        if (!string.IsNullOrWhiteSpace(prof.Custom3Label) && !string.IsNullOrWhiteSpace(op.Custom3)) parts.Add($"{prof.Custom3Label}: {op.Custom3}");
        if (!string.IsNullOrWhiteSpace(prof.Custom4Label) && !string.IsNullOrWhiteSpace(op.Custom4)) parts.Add($"{prof.Custom4Label}: {op.Custom4}");
        if (!string.IsNullOrWhiteSpace(prof.Custom5Label) && !string.IsNullOrWhiteSpace(op.Custom5)) parts.Add($"{prof.Custom5Label}: {op.Custom5}");
        return string.Join("; ", parts);
    }
    private static string Csv(object? o)
    {
        var s = o?.ToString() ?? "";
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
