using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChambaIA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IngestionDedup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "JobSources",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DuplicateOfId",
                table: "JobOffers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_DuplicateOfId",
                table: "JobOffers",
                column: "DuplicateOfId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobOffers_JobOffers_DuplicateOfId",
                table: "JobOffers",
                column: "DuplicateOfId",
                principalTable: "JobOffers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobOffers_JobOffers_DuplicateOfId",
                table: "JobOffers");

            migrationBuilder.DropIndex(
                name: "IX_JobOffers_DuplicateOfId",
                table: "JobOffers");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "JobSources");

            migrationBuilder.DropColumn(
                name: "DuplicateOfId",
                table: "JobOffers");
        }
    }
}
