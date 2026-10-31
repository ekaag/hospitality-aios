using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalityOS.Adapters.EfCore.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestMessageIntent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Intent",
                table: "GuestMessages",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Intent",
                table: "GuestMessages");
        }
    }
}
