using System.Globalization;
using BallastLog.Mate.Data;
using BallastLog.Mate.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BallastLog.Mate.Pages.Status;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly CsvService _csv;
    public IndexModel(AppDbContext db, CsvService csv) { _db = db; _csv = csv; }

    public List<BallastLog.Mate.Models.Tank> Tanks { get; set; } = new();

    // Thresholds (percent). Applies on GET when you press Apply.
    [BindProperty(SupportsGet = true)]
    public decimal MinPct { get; set; } = 5m;

    [BindProperty(SupportsGet = true)]
    public decimal MaxPct { get; set; } = 95m;

    // Totals for the big summary bar
    public decimal TotalCurrent { get; private set; }
    public decimal TotalMax { get; private set; }

    // Page statistics
    public int TotalTanks { get; private set; }
    public int TotalEmpty { get; private set; }   // pct <= Min
    public int TotalInUse { get; private set; }   // pct > Min  (includes "full")

    public async Task OnGet()
    {
        Tanks = await _db.Tanks
            .OrderBy(t => t.Order)
            .ThenBy(t => t.Code)
            .ToListAsync();

        // Clamp / normalize thresholds
        MinPct = Math.Clamp(MinPct, 0m, 100m);
        MaxPct = Math.Clamp(MaxPct, 0m, 100m);
        if (MinPct > MaxPct) (MinPct, MaxPct) = (MaxPct, MinPct);

        // Totals
        TotalCurrent = Tanks.Sum(t => (decimal)t.CurrentCapacity);
        TotalMax = Tanks.Sum(t => (decimal)t.MaxCapacity);

        // Stats
        TotalTanks = Tanks.Count;
        TotalEmpty = Tanks.Count(t =>
        {
            var max = (decimal)t.MaxCapacity;
            var cur = (decimal)t.CurrentCapacity;
            var pct = max > 0m ? (cur / max) * 100m : 0m;
            return pct <= MinPct;
        });
        TotalInUse = Tanks.Count(t =>
        {
            var max = (decimal)t.MaxCapacity;
            var cur = (decimal)t.CurrentCapacity;
            var pct = max > 0m ? (cur / max) * 100m : 0m;
            return pct > MinPct;
        });
    }

    public async Task<FileContentResult> OnPostExport()
        => File(await _csv.ExportTanksAsync(), "text/csv", "tanks-status.csv");
}
