using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BallastLog.Mate.Pages.Ops;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    public DetailsModel(AppDbContext db) => _db = db;

    public Operation Op { get; set; } = default!;

    public async Task<IActionResult> OnGet(Guid id)
    {
        var op = await _db.Operations
            .Include(o => o.Legs).ThenInclude(l => l.Tank)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (op == null) return RedirectToPage("Index");
        Op = op;
        return Page();
    }
}