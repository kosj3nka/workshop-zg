using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WorkshopZagreb.Migrations
{
    /// <inheritdoc />
    public partial class AddIsArchived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservedDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservedDays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscribers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    SubscribedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UnsubscribedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Token = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscribers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workshops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    BannerUrl = table.Column<string>(type: "TEXT", nullable: true),
                    LogoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    InstagramPostUrl = table.Column<string>(type: "TEXT", nullable: false),
                    HostName = table.Column<string>(type: "TEXT", nullable: true),
                    HostInstagram = table.Column<string>(type: "TEXT", nullable: true),
                    HostWebsite = table.Column<string>(type: "TEXT", nullable: true),
                    EntrioUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Price = table.Column<decimal>(type: "TEXT", nullable: true),
                    MaxParticipants = table.Column<int>(type: "INTEGER", nullable: true),
                    Slug = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workshops", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkshopPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkshopId = table.Column<int>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkshopPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkshopPhotos_Workshops_WorkshopId",
                        column: x => x.WorkshopId,
                        principalTable: "Workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Workshops",
                columns: new[] { "Id", "BannerUrl", "CreatedAt", "Date", "Description", "EndTime", "EntrioUrl", "HostInstagram", "HostName", "HostWebsite", "InstagramPostUrl", "IsArchived", "LogoUrl", "MaxParticipants", "Name", "Price", "Slug", "StartTime" },
                values: new object[,]
                {
                    { 1, "/images/unutra.webp", new DateTime(2026, 6, 4, 13, 19, 36, 451, DateTimeKind.Utc).AddTicks(7781), new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Local), "Naučite osnove akvarela u opuštenom okruženju uz kavu. Sve materijale osiguravamo mi!", new TimeSpan(0, 17, 0, 0, 0), "https://entrio.hr", "https://instagram.com/anakovac.art", "Ana Kovač", null, "https://www.instagram.com/workshop.zagreb/", false, null, 12, "Akvarel za početnike", 35m, "akvarel-za-pocetnike", new TimeSpan(0, 14, 0, 0, 0) },
                    { 2, "/images/table.webp", new DateTime(2026, 6, 4, 13, 19, 36, 451, DateTimeKind.Utc).AddTicks(7788), new DateTime(2026, 6, 18, 0, 0, 0, 0, DateTimeKind.Local), "Uvod u oblikovanje gline na lončarskom kolu. Iskustvo nije potrebno — samo volontiranje za pranje ruku.", new TimeSpan(0, 14, 0, 0, 0), null, null, "Marko Blažević", null, "https://www.instagram.com/workshop.zagreb/", false, null, 8, "Keramika za sve", 45m, "keramika-za-sve", new TimeSpan(0, 11, 0, 0, 0) },
                    { 3, "/images/prostor.jpg", new DateTime(2026, 6, 4, 13, 19, 36, 451, DateTimeKind.Utc).AddTicks(7792), new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Local), "Naučite plesti makramé uzlove i izradite vlastiti zidni ukras.", null, null, null, null, null, "https://www.instagram.com/workshop.zagreb/", false, null, 10, "Makramé osnove", 30m, "makrame-osnove", new TimeSpan(0, 16, 0, 0, 0) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_Email",
                table: "Subscribers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkshopPhotos_WorkshopId",
                table: "WorkshopPhotos",
                column: "WorkshopId");

            migrationBuilder.CreateIndex(
                name: "IX_Workshops_Slug",
                table: "Workshops",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservedDays");

            migrationBuilder.DropTable(
                name: "Subscribers");

            migrationBuilder.DropTable(
                name: "WorkshopPhotos");

            migrationBuilder.DropTable(
                name: "Workshops");
        }
    }
}
