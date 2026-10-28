using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalityOS.Adapters.EfCore.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAITraces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AITraces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActorType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CausationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TriggerType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TriggerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Intent = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    RecencyKey = table.Column<string>(type: "TEXT", maxLength: 52, nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AITraces", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AITraces_CorrelationId",
                table: "AITraces",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AITraces_TenantId",
                table: "AITraces",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AITraces_TenantId_RecencyKey",
                table: "AITraces",
                columns: new[] { "TenantId", "RecencyKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AITraces");
        }
    }
}
