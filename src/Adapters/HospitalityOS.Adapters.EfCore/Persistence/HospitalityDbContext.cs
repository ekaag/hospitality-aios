using System.Linq.Expressions;
using HospitalityOS.Adapters.EfCore.Conversions;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HospitalityOS.Adapters.EfCore.Persistence;

/// <summary>
/// The platform's database: one table per aggregate, mapped onto the domain types themselves. Every query is
/// filtered to the current tenant (P10). Scoped: one per request, like the unit of work that saves it.
/// </summary>
internal sealed class HospitalityDbContext(
    DbContextOptions<HospitalityDbContext> options,
    ICurrentTenant currentTenant,
    IEnumerable<IProviderModelConventions> providerConventions) : DbContext(options)
{
    /// <summary>
    /// Read by the tenant query filters on every query, so each scope sees only its tenant.
    /// </summary>
    internal TenantId CurrentTenantId => currentTenant.TenantId;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        KeyColumns.Apply(ChangeTracker);

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        KeyColumns.Apply(ChangeTracker);

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        foreach (Type idType in DomainModel.TypedIdTypes)
        {
            configurationBuilder.Properties(idType).HaveConversion(typeof(TypedIdConverter<>).MakeGenericType(idType));
        }

        // Enums as names, so reordering enum members never changes stored data.
        foreach (Type enumType in DomainModel.EnumTypes)
        {
            configurationBuilder.Properties(enumType).HaveConversion<string>().HaveMaxLength(64);
        }

        foreach (IProviderModelConventions conventions in providerConventions)
        {
            conventions.Configure(configurationBuilder);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HospitalityDbContext).Assembly);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes().Where(type => typeof(ITenantScoped).IsAssignableFrom(type.ClrType)))
        {
            entityType.SetQueryFilter(TenantFilter(entityType.ClrType));
        }
    }

    private LambdaExpression TenantFilter(Type entityType)
    {
        // entity => entity.TenantId == this.CurrentTenantId; EF Core re-reads the property for each query.
        ParameterExpression entity = Expression.Parameter(entityType, "entity");
        BinaryExpression sameTenant = Expression.Equal(
            Expression.Property(entity, nameof(ITenantScoped.TenantId)),
            Expression.Property(Expression.Constant(this), nameof(CurrentTenantId)));

        return Expression.Lambda(sameTenant, entity);
    }
}
