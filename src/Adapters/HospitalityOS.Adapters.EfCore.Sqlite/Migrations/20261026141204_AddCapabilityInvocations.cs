using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalityOS.Adapters.EfCore.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityInvocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorRole",
                table: "AuditLog",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CapabilityInvocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapabilityName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CapabilityVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    InputHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActorType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ActorRole = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CausationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Output = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RequestedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    SucceededIdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 265, nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityInvocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityInvocations_CorrelationId",
                table: "CapabilityInvocations",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityInvocations_TenantId",
                table: "CapabilityInvocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityInvocations_TenantId_SucceededIdempotencyKey",
                table: "CapabilityInvocations",
                columns: new[] { "TenantId", "SucceededIdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapabilityInvocations");

            migrationBuilder.DropColumn(
                name: "ActorRole",
                table: "AuditLog");
        }
    }
}
