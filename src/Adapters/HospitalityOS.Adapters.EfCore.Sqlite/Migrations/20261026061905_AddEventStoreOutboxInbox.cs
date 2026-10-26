using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalityOS.Adapters.EfCore.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddEventStoreOutboxInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventStore",
                columns: table => new
                {
                    Position = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EventVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AggregateType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AggregateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecordedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Envelope = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventStore", x => x.Position);
                });

            migrationBuilder.CreateTable(
                name: "Inbox",
                columns: table => new
                {
                    Handler = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbox", x => new { x.Handler, x.EventId });
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EventVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AggregateType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AggregateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecordedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Envelope = table.Column<string>(type: "TEXT", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    NextAttemptAt = table.Column<long>(type: "INTEGER", nullable: true),
                    LeaseUntil = table.Column<long>(type: "INTEGER", nullable: true),
                    DeadLetteredAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Sequence);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventStore_CorrelationId",
                table: "EventStore",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_EventStore_EventId",
                table: "EventStore",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventStore_RecordedAt",
                table: "EventStore",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_EventId",
                table: "Outbox",
                column: "EventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventStore");

            migrationBuilder.DropTable(
                name: "Inbox");

            migrationBuilder.DropTable(
                name: "Outbox");
        }
    }
}
