using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Positions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Positions"
                SET "CreatedAt" = CURRENT_TIMESTAMP;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Positions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_CreatedAt_Id",
                table: "Positions",
                columns: new[] { "CreatedAt", "Id" },
                descending: new[] { true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Positions_CreatedAt_Id",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Positions");
        }
    }
}
