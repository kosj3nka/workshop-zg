using Microsoft.EntityFrameworkCore;
using WorkshopZagreb.Models;

namespace WorkshopZagreb.Data;

// AppDbContext is the bridge between your C# models and the actual database.
// Think of it as the "database session" — you query through it and save through it.
// When you run `dotnet ef migrations add Init` + `dotnet ef database update`,
// EF Core reads this class and creates the SQLite file with the right tables.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Each DbSet = one database table
    public DbSet<Workshop> Workshops => Set<Workshop>();
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

        // One menu category -> many menu items, delete items when category is deleted
        modelBuilder.Entity<MenuCategory>()
            .HasMany(c => c.Items)
            .WithOne(i => i.Category)
            .HasForeignKey(i => i.MenuCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed some sample workshops so the site isn't empty on first run
        modelBuilder.Entity<Workshop>().HasData(
            new Workshop
            {
                Id = 1,
                Name = "Akvarel za početnike",
                Date = DateTime.Today.AddDays(7),
                StartTime = new TimeSpan(14, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                Description = "Naučite osnove akvarela u opuštenom okruženju uz kavu. Sve materijale osiguravamo mi!",
                BannerUrl = "/images/unutra.webp",
                LogoUrl = null,
                InstagramPostUrl = "https://www.instagram.com/workshop.zagreb/",
                HostName = "Ana Kovač",
                HostInstagram = "https://instagram.com/anakovac.art",
                EntrioUrl = "https://entrio.hr",
                Price = 35,
                MaxParticipants = 12,
                Slug = "akvarel-za-pocetnike",
                IsArchived = false,
                CreatedAt = DateTime.UtcNow
            },
            new Workshop
            {
                Id = 2,
                Name = "Keramika za sve",
                Date = DateTime.Today.AddDays(14),
                StartTime = new TimeSpan(11, 0, 0),
                EndTime = new TimeSpan(14, 0, 0),
                Description = "Uvod u oblikovanje gline na lončarskom kolu. Iskustvo nije potrebno — samo volontiranje za pranje ruku.",
                BannerUrl = "/images/table.webp",
                LogoUrl = null,
                InstagramPostUrl = "https://www.instagram.com/workshop.zagreb/",
                HostName = "Marko Blažević",
                Price = 45,
                MaxParticipants = 8,
                Slug = "keramika-za-sve",
                IsArchived = false,
                CreatedAt = DateTime.UtcNow
            },
            new Workshop
            {
                Id = 3,
                Name = "Makramé osnove",
                Date = DateTime.Today.AddDays(21),
                StartTime = new TimeSpan(16, 0, 0),
                Description = "Naučite plesti makramé uzlove i izradite vlastiti zidni ukras.",
                BannerUrl = "/images/prostor.jpg",
                LogoUrl = null,
                InstagramPostUrl = "https://www.instagram.com/workshop.zagreb/",
                Price = 30,
                MaxParticipants = 10,
                Slug = "makrame-osnove",
                IsArchived = false,
                CreatedAt = DateTime.UtcNow
            }
        );
    }
}
