using System.Text;
using BallastLog.Mate.Data;
using Microsoft.EntityFrameworkCore;

namespace BallastLog.Mate.Services;

public class CsvService
{
    private readonly AppDbContext _db;
    public CsvService(AppDbContext db) => _db = db;

    public async Task<byte[]> ExportTanksAsync()
    {
        var tanks = await _db.Tanks.OrderBy(t => t.Order).ThenBy(t => t.Code).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("TankID,Name,MaxCapacity,CurrentCapacity");
        foreach (var t in tanks)
        {
            var name = t.Name.Replace("\"", "\"\"");
            sb.AppendLine($"{t.Code},\"{name}\",{t.MaxCapacity},{t.CurrentCapacity}");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}