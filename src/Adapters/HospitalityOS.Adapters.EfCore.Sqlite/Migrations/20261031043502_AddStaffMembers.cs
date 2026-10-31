using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalityOS.Adapters.EfCore.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsOnDuty = table.Column<bool>(type: "INTEGER", nullable: false),
                    Details_DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Details_Skills = table.Column<string>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMembers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HumanActions_PropertyId_Status",
                table: "HumanActions",
                columns: new[] { "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_PropertyId",
                table: "StaffMembers",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_TenantId",
                table: "StaffMembers",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffMembers");

            migrationBuilder.DropIndex(
                name: "IX_HumanActions_PropertyId_Status",
                table: "HumanActions");
        }
    }
}
