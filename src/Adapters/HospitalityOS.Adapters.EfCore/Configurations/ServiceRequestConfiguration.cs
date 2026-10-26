using HospitalityOS.Adapters.EfCore.Conversions;
using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Services.ServiceRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.MapAggregate("ServiceRequests");
        builder.Property(request => request.PropertyId);
        builder.Property(request => request.ServiceId);
        builder.Property(request => request.ServiceCategory);
        builder.Property(request => request.CreatedAt);
        builder.Property(request => request.DueAt);
        builder.Property(request => request.Status);
        builder.Property(request => request.ExceptionReason);
        builder.Property(request => request.CompletedAt);
        builder.ComplexProperty(request => request.Details, details => details.Property(value => value.Location).HasJsonConversion());
        builder.ComplexProperty(request => request.Origin);
        builder.Property<Guid?>(KeyColumns.SourceMessageId);
        builder.HasIndex(KeyColumns.SourceMessageId).IsUnique();
    }
}
