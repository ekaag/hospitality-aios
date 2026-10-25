using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.ServiceRequests.Events;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Services.ServiceRequests;

public sealed class ServiceRequestTests
{
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209913001"));
    private static readonly PropertyId propertyId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209913002"));
    private static readonly ServiceRequestId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209913003"));
    private static readonly DateTimeOffset now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly ActionLocation room412 = new("Room 412", new AccommodationUnitId(Guid.Parse("01890a5d-ac96-774b-bcce-b30209913004")));

    private static readonly RequestOrigin fromGuest = new(
        new GuestId(Guid.Parse("01890a5d-ac96-774b-bcce-b30209913005")),
        new StayContextId(Guid.Parse("01890a5d-ac96-774b-bcce-b30209913006")),
        new ConversationId(Guid.Parse("01890a5d-ac96-774b-bcce-b30209913007")),
        new GuestMessageId(Guid.Parse("01890a5d-ac96-774b-bcce-b30209913008")));

    [Fact]
    public void Create_TwoTowelsToRoom412_CreatesOpenRequestWithDueTimeAndRaisesCreated()
    {
        Service housekeeping = Housekeeping();
        ServiceRequestDetails towels = new(room412, "bath_towel", 2);

        ServiceRequest request = ServiceRequest.Create(id, housekeeping, towels, fromGuest, now).Value;

        Assert.Equal(ServiceRequestStatus.Open, request.Status);
        Assert.Equal(housekeeping.Id, request.ServiceId);
        Assert.Equal(ServiceCategory.Housekeeping, request.ServiceCategory);
        Assert.Equal(now.AddMinutes(20), request.DueAt);
        Assert.Equal(
            [new ServiceRequestCreated(id, propertyId, housekeeping.Id, ServiceCategory.Housekeeping, towels, fromGuest, now.AddMinutes(20), now)],
            request.DomainEvents);
    }

    [Fact]
    public void Create_Pillow_DefaultsToQuantityOneAndNormalizesItemCode()
    {
        ServiceRequest request = ServiceRequest.Create(id, Housekeeping(), new ServiceRequestDetails(room412, " Pillow "), fromGuest, now).Value;

        Assert.Equal("pillow", request.Details.ItemCode);
        Assert.Equal(1, request.Details.Quantity);
    }

    [Fact]
    public void Create_MaintenanceWithoutItems_NeedsDescription()
    {
        Service maintenance = Service.Create(
            new ServiceId(Guid.Parse("01890a5d-ac96-774b-bcce-b30209913011")), tenantId, propertyId,
            new ServiceDetails("maintenance.repair", "Maintenance", ServiceCategory.Maintenance), now).Value;

        Result<ServiceRequest> missing = ServiceRequest.Create(id, maintenance, new ServiceRequestDetails(room412), fromGuest, now);
        Result<ServiceRequest> described = ServiceRequest.Create(id, maintenance, new ServiceRequestDetails(room412, Description: "The AC isn't working."), fromGuest, now);

        Assert.Equal(("description", "service_request.description_required"), FieldCodes(missing).Single());
        Assert.Equal(ServiceCategory.Maintenance, described.Value.ServiceCategory);
        Assert.Null(described.Value.DueAt);
    }

    [Fact]
    public void Create_WithTooManyTowels_RejectsQuantity()
    {
        Result<ServiceRequest> result = ServiceRequest.Create(id, Housekeeping(), new ServiceRequestDetails(room412, "bath_towel", 5), fromGuest, now);

        Assert.Equal([("quantity", "service_request.quantity_out_of_range")], FieldCodes(result));
    }

    [Fact]
    public void Create_WithUnknownOrMissingItemAndNoLocation_ReportsAll()
    {
        Result<ServiceRequest> unknown = ServiceRequest.Create(id, Housekeeping(), new ServiceRequestDetails(null!, "bathrobe"), fromGuest, now);
        Result<ServiceRequest> missing = ServiceRequest.Create(id, Housekeeping(), new ServiceRequestDetails(room412 with { Label = " " }), fromGuest, now);

        Assert.Equal([("location", "service_request.location_required"), ("itemCode", "service_request.item_unknown")], FieldCodes(unknown));
        Assert.Equal([("location.label", "service_request.location_label_required"), ("itemCode", "service_request.item_required")], FieldCodes(missing));
    }

    [Fact]
    public void Create_ByGuestForNonRequestableService_IsRejected_ButStaffMayRaiseIt()
    {
        Service internalOnly = Service.Create(
            new ServiceId(Guid.Parse("01890a5d-ac96-774b-bcce-b30209913012")), tenantId, propertyId,
            new ServiceDetails("engineering.inspection", "Inspection", ServiceCategory.Maintenance, IsGuestRequestable: false), now).Value;
        ServiceRequestDetails details = new(room412, Description: "Quarterly check");

        Result<ServiceRequest> byGuest = ServiceRequest.Create(id, internalOnly, details, fromGuest, now);
        Result<ServiceRequest> byStaff = ServiceRequest.Create(id, internalOnly, details, RequestOrigin.Staff, now);

        Assert.Equal([("serviceId", "service_request.service_not_requestable")], FieldCodes(byGuest));
        Assert.True(byStaff.IsSuccess);
    }

    [Fact]
    public void Lifecycle_InProgressThenCompleted_RaisesUpdatedThenCompletedOnce()
    {
        ServiceRequest request = CreateTowels();

        request.MarkInProgress(now.AddMinutes(5));
        request.MarkInProgress(now.AddMinutes(6));
        request.Complete(now.AddMinutes(10));
        request.Complete(now.AddMinutes(11));

        Assert.Equal(ServiceRequestStatus.Completed, request.Status);
        Assert.Equal(now.AddMinutes(10), request.CompletedAt);
        Assert.Equal(
            [
                new ServiceRequestUpdated(id, ServiceRequestStatus.InProgress, null, now.AddMinutes(5)),
                new ServiceRequestCompleted(id, propertyId, fromGuest, now.AddMinutes(10)),
            ],
            request.DomainEvents);
    }

    [Fact]
    public void RecordException_KeepsRequestUnresolved_AndWorkCanRestart()
    {
        ServiceRequest request = CreateTowels();

        request.RecordException("Worker could not access the room", now.AddMinutes(5));
        request.RecordException(" Worker could not access the room ", now.AddMinutes(6));

        Assert.Equal(ServiceRequestStatus.Exception, request.Status);
        Assert.True(request.IsActive);
        Assert.Single(request.DomainEvents);

        request.MarkInProgress(now.AddMinutes(30));
        Assert.Equal(ServiceRequestStatus.InProgress, request.Status);
    }

    [Fact]
    public void FinalStates_RejectFurtherTransitions()
    {
        ServiceRequest completed = CreateTowels();
        completed.Complete(now);
        ServiceRequest cancelled = CreateTowels();
        cancelled.Cancel(now);

        Assert.Equal("service_request.final", completed.Cancel(now).Error.Code);
        Assert.Equal("service_request.final", completed.MarkInProgress(now).Error.Code);
        Assert.Equal("service_request.final", completed.RecordException("late", now).Error.Code);
        Assert.Equal("service_request.final", cancelled.Complete(now).Error.Code);
        Assert.True(cancelled.Cancel(now).IsSuccess);
    }

    private static ServiceRequest CreateTowels()
    {
        ServiceRequest request = ServiceRequest.Create(id, Housekeeping(), new ServiceRequestDetails(room412, "bath_towel", 2), fromGuest, now).Value;
        request.DequeueDomainEvents();

        return request;
    }

    private static Service Housekeeping() => Service.Create(
        new ServiceId(Guid.Parse("01890a5d-ac96-774b-bcce-b30209913010")),
        tenantId,
        propertyId,
        new ServiceDetails(
            "housekeeping.amenities",
            "Housekeeping amenities",
            ServiceCategory.Housekeeping,
            Items: [new ServiceItem("bath_towel", "Bath towel", 4), new ServiceItem("pillow", "Pillow", 2)],
            TargetCompletion: TimeSpan.FromMinutes(20)),
        now).Value;

    private static List<(string Field, string Code)> FieldCodes(Result result)
    {
        ValidationError error = Assert.IsType<ValidationError>(result.Error);

        return [.. error.Fields.Select(field => (field.Field, field.Code))];
    }
}
