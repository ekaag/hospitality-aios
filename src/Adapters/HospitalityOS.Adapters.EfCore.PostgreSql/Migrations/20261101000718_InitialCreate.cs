using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HospitalityOS.Adapters.EfCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccommodationTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Details_Description = table.Column<string>(type: "text", nullable: true),
                    Details_MaxOccupancy = table.Column<int>(type: "integer", nullable: false),
                    Details_Name = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccommodationUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccommodationTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UnitNumberKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationUnits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AITraces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggerType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TriggerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Intent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    RecencyKey = table.Column<string>(type: "character varying(52)", maxLength: 52, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AITraces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Amenities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationSpaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details_Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details_Description = table.Column<string>(type: "text", nullable: true),
                    Details_Name = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Amenities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Position = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActorRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TargetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TargetId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Position);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityInvocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapabilityName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CapabilityVersion = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActorRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Output = table.Column<string>(type: "text", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SucceededIdempotencyKey = table.Column<string>(type: "character varying(265)", maxLength: 265, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityInvocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details_About = table.Column<string>(type: "text", nullable: true),
                    Details_Body = table.Column<string>(type: "text", nullable: false),
                    Details_Tags = table.Column<string>(type: "text", nullable: true),
                    Details_Title = table.Column<string>(type: "text", nullable: false),
                    Details_Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: true),
                    StayContextId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventStore",
                columns: table => new
                {
                    Position = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventVersion = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Envelope = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventStore", x => x.Position);
                });

            migrationBuilder.CreateTable(
                name: "ExternalReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CanonicalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuestMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: true),
                    StayContextId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sender = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ClientMessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Intent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuestNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    SubjectType = table.Column<string>(type: "text", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderName = table.Column<string>(type: "text", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Profile_DisplayName = table.Column<string>(type: "text", nullable: true),
                    Profile_PreferredLanguage = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HumanActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Assignments = table.Column<string>(type: "text", nullable: false),
                    Completion_CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Completion_CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Completion_Evidence = table.Column<string>(type: "text", nullable: true),
                    Completion_Note = table.Column<string>(type: "text", nullable: true),
                    Details_ActionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details_CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details_Deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Details_Instructions = table.Column<string>(type: "text", nullable: false),
                    Details_Items = table.Column<string>(type: "text", nullable: true),
                    Details_Location = table.Column<string>(type: "text", nullable: false),
                    Details_Priority = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details_Reason = table.Column<string>(type: "text", nullable: false),
                    Details_RequestedByAgent = table.Column<string>(type: "text", nullable: false),
                    Details_ServiceRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details_SkillsRequired = table.Column<string>(type: "text", nullable: true),
                    Details_Sla = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Details_SourceEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HumanActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inbox",
                columns: table => new
                {
                    Handler = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbox", x => new { x.Handler, x.EventId });
                });

            migrationBuilder.CreateTable(
                name: "IntegrationConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Settings = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Menus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Details_Description = table.Column<string>(type: "text", nullable: true),
                    Details_Items = table.Column<string>(type: "text", nullable: false),
                    Details_Name = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperatingHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Schedule = table.Column<string>(type: "text", nullable: false),
                    SubjectKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Subject_Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject_Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingHours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventVersion = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Envelope = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeadLetteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Sequence);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details_AppliesTo = table.Column<string>(type: "text", nullable: true),
                    Details_Content = table.Column<string>(type: "text", nullable: false),
                    Details_EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    Details_EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Details_Title = table.Column<string>(type: "text", nullable: false),
                    Details_Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Details_Address = table.Column<string>(type: "text", nullable: false),
                    Details_ContactEmail = table.Column<string>(type: "text", nullable: true),
                    Details_ContactPhone = table.Column<string>(type: "text", nullable: true),
                    Details_Name = table.Column<string>(type: "text", nullable: false),
                    Details_TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PropertyEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details_Audience = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details_BookingRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Details_Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details_Description = table.Column<string>(type: "text", nullable: true),
                    Details_LocalEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Details_LocalStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Details_LocationSpaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details_Price = table.Column<string>(type: "text", nullable: true),
                    Details_Title = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Restaurants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Details_Cuisine = table.Column<string>(type: "text", nullable: true),
                    Details_Description = table.Column<string>(type: "text", nullable: true),
                    Details_LocationSpaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details_Name = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Restaurants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceCategory = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExceptionReason = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details_Description = table.Column<string>(type: "text", nullable: true),
                    Details_ItemCode = table.Column<string>(type: "text", nullable: true),
                    Details_Location = table.Column<string>(type: "text", nullable: false),
                    Details_Quantity = table.Column<int>(type: "integer", nullable: false),
                    Origin_ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Origin_GuestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Origin_SourceMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Origin_StayContextId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Details_Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details_Code = table.Column<string>(type: "text", nullable: false),
                    Details_Description = table.Column<string>(type: "text", nullable: true),
                    Details_IsGuestRequestable = table.Column<bool>(type: "boolean", nullable: false),
                    Details_Items = table.Column<string>(type: "text", nullable: true),
                    Details_Name = table.Column<string>(type: "text", nullable: false),
                    Details_TargetCompletion = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Spaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentSpaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details_Code = table.Column<string>(type: "text", nullable: true),
                    Details_Description = table.Column<string>(type: "text", nullable: true),
                    Details_Directions = table.Column<string>(type: "text", nullable: true),
                    Details_Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details_Name = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsOnDuty = table.Column<bool>(type: "boolean", nullable: false),
                    Details_DisplayName = table.Column<string>(type: "text", nullable: false),
                    Details_Skills = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StayContexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Arrival = table.Column<DateOnly>(type: "date", nullable: false),
                    Departure = table.Column<DateOnly>(type: "date", nullable: false),
                    AccommodationUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StayContexts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationTypes_TenantId",
                table: "AccommodationTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationUnits_PropertyId_UnitNumberKey",
                table: "AccommodationUnits",
                columns: new[] { "PropertyId", "UnitNumberKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationUnits_TenantId",
                table: "AccommodationUnits",
                column: "TenantId");

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

            migrationBuilder.CreateIndex(
                name: "IX_Amenities_PropertyId",
                table: "Amenities",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Amenities_TenantId",
                table: "Amenities",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_AuditId",
                table: "AuditLog",
                column: "AuditId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CorrelationId",
                table: "AuditLog",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TenantId_Position",
                table: "AuditLog",
                columns: new[] { "TenantId", "Position" });

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

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_PropertyId",
                table: "ContentItems",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentItems_TenantId",
                table: "ContentItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId",
                table: "Conversations",
                column: "TenantId");

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
                name: "IX_ExternalReferences_PropertyId_EntityType_CanonicalId",
                table: "ExternalReferences",
                columns: new[] { "PropertyId", "EntityType", "CanonicalId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalReferences_PropertyId_ProviderName_EntityType_Exter~",
                table: "ExternalReferences",
                columns: new[] { "PropertyId", "ProviderName", "EntityType", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalReferences_TenantId",
                table: "ExternalReferences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestMessages_ConversationId_ClientMessageId",
                table: "GuestMessages",
                columns: new[] { "ConversationId", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestMessages_ConversationId_SentAt",
                table: "GuestMessages",
                columns: new[] { "ConversationId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestMessages_TenantId",
                table: "GuestMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestNotifications_Key",
                table: "GuestNotifications",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestNotifications_TenantId",
                table: "GuestNotifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_TenantId",
                table: "Guests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HumanActions_PropertyId_Status",
                table: "HumanActions",
                columns: new[] { "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HumanActions_TenantId",
                table: "HumanActions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationConfigurations_PropertyId_Kind",
                table: "IntegrationConfigurations",
                columns: new[] { "PropertyId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationConfigurations_TenantId",
                table: "IntegrationConfigurations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_RestaurantId",
                table: "Menus",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_TenantId",
                table: "Menus",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatingHours_PropertyId_SubjectKey",
                table: "OperatingHours",
                columns: new[] { "PropertyId", "SubjectKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatingHours_TenantId",
                table: "OperatingHours",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_TenantId",
                table: "Organizations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_EventId",
                table: "Outbox",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Policies_PropertyId",
                table: "Policies",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_TenantId",
                table: "Policies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_TenantId",
                table: "Properties",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyEvents_PropertyId",
                table: "PropertyEvents",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyEvents_TenantId",
                table: "PropertyEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_PropertyId",
                table: "Restaurants",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_TenantId",
                table: "Restaurants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_SourceMessageId",
                table: "ServiceRequests",
                column: "SourceMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_TenantId",
                table: "ServiceRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_PropertyId_CodeKey",
                table: "Services",
                columns: new[] { "PropertyId", "CodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Services_TenantId",
                table: "Services",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Spaces_PropertyId",
                table: "Spaces",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Spaces_TenantId",
                table: "Spaces",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_PropertyId",
                table: "StaffMembers",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_TenantId",
                table: "StaffMembers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StayContexts_PropertyId_GuestId",
                table: "StayContexts",
                columns: new[] { "PropertyId", "GuestId" });

            migrationBuilder.CreateIndex(
                name: "IX_StayContexts_TenantId",
                table: "StayContexts",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccommodationTypes");

            migrationBuilder.DropTable(
                name: "AccommodationUnits");

            migrationBuilder.DropTable(
                name: "AITraces");

            migrationBuilder.DropTable(
                name: "Amenities");

            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "CapabilityInvocations");

            migrationBuilder.DropTable(
                name: "ContentItems");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "EventStore");

            migrationBuilder.DropTable(
                name: "ExternalReferences");

            migrationBuilder.DropTable(
                name: "GuestMessages");

            migrationBuilder.DropTable(
                name: "GuestNotifications");

            migrationBuilder.DropTable(
                name: "Guests");

            migrationBuilder.DropTable(
                name: "HumanActions");

            migrationBuilder.DropTable(
                name: "Inbox");

            migrationBuilder.DropTable(
                name: "IntegrationConfigurations");

            migrationBuilder.DropTable(
                name: "Menus");

            migrationBuilder.DropTable(
                name: "OperatingHours");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "Outbox");

            migrationBuilder.DropTable(
                name: "Policies");

            migrationBuilder.DropTable(
                name: "Properties");

            migrationBuilder.DropTable(
                name: "PropertyEvents");

            migrationBuilder.DropTable(
                name: "Restaurants");

            migrationBuilder.DropTable(
                name: "ServiceRequests");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "Spaces");

            migrationBuilder.DropTable(
                name: "StaffMembers");

            migrationBuilder.DropTable(
                name: "StayContexts");
        }
    }
}
