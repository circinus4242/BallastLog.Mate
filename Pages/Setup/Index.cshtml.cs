using System.ComponentModel.DataAnnotations;
using BallastLog.Mate.Data;
using BallastLog.Mate.Models;
using BallastLog.Mate.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BallastLog.Mate.Pages.Setup;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ProfileIoService _io;
    private readonly RecalcService _recalc;

    public IndexModel(AppDbContext db, ProfileIoService io, RecalcService recalc)
    {
        _db = db; _io = io; _recalc = recalc;
    }

    [BindProperty]
    public ShipProfile Profile { get; set; } = new();

    public List<Tank> Tanks { get; set; } = new();

    [BindProperty]
    public TankInput NewTank { get; set; } = new();

    public class TankInput
    {
        [Required, MaxLength(16)]
        public string Code { get; set; } = "";
        [Required, MaxLength(200)]
        public string Name { get; set; } = "";
        [Range(0, int.MaxValue)]
        public int MaxCapacity { get; set; }
        [Range(0, int.MaxValue)]
        public int InitialCapacity { get; set; }
        public int Order { get; set; }
    }

    public async Task OnGet()
    {
        Profile = await _db.ShipProfiles.FirstAsync(p => p.Id == 1);
        Tanks = await _db.Tanks.OrderBy(t => t.Order).ThenBy(t => t.Code).ToListAsync();
    }

    // SAVE PROFILE: validate ONLY Profile
    public async Task<IActionResult> OnPostSaveProfile()
    {
        // Only validate the Profile object
        ModelState.Clear();
        if (!TryValidateModel(Profile, nameof(Profile)))
            return await Reload();

        var prof = await _db.ShipProfiles.FirstAsync(p => p.Id == 1);
        prof.ShipName = Profile.ShipName ?? "";
        prof.ShipClass = Profile.ShipClass;
        prof.MaxFlowRate = Profile.MaxFlowRate;
        prof.Custom1Label = Profile.Custom1Label;
        prof.Custom2Label = Profile.Custom2Label;
        prof.Custom3Label = Profile.Custom3Label;
        prof.Custom4Label = Profile.Custom4Label;
        prof.Custom5Label = Profile.Custom5Label;

        await _db.SaveChangesAsync();
        TempData["msg"] = "Profile saved.";
        return RedirectToPage();
    }

    // ADD TANK: validate ONLY NewTank
    public async Task<IActionResult> OnPostAddTank()
    {
        // Only validate the NewTank object
        ModelState.Clear();
        if (!TryValidateModel(NewTank, nameof(NewTank)))
            return await Reload();

        var t = new Tank
        {
            Code = NewTank.Code.Trim(),
            Name = NewTank.Name.Trim(),
            MaxCapacity = NewTank.MaxCapacity,
            InitialCapacity = NewTank.InitialCapacity,
            CurrentCapacity = NewTank.InitialCapacity,
            Order = NewTank.Order,
            IsActive = true
        };
        _db.Tanks.Add(t);
        await _db.SaveChangesAsync();
        await _recalc.RecalculateAllAsync();

        TempData["msg"] = $"Tank {t.Code} added.";
        // clear the input row after success
        NewTank = new TankInput();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteTank(Guid id)
    {
        var t = await _db.Tanks.FindAsync(id);
        if (t != null) _db.Tanks.Remove(t);
        await _db.SaveChangesAsync();
        await _recalc.RecalculateAllAsync();
        TempData["msg"] = "Tank deleted.";
        return RedirectToPage();
    }

    public async Task<FileContentResult> OnPostExport()
        => File(await _io.ExportAsync(), "application/json", "ship-profile.json");

    public async Task<IActionResult> OnPostImport()
    {
        if (Request.Form.Files.Count == 0) return await Reload();
        using var s = Request.Form.Files[0].OpenReadStream();
        await _io.ImportAsync(s);
        await _recalc.RecalculateAllAsync();
        TempData["msg"] = "Imported ship profile & tanks.";
        return RedirectToPage();
    }

    private async Task<PageResult> Reload()
    {
        Profile = await _db.ShipProfiles.FirstAsync(p => p.Id == 1);
        Tanks = await _db.Tanks.OrderBy(t => t.Order).ThenBy(t => t.Code).ToListAsync();
        return Page();
    }
}
