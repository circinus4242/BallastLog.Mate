using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BallastLog.Mate.Pages.Reports.LogNew;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<(string Date, string Code, string Item, string Record)> Rows { get; set; } = new();

    public async Task OnGet(DateTime? fromDate, DateTime? toDate)
    {
        FromDate = fromDate; ToDate = toDate;
        var (from, to) = Normalize(fromDate, toDate);

        var ops = await _db.Operations.Include(o => o.Legs).ThenInclude(l => l.Tank)
                    .Where(o => o.StopLocal >= from && o.StopLocal <= to)
                    .OrderBy(o => o.StopLocal)
                    .ToListAsync();

        foreach (var op in ops)
        {
            var details = new Ops.DetailsModel(_db) { Id = op.Id };
            await details.OnGet();
            foreach (var r in details.NewLog)
                Rows.Add((r.Date, r.Code, r.Item, r.Record));
        }
    }

    private static (DateTime from, DateTime to) Normalize(DateTime? from, DateTime? to)
    {
        var f = (from?.Date ?? DateTime.MinValue.Date);
        var t = to.HasValue ? to.Value.Date.AddDays(1).AddTicks(-1) : DateTime.MaxValue;
        if (t < f) (f, t) = (t, f);
        return (f, t);
    }
}
