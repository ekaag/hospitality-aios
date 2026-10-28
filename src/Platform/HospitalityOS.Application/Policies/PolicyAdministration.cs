using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Api.Policies;
using HospitalityOS.Domain.Properties.Policies;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using ContractStatus = HospitalityOS.Contracts.Api.Policies.PolicyStatus;
using ContractSubject = HospitalityOS.Contracts.Api.Properties.SubjectReference;
using ContractType = HospitalityOS.Contracts.Capabilities.Properties.PolicyType;
using DomainType = HospitalityOS.Domain.Properties.Policies.PolicyType;

namespace HospitalityOS.Application.Policies;

internal sealed class PolicyAdministration(
    IPolicyRepository policies,
    PropertyReferences references,
    ICurrentTenant currentTenant,
    AuditedCommit commit,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : IPolicyAdministration
{
    public async Task<Result<PolicyResponse>> CreateAsync(CreatePolicyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PropertyId> property = await references.PropertyAsync(request.PropertyId, "policy", cancellationToken).ConfigureAwait(false);

        if (property.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.PolicyCreate, property.Error, null, cancellationToken).ConfigureAwait(false);
        }

        RequestFields fields = new();
        PolicyDetails details = await DetailsAsync(
            property.Value, request.Type, request.Title, request.Content, request.EffectiveFrom, request.EffectiveTo, request.AppliesTo, fields, cancellationToken).ConfigureAwait(false);
        PolicyId id = new(idGenerators.Resolve().NewId());
        Result<Policy> created = fields.Merge(Policy.Create(id, currentTenant.TenantId, property.Value, details, timeProvider.GetUtcNow()));

        return await commit.CreateAsync(
            AdministrationActions.PolicyCreate,
            created,
            policy => policies.AddAsync(policy, cancellationToken),
            policy => Target(policy.Id.Value, policy.PropertyId),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PolicyResponse>> UpdateAsync(Guid id, UpdatePolicyRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await ChangeAsync(
            AdministrationActions.PolicyUpdate,
            id,
            expectedVersion,
            async policy =>
            {
                RequestFields fields = new();
                PolicyDetails details = await DetailsAsync(
                    policy.PropertyId, request.Type, request.Title, request.Content, request.EffectiveFrom, request.EffectiveTo, request.AppliesTo, fields, cancellationToken).ConfigureAwait(false);

                return fields.Apply(() => policy.Update(details, timeProvider.GetUtcNow()));
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<PolicyResponse>> ActivateAsync(Guid id, long? expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(AdministrationActions.PolicyActivate, id, expectedVersion, policy => Task.FromResult(policy.Activate(timeProvider.GetUtcNow())), cancellationToken);

    public Task<Result<PolicyResponse>> RetireAsync(Guid id, long? expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(AdministrationActions.PolicyRetire, id, expectedVersion, policy => Task.FromResult(policy.Retire(timeProvider.GetUtcNow())), cancellationToken);

    public async Task<Result<PolicyResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Policy? policy = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return policy is null ? AdministrationErrors.PolicyNotFound : Response(policy);
    }

    public async Task<IReadOnlyList<PolicyResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Policy> found = await policies.ListForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        return [.. found.OrderBy(policy => policy.Details.Title, StringComparer.OrdinalIgnoreCase).Select(Response)];
    }

    private async Task<Result<PolicyResponse>> ChangeAsync(string action, Guid id, long? expectedVersion, Func<Policy, Task<Result>> change, CancellationToken cancellationToken)
    {
        Policy? policy = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return await commit.ChangeAsync(
            action,
            Target(id, policy?.PropertyId),
            policy,
            AdministrationErrors.PolicyNotFound,
            expectedVersion,
            change,
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    // An empty GUID cannot be a policy ID, so it names no policy.
    private Task<Policy?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? Task.FromResult<Policy?>(null) : policies.GetByIdAsync(new PolicyId(id), cancellationToken);

    private static AuditTarget Target(Guid id, PropertyId? propertyId) => new(nameof(Policy), id.ToString(), propertyId);

    // The domain validates every field, including missing text, so absent values pass through as null.
    private async Task<PolicyDetails> DetailsAsync(
        PropertyId propertyId,
        ContractType? type,
        string? title,
        string? content,
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo,
        ContractSubject? appliesTo,
        RequestFields fields,
        CancellationToken cancellationToken) =>
        new(
            fields.Required<DomainType>(type, "type", "policy.type_required"),
            title!,
            content!,
            effectiveFrom,
            effectiveTo,
            await references.SubjectAsync(propertyId, appliesTo, "appliesTo", "policy.applies_to", fields, cancellationToken).ConfigureAwait(false));

    private static PolicyResponse Response(Policy policy)
    {
        PolicyDetails details = policy.Details;

        return new PolicyResponse(
            policy.Id.Value,
            policy.PropertyId,
            EnumMirror.To<ContractType>(details.Type),
            details.Title,
            details.Content,
            details.EffectiveFrom,
            details.EffectiveTo,
            Subjects.ToContractOrNull(details.AppliesTo),
            EnumMirror.To<ContractStatus>(policy.Status),
            policy.Version);
    }
}
