using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using BallastLog.Mate.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BallastLog.Mate.Pages.Ops;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly RecalcService _recalc;
    public IndexModel(AppDbContext db, RecalcService recalc) { _db = db; _recalc = recalc; }

    public record Row(Guid Id, DateTime Start, DateTime Stop, string? LocS, string? LocE,
                      OpType Type, string TanksFrom, string TanksTo, int Total, OpState State,
                      bool RecLog, bool RecFm);

    public List<Row> Ops { get; set; } = new();

    public async Task OnGet()
    {
        var list = await _db.Operations
            .Include(o => o.Legs).ThenInclude(l => l.Tank)
            .OrderByDescending(o => o.StartLocal)
            .ToListAsync();

        Ops = list.Select(o =>
        {
            string from = string.Join(",", o.Legs.Where(l => l.Direction == LegDir.From)
                                                 .Select(l => l.IsSea ? "SEA" : l.Tank!.Code));
            string to = string.Join(",", o.Legs.Where(l => l.Direction == LegDir.To)
                                               .Select(l => l.IsSea ? "SEA" : l.Tank!.Code));
            return new Row(o.Id, o.StartLocal, o.StopLocal, o.LocationStart, o.LocationStop,
                           o.Type, from, to, o.TotalAmount, o.State, o.RecordedToLogBook, o.RecordedToFm123);
        }).ToList();
    }

    public async Task<IActionResult> OnPostMark(Guid id, string which)
    {
        var op = await _db.Operations.FindAsync(id);
        if (op == null) return RedirectToPage();
        if (which == "log") op.RecordedToLogBook = !op.RecordedToLogBook;
        if (which == "fm") op.RecordedToFm123 = !op.RecordedToFm123;
        op.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDelete(Guid id)
    {
        var op = await _db.Operations.FindAsync(id);
        if (op != null) _db.Operations.Remove(op);
        await _db.SaveChangesAsync();
        await _recalc.RecalculateAllAsync();
        return RedirectToPage();
    }
}