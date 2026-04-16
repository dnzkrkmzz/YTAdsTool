using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YTReklamAraci.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SearchCount",
                table: "SearchCaches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchCount",
                table: "SearchCaches");
        }
    }
}
