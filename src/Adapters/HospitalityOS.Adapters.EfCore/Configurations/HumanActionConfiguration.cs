using HospitalityOS.Adapters.EfCore.Conversions;
using HospitalityOS.Domain.Operations.HumanActions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class HumanActionConfiguration : IEntityTypeConfiguration<HumanAction>
{
    public void Configure(EntityTypeBuilder<HumanAction> builder)
    {
        builder.MapAggregate("HumanActions");
        builder.Property(action => action.PropertyId);
        builder.Property(action => action.Deadline);
        builder.Property(action => action.Status);
        builder.Property(action => action.CreatedAt);
        builder.Property(action => action.AcceptedAt);
        builder.Property(action => action.StartedAt);
        builder.Property(action => action.FailureReason);
        builder.Property(action => action.EndedAt);
        builder.HasIndex(nameof(HumanAction.PropertyId), nameof(HumanAction.Status));
        builder.ComplexProperty(action => action.Details, details =>
        {
            details.Property(value => value.Location).HasJsonConversion();
            details.Property(value => value.Items).HasJsonConversion();
            details.Property(value => value.SkillsRequired).HasJsonConversion();
        });
        builder.ComplexProperty(action => action.Completion, completion => completion.Property(value => value.Evidence).HasJsonConversion());

        // The assignment history lives in a private list; Assignments and AssignedTo are views of it.
        builder.Property<List<TaskAssignment>>("assignments").HasColumnName("Assignments").HasJsonListConversion();
        builder.Ignore(action => action.Assignments);
        builder.Ignore(action => action.AssignedTo);
    }
}
