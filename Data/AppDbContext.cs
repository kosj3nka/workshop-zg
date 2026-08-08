using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Data;

// AppDbContext is the bridge between your C# models and the actual database.
// Think of it as the "database session" — you query through it and save through it.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Each DbSet = one database table
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<WorkshopOccurrence> WorkshopOccurrences => Set<WorkshopOccurrence>();
    public DbSet<WorkshopPhoto> WorkshopPhotos => Set<WorkshopPhoto>();
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<ReservedDay> ReservedDays => Set<ReservedDay>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Slug must be unique — no two workshops can have the same URL
        modelBuilder.Entity<Workshop>()
            .HasIndex(w => w.Slug)
            .IsUnique();

        // Email must be unique in the subscribers table
        modelBuilder.Entity<Subscriber>()
            .HasIndex(s => s.Email)
            .IsUnique();

        // One workshop -> many photos, delete photos when workshop is deleted
        modelBuilder.Entity<Workshop>()
            .HasMany(w => w.Photos)
            .WithOne(p => p.Workshop)
            .HasForeignKey(p => p.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        // One workshop -> many occurrences (dates), delete occurrences when workshop is deleted
        modelBuilder.Entity<Workshop>()
            .HasMany(w => w.Occurrences)
            .WithOne(o => o.Workshop)
            .HasForeignKey(o => o.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        // One menu category -> many menu items, delete items when category is deleted
        modelBuilder.Entity<MenuCategory>()
            .HasMany(c => c.Items)
            .WithOne(i => i.Category)
            .HasForeignKey(i => i.MenuCategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
