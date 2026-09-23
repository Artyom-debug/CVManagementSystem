using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeletedCVStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"CVs\" WHERE \"Status\" = 'Deleted';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CVs_Status",
                table: "CVs",
                sql: "\"Status\" IN ('Draft', 'Published')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CVs_Status",
                table: "CVs");
        }
    }
}
