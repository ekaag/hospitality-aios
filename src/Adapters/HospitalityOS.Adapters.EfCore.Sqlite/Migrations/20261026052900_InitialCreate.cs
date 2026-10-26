using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalityOS.Adapters.EfCore.Sqlite.Migrations
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Details_Description = table.Column<string>(type: "TEXT", nullable: true),
                    Details_MaxOccupancy = table.Column<int>(type: "INTEGER", nullable: false),
                    Details_Name = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccommodationUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccommodationTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    UnitNumberKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationUnits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Amenities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationSpaceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Details_Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Details_Description = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Name = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Amenities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Details_About = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Body = table.Column<string>(type: "TEXT", nullable: false),
                    Details_Tags = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Title = table.Column<string>(type: "TEXT", nullable: false),
                    Details_Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    GuestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StayContextId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuestMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StayContextId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Sender = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ClientMessageId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SentAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Profile_DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Profile_PreferredLanguage = table.Column<string>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HumanActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Deadline = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    AcceptedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true),
                    EndedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Assignments = table.Column<string>(type: "TEXT", nullable: false),
                    Completion_CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Completion_CompletedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    Completion_Evidence = table.Column<string>(type: "TEXT", nullable: true),
                    Completion_Note = table.Column<string>(type: "TEXT", nullable: true),
                    Details_ActionType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Details_Deadline = table.Column<long>(type: "INTEGER", nullable: true),
                    Details_Instructions = table.Column<string>(type: "TEXT", nullable: false),
                    Details_Items = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Location = table.Column<string>(type: "TEXT", nullable: false),
                    Details_Priority = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Details_Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Details_RequestedByAgent = table.Column<string>(type: "TEXT", nullable: false),
                    Details_ServiceRequestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Details_SkillsRequired = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Sla = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Details_SourceEventId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HumanActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Menus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Details_Description = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Items = table.Column<string>(type: "TEXT", nullable: false),
                    Details_Name = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperatingHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Schedule = table.Column<string>(type: "TEXT", nullable: false),
                    SubjectKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Subject_Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Subject_Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingHours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Details_AppliesTo = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Content = table.Column<string>(type: "TEXT", nullable: false),
                    Details_EffectiveFrom = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Details_EffectiveTo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Details_Title = table.Column<string>(type: "TEXT", nullable: false),
                    Details_Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Details_Address = table.Column<string>(type: "TEXT", nullable: false),
                    Details_ContactEmail = table.Column<string>(type: "TEXT", nullable: true),
                    Details_ContactPhone = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Name = table.Column<string>(type: "TEXT", nullable: false),
                    Details_TimeZoneId = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PropertyEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Details_Audience = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Details_BookingRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    Details_Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Details_Description = table.Column<string>(type: "TEXT", nullable: true),
                    Details_LocalEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Details_LocalStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Details_LocationSpaceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Details_Price = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Title = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Restaurants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Details_Cuisine = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Description = table.Column<string>(type: "TEXT", nullable: true),
                    Details_LocationSpaceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Details_Name = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Restaurants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServiceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServiceCategory = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DueAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExceptionReason = table.Column<string>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    SourceMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Details_Description = table.Column<string>(type: "TEXT", nullable: true),
                    Details_ItemCode = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Location = table.Column<string>(type: "TEXT", nullable: false),
                    Details_Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Origin_ConversationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Origin_GuestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Origin_SourceMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Origin_StayContextId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CodeKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Details_Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Details_Code = table.Column<string>(type: "TEXT", nullable: false),
                    Details_Description = table.Column<string>(type: "TEXT", nullable: true),
                    Details_IsGuestRequestable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Details_Items = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Name = table.Column<string>(type: "TEXT", nullable: false),
                    Details_TargetCompletion = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Spaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentSpaceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Details_Code = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Description = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Directions = table.Column<string>(type: "TEXT", nullable: true),
                    Details_Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Details_Name = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StayContexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Arrival = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Departure = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AccommodationUnitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
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
                name: "IX_Amenities_PropertyId",
                table: "Amenities",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Amenities_TenantId",
                table: "Amenities",
                column: "TenantId");

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
                name: "IX_Guests_TenantId",
                table: "Guests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HumanActions_TenantId",
                table: "HumanActions",
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
                name: "Amenities");

            migrationBuilder.DropTable(
                name: "ContentItems");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "GuestMessages");

            migrationBuilder.DropTable(
                name: "Guests");

            migrationBuilder.DropTable(
                name: "HumanActions");

            migrationBuilder.DropTable(
                name: "Menus");

            migrationBuilder.DropTable(
                name: "OperatingHours");

            migrationBuilder.DropTable(
                name: "Organizations");

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
                name: "StayContexts");
        }
    }
}
