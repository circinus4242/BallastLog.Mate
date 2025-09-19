using BallastLog.Mate.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace BallastLog.Mate.Data;

public class AppDbContext : DbContext
{
    public DbSet<ShipProfile> ShipProfiles => Set<ShipProfile>();
    public DbSet<Tank> Tanks => Set<Tank>();
    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<OperationLeg> OperationLegs => Set<OperationLeg>();
    public DbSet<TankType> TankTypes => Set<TankType>();

    public string DbPath { get; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        var baseDir = AppContext.BaseDirectory;
        var dataDir = Path.Combine(baseDir, "data");
        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
        DbPath = Path.Combine(dataDir, "ballast.db");
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ShipProfile>().HasKey(x => x.Id);
        b.Entity<ShipProfile>().HasData(new ShipProfile { Id = 1, ShipName = "", MaxFlowRate = 0 });

        b.Entity<Tank>().HasIndex(x => x.Code).IsUnique();

        b.Entity<Tank>(t =>
        {
            t.Property(x => x.MaxCapacity).HasPrecision(6, 1);
            t.Property(x => x.InitialCapacity).HasPrecision(6, 1);
            t.Property(x => x.CurrentCapacity).HasPrecision(6, 1);
        });

        b.Entity<Operation>()
            .HasMany(o => o.Legs)
            .WithOne(l => l.Operation)
            .HasForeignKey(l => l.OperationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}