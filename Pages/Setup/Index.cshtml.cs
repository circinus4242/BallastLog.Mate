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
    public List<TankType> Types { get; set; } = new();

    // ------- Add / edit inputs -------

    [BindProperty]
    public TankInput NewTank { get; set; } = new();

    public class TankInput
    {
        [Required, MaxLength(16)]
        public string Code { get; set; } = "";
        [Required, MaxLength(200)]
        public string Name { get; set; } = "";
        public double MaxCapacity { get; set; }
        public double InitialCapacity { get; set; }
        public int Order { get; set; }
        public Guid? TankTypeId { get; set; } // NEW: selected type
    }

    [BindProperty]
    public NewTypeInput NewType { get; set; } = new();

    public class NewTypeInput
    {
        [Required, MaxLength(32)]
        public string Name { get; set; } = "";
        [Required, RegularExpression("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
        public string ColorHex { get; set; } = "#0d6efd";
    }

    // ------- GET -------
    public async Task OnGet()
    {
        Profile = await _db.ShipProfiles.FirstAsync(p => p.Id == 1);
        Tanks = await _db.Tanks.Include(t => t.TankType)
                               .OrderBy(t => t.Order).ThenBy(t => t.Code)
                               .ToListAsync();
        Types = await _db.Set<TankType>().OrderBy(t => t.Name).ToListAsync();
    }

    // ------- Profile -------
    public async Task<IActionResult> OnPostSaveProfile()
    {
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

    // ------- Tanks -------
    public async Task<IActionResult> OnPostAddTank()
    {
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
            TankTypeId = NewTank.TankTypeId,
            IsActive = true
        };
        _db.Tanks.Add(t);
        await _db.SaveChangesAsync();
        await _recalc.RecalculateAllAsync();

        TempData["msg"] = $"Tank {t.Code} added.";
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

    // ------- Tank Types -------
    public async Task<IActionResult> OnPostAddType()
    {
        ModelState.Clear();
        if (!TryValidateModel(NewType, nameof(NewType)))
            return await Reload();

        _db.Add(new TankType { Name = NewType.Name.Trim(), ColorHex = NewType.ColorHex });
        await _db.SaveChangesAsync();
        TempData["msg"] = "Tank type added.";
        NewType = new NewTypeInput();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteType(Guid id)
    {
        var type = await _db.Set<TankType>().FindAsync(id);
        if (type != null)
        {
            // Optional: guard if any tanks use this type
            bool inUse = await _db.Tanks.AnyAsync(t => t.TankTypeId == id);
            if (!inUse)
            {
                _db.Remove(type);
                await _db.SaveChangesAsync();
                TempData["msg"] = "Tank type deleted.";
            }
            else
            {
                TempData["msg"] = "Type is in use by tanks.";
            }
        }
        return RedirectToPage();
    }

    // ------- Import / Export -------
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

    // ------- helpers -------
    private async Task<PageResult> Reload()
    {
        Profile = await _db.ShipProfiles.FirstAsync(p => p.Id == 1);
        Tanks = await _db.Tanks.Include(t => t.TankType)
                               .OrderBy(t => t.Order).ThenBy(t => t.Code)
                               .ToListAsync();
        Types = await _db.Set<TankType>().OrderBy(t => t.Name).ToListAsync();
        return Page();
    }
}
