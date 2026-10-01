using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChambaIA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OnboardingCompletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardingCompletedAt",
                table: "CandidateProfiles",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnboardingCompletedAt",
                table: "CandidateProfiles");
        }
    }
}
