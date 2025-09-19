using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BallastLog.Mate.Pages.Reports.LogOld;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }

    public List<Row> Rows { get; set; } = new();

    public class Row
    {
        public string Date { get; set; } = "";
        public string Item { get; set; } = "";
        public string Record { get; set; } = "";
        public Guid OpId { get; set; }
        public bool RecordedLog { get; set; }
        public bool FirstInOp { get; set; }
    }

    public async Task OnGet()
    {
        var (from, to) = Normalize(FromDate, ToDate);

        var ops = await _db.Operations
            .Include(o => o.Legs).ThenInclude(l => l.Tank)
            .Where(o => o.StopLocal >= from && o.StopLocal <= to)
            .OrderBy(o => o.StopLocal)
            .ToListAsync();

        // Use DetailsModel logic to build rows per operation
        foreach (var op in ops)
        {
            var details = new BallastLog.Mate.Pages.Ops.DetailsModel(_db) { Id = op.Id };
            await details.OnGet();

            for (int i = 0; i < details.OldLog.Count; i++)
            {
                var r = details.OldLog[i];
                Rows.Add(new Row
                {
                    Date = r.Date,
                    Item = r.Item,
                    Record = r.Record,
                    OpId = op.Id,
                    RecordedLog = op.RecordedToLogBook,
                    FirstInOp = (i == 0)
                });
            }
        }
    }

    public async Task<IActionResult> OnPostMarkLog(Guid opId, DateTime? fromDate, DateTime? toDate)
    {
        var op = await _db.Operations.FindAsync(opId);
        if (op != null)
        {
            op.RecordedToLogBook = true;
            op.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new
        {
            fromDate = fromDate?.ToString("yyyy-MM-dd"),
            toDate = toDate?.ToString("yyyy-MM-dd")
        });
    }

    private static (DateTime from, DateTime to) Normalize(DateTime? f, DateTime? t)
    {
        var from = (f?.Date ?? DateTime.MinValue.Date);
        var to = t.HasValue ? t.Value.Date.AddDays(1).AddTicks(-1) : DateTime.MaxValue;
        if (to < from) (from, to) = (to, from);
        return (from, to);
    }
}
