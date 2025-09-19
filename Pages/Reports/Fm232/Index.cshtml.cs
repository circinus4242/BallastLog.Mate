using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;
using static BallastLog.Mate.Pages.Ops.DetailsModel;

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
        // display
        public DateTime StopLocal { get; set; }
        public int TankOrder { get; set; }
        public string TankCode { get; set; } = "";
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

        // control
        public Guid OpId { get; set; }
        public bool RecordedFm232 { get; set; }
        public bool FirstInOp { get; set; }
    }

    public async Task OnGet()
    {
        Prof = await _db.ShipProfiles.FirstAsync(p => p.Id == 1);
        Tanks = await _db.Tanks.OrderBy(t => t.Order).ToListAsync();

        var (from, to) = NormalizeRange(FromDate, ToDate);
        var ops = await _db.Operations
            .Include(o => o.Legs).ThenInclude(l => l.Tank)
            .Where(o => o.StopLocal >= from && o.StopLocal <= to)
            .OrderBy(o => o.StopLocal)
            .ToListAsync();

        foreach (var op in ops)
        {
            var legs = op.Legs.Where(l => !l.IsSea && l.Tank != null).ToList();
            if (TankId.HasValue) legs = legs.Where(l => l.TankId == TankId.Value).ToList();
            if (!legs.Any()) continue;

            bool first = true;
            foreach (var g in legs.GroupBy(l => l.TankId).OrderBy(g => g.First().Tank!.Order))
            {
                var any = g.First();
                var tank = any.Tank!;
                double initial = g.Any(x => x.Direction == LegDir.From) ? g.First(x => x.Direction == LegDir.From).VolumeBefore
                                                                     : g.First(x => x.Direction == LegDir.To).VolumeBefore;
                double final = g.Any(x => x.Direction == LegDir.To) ? g.Last(x => x.Direction == LegDir.To).VolumeAfter
                                                                 : g.Last(x => x.Direction == LegDir.From).VolumeAfter;
                double deltaTo = g.Where(x => x.Direction == LegDir.To).Sum(x => x.Delta);
                double deltaFrom = g.Where(x => x.Direction == LegDir.From).Sum(x => x.Delta);

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
                    OpId = op.Id,
                    RecordedFm232 = op.RecordedToFm232,
                    FirstInOp = first
                });
                first = false;
            }
        }

        Rows = Rows.OrderBy(r => r.StopLocal).ThenBy(r => r.TankOrder).ToList();
    }

    public async Task<IActionResult> OnPostMarkFm232(Guid opId, DateTime? fromDate, DateTime? toDate, Guid? tankId)
    {
        var op = await _db.Operations.FindAsync(opId);
        if (op != null)
        {
            op.RecordedToFm232 = true;
            op.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new
        {
            fromDate = fromDate?.ToString("yyyy-MM-dd"),
            toDate = toDate?.ToString("yyyy-MM-dd"),
            tankId
        });
    }

    public async Task<FileResult> OnPostExportCsv(DateTime? fromDate, DateTime? toDate, Guid? tankId)
    {
        FromDate = fromDate; ToDate = toDate; TankId = tankId;
        await OnGet();

        var sb = new StringBuilder();
        sb.AppendLine("Tank,Date,Location,TimeStart,Initial,EstUptakeSea,EstIntakeReception,EstCirculated,EstDischargedSea,EstDischargedPRF,Final,TimeCompleted,Method,SeaDepth,DistNearestLand,OIC,Remarks,RecordedFm232");
        foreach (var r in Rows)
        {
            sb.AppendLine(string.Join(",",
                Csv(r.TankCode), Csv(r.StopLocal.ToString("dd'/'MM'/'yy")),
                Csv(r.Location), Csv(r.TimeStart), r.Initial,
                Csv(r.EstUptakeSea), Csv(r.EstIntakeReception), Csv(r.EstCirculated), Csv(r.EstDischargedSea), Csv(r.EstDischargedReception),
                r.Final, Csv(r.TimeCompleted), Csv(r.Method), Csv(r.SeaDepth), Csv(r.DistNearestLand), Csv(r.Oic), Csv(r.Remarks),
                r.RecordedFm232 ? "YES" : "NO"
            ));
        }
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", "fm232.csv");
    }

    public async Task<FileResult> OnPostExportPdf(DateTime? fromDate, DateTime? toDate, Guid? tankId)
    {
        FromDate = fromDate; ToDate = toDate; TankId = tankId;
        await OnGet(); // fills Prof + Rows

        var groups = Rows.GroupBy(r => r.OpId).OrderBy(g => g.First().StopLocal).ToList();
        var dateRange = $"{FromDate:dd-MMM-yyyy} to {ToDate:dd-MMM-yyyy}";

        // local helpers
        QuestPDF.Settings.License = LicenseType.Community;

        var headerStyle = TextStyle.Default.FontFamily("Inter").SemiBold().FontSize(12);
        var small = TextStyle.Default.FontFamily("Inter").FontSize(9);
        var mono = TextStyle.Default.FontFamily("Roboto Mono").FontSize(9);
        var cellText = small;
        var cellTextMono = mono;

        byte zebra = 0;

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(small);

                // ----- Header -----
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text($"FM-232 – {Prof.ShipName ?? "Ship"}").Style(headerStyle);
                        col.Item().Text($"Period: {dateRange}");
                    });
                    row.ConstantItem(200).AlignRight().Text($"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontFamily("Inter").FontSize(9);
                });

                // ----- Footer -----
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ").FontFamily("Inter");
                    x.CurrentPageNumber().FontFamily("Inter");
                    x.Span(" / ").FontFamily("Inter");
                    x.TotalPages().FontFamily("Inter");
                });

                // ----- Content -----
                page.Content().Column(col =>
                {
                    foreach (var g in groups)
                    {
                        var first = g.First();

                        // Operation header strip
                        col.Item().PaddingBottom(4).Background(Colors.Grey.Lighten3).Padding(6).Border(0.5f).BorderColor(Colors.Grey.Medium).Row(r =>
                        {
                            r.RelativeItem().Text(txt =>
                            {
                                txt.Line($"{first.StopLocal:dd/MM/yy}  |  {first.TimeStart} → {first.TimeCompleted}  |  {first.Location}").FontFamily("Inter").SemiBold();
                                txt.Line($"Method: {first.Method}").FontFamily("Inter");
                            });
                            r.ConstantItem(140).AlignRight().Text(t =>
                            {
                                t.Span(first.RecordedFm232 ? "RECORDED" : "NOT RECORDED")
                                 .FontFamily("Inter")
                                 .SemiBold()
                                 .FontSize(10)
                                 .FontColor(first.RecordedFm232 ? Colors.Green.Darken2 : Colors.Red.Medium);
                            });
                        });

                        // Table
                        col.Item().Table(t =>
                        {
                            // Column layout tuned for readability
                            t.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(50);  // Tank
                                c.ConstantColumn(46);  // Date
                                c.RelativeColumn(2);   // Location
                                c.ConstantColumn(40);  // Start
                                c.ConstantColumn(46);  // Initial
                                c.ConstantColumn(24);  // UptakeSea
                                c.ConstantColumn(24);  // IntakePRF
                                c.ConstantColumn(46);  // Circ/Treated
                                c.ConstantColumn(24);  // DischSea
                                c.ConstantColumn(24);  // DischPRF
                                c.ConstantColumn(46);  // Final
                                c.ConstantColumn(46);  // Completed
                                c.ConstantColumn(40);  // Method
                                c.ConstantColumn(36);  // Depth
                                c.ConstantColumn(36);  // Dist NL
                                c.ConstantColumn(30);  // OIC
                                c.RelativeColumn(3);   // Remarks
                            });

                            // header row (repeats on new pages)
                            t.Header(h =>
                            {
                                void HeadCell(string s) =>
                                    h.Cell().Element(HeaderCell).Text(s).FontFamily("Inter").SemiBold();
                                HeadCell("Tank"); HeadCell("Date"); HeadCell("Location"); HeadCell("Start");
                                HeadCell("Initial"); HeadCell("UptakeSea"); HeadCell("IntakePRF"); HeadCell("Circ/Treated");
                                HeadCell("DischSea"); HeadCell("DischPRF"); HeadCell("Final"); HeadCell("Completed");
                                HeadCell("Method"); HeadCell("Depth"); HeadCell("Dist NL"); HeadCell("OIC"); HeadCell("Remarks");

                                static IContainer HeaderCell(IContainer c) =>
                                    c.DefaultTextStyle(x => x.FontSize(9))
                                     .PaddingVertical(4).PaddingHorizontal(3)
                                     .Background(Colors.Grey.Lighten2).BorderBottom(1).BorderColor(Colors.Grey.Medium);
                            });

                            // data rows
                            int idx = 0;
                            foreach (var r in g)
                            {
                                bool even = (idx++ % 2 == 0);
                                t.Cell().Element(e => RowCell(e, even)).Text(r.TankCode).Style(cellText);
                                t.Cell().Element(e => RowCell(e, even)).AlignRight().Text(r.StopLocal.ToString("dd/MM/yy")).Style(cellText);
                                t.Cell().Element(e => RowCell(e, even)).Text(r.Location).Style(cellText).WrapAnywhere();
                                t.Cell().Element(e => RowCell(e, even)).AlignRight().Text(r.TimeStart).Style(cellTextMono);

                                t.Cell().Element(e => RowCell(e, even)).AlignRight().Text(r.Initial.ToString()).Style(cellTextMono);
                                t.Cell().Element(e => RowCell(e, even)).AlignRight().Text(r.EstUptakeSea).Style(cellTextMono);
                                t.Cell().Element(e => RowCell(e, even)).AlignRight().Text(r.EstIntakeReception).Style(cellTextMono);
                                t.Cell().Element(e => RowCell(e, even)).AlignRight().Text(r.EstCirculated).Style(cellTextMono);
                                t.Cell().Element(e => RowCell(e, even)).AlignRight().Text(r.EstDischargedSea).Style(cellTextMono);
                                t.Cell().Element(e => RowCell(e, even)).AlignRight().Text(r.EstDischargedReception).Style(cellTextMono);
                                t.Cell().Element(e => RowCell(e, even)).AlignRight().Text(r.Final.ToString()).Style(cellTextMono);
                                t.Cell().Element(e => RowCell(e, even)).AlignRight().Text(r.TimeCompleted).Style(cellTextMono);

                                t.Cell().Element(e => RowCell(e, even)).Text(r.Method).Style(cellText);
                                t.Cell().Element(e => RowCell(e, even)).AlignRight().Text(r.SeaDepth).Style(cellTextMono);
                                t.Cell().Element(e => RowCell(e, even)).AlignRight().Text(r.DistNearestLand).Style(cellTextMono);
                                t.Cell().Element(e => RowCell(e, even)).AlignCenter().Text(r.Oic).Style(cellText);
                                t.Cell().Element(e => RowCell(e, even)).Text(r.Remarks ?? "").Style(cellText).WrapAnywhere();
                            }

                            static IContainer RowCell(IContainer c, bool even) =>
                                c.Background(even ? Colors.Grey.Lighten5 : Colors.White)
                                 .PaddingVertical(2.5f).PaddingHorizontal(3)
                                 .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
                        });

                        // space between groups
                        col.Item().Height(10);
                    }
                });
            });
        }).GeneratePdf();

        return File(pdf, "application/pdf", "fm232.pdf");
    }


    // ---- helpers ----
    

    // ------- small styling helpers
    static IContainer HeaderCell(IContainer c) =>
        c.Padding(4).Background(Colors.Grey.Lighten3)
         .BorderBottom(1).BorderColor(Colors.Grey.Medium)
         .DefaultTextStyle(TextStyle.Default.SemiBold());

    enum Align { Left, Right, Center }
    static IContainer RowCell(IContainer c, bool zebra, Align a = Align.Left)
    {
        var boxed = c.Background(zebra ? Colors.White : Colors.Grey.Lighten5)
                      .PaddingVertical(2.5f).PaddingHorizontal(3)
                      .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1);

        return a switch
        {
            Align.Right => boxed.AlignRight(),
            Align.Center => boxed.AlignCenter(),
            _ => boxed
        };
    }

    static string Num(int v) => v == 0 ? "" : v.ToString("0");

    // ------- resilient property readers (omit if not present)
    static string FirstNonEmpty(params string[] items) =>
        items.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";

    static string ReadProp(object? obj, params string[] names)
    {
        var val = ReadValue(obj, names);
        return val?.ToString() ?? "";
    }

    static object? ReadValue(object? obj, params string[] names)
    {
        if (obj == null) return null;
        var t = obj.GetType();
        foreach (var n in names)
        {
            var p = t.GetProperty(n);
            if (p == null) continue;
            var v = p.GetValue(obj);
            if (v == null) continue;
            var s = v.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return v;
        }
        return null;
    }

    static string FormatVolume(object? v)
    {
        if (v == null) return "";
        try
        {
            var d = Convert.ToDecimal(v);
            return $"{d:0} m³";
        }
        catch { return v.ToString() ?? ""; }
    }




    // small helper to avoid "0" / null noise
    static string Num(decimal? v) => v.HasValue ? v.Value.ToString("0") : "";

    private static (DateTime from, DateTime to) NormalizeRange(DateTime? from, DateTime? to)
    {
        var f = (from?.Date ?? DateTime.MinValue.Date);
        DateTime t = to.HasValue ? to.Value.Date.AddDays(1).AddTicks(-1) : DateTime.MaxValue;
        if (t < f) (f, t) = (t, f);
        return (f, t);
    }

    private static string SameOr(string a, string b)
        => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase) ? a : $"{a} / {b}";

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
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
