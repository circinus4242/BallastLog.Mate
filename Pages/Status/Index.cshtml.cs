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

    public async Task OnGet()
    {
        Tanks = await _db.Tanks.OrderBy(t => t.Order).ThenBy(t => t.Code).ToListAsync();
    }

    public async Task<FileContentResult> OnPostExport()
        => File(await _csv.ExportTanksAsync(), "text/csv", "tanks-status.csv");
}