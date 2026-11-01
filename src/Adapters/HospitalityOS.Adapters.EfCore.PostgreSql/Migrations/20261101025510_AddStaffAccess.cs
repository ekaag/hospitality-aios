using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalityOS.Adapters.EfCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffAccess",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    InviteCodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InviteExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SignInIssuer = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    SignInSubject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffAccess", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAccess_InviteCodeHash",
                table: "StaffAccess",
                column: "InviteCodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffAccess_SignInIssuer_SignInSubject",
                table: "StaffAccess",
                columns: new[] { "SignInIssuer", "SignInSubject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffAccess_StaffMemberId",
                table: "StaffAccess",
                column: "StaffMemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffAccess_TenantId",
                table: "StaffAccess",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffAccess");
        }
    }
}
