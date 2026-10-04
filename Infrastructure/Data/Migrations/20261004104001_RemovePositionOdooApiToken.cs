using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePositionOdooApiToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Positions_OdooApiTokenHash",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "OdooApiTokenHash",
                table: "Positions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OdooApiTokenHash",
                table: "Positions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_OdooApiTokenHash",
                table: "Positions",
                column: "OdooApiTokenHash",
                unique: true);
        }
    }
}
