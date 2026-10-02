using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChambaIA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrackerHistoryAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemindFor",
                table: "Notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    InterviewDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationEvents_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ApplicationId",
                table: "Notifications",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationEvents_ApplicationId_At",
                table: "ApplicationEvents",
                columns: new[] { "ApplicationId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationEvents");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_ApplicationId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RemindFor",
                table: "Notifications");
        }
    }
}
