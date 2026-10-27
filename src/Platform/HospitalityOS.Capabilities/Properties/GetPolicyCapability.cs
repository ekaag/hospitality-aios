using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Domain.Properties.Policies;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using ContractPolicyType = HospitalityOS.Contracts.Capabilities.Properties.PolicyType;
using ContractSubjectKind = HospitalityOS.Contracts.Capabilities.SubjectKind;

namespace HospitalityOS.Capabilities.Properties;

/// <summary>
/// <c>Property.GetPolicy</c>: the active policies in effect today, by topic and subject (Journey 6). The agent answers
/// policy questions only from these texts; an empty result means there is no authoritative policy.
/// </summary>
internal sealed class GetPolicyCapability(
    IPropertyRepository properties,
    IPolicyRepository policies,
    TimeProvider timeProvider) : ICapability<GetPolicyInput, GetPolicyOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = ReadCapability.Describe(
        "Property.GetPolicy",
        "Returns the property's policies in effect today (children, pets, smoking, check-in, cancellation, dress code, ...), "
            + "optionally on one topic and for one amenity, restaurant, space, or service (property-wide policies are included). "
            + "Answer policy questions only from the returned text; if nothing is returned, say there is no policy on record.");

    public async Task<Result<GetPolicyOutput>> ExecuteAsync(GetPolicyInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        Result<PropertyClock> loaded = await PropertyClock.LoadAsync(properties, input.PropertyId, timeProvider, cancellationToken).ConfigureAwait(false);

        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        DateOnly today = loaded.Value.Today;
        IReadOnlyList<Policy> active = await policies.ListActiveForPropertyAsync(input.PropertyId, cancellationToken).ConfigureAwait(false);

        IEnumerable<Policy> matching = active
            .Where(policy => policy.IsInEffectOn(today))
            .Where(policy => input.Type is null || ContractEnum.To<ContractPolicyType>(policy.Details.Type) == input.Type)
            .Where(policy => input.SubjectId is null || policy.Details.AppliesTo is null || policy.Details.AppliesTo.Id == input.SubjectId)
            .OrderBy(policy => policy.Details.AppliesTo is null)
            .ThenBy(policy => policy.Details.Type)
            .ThenBy(policy => policy.Details.Title, StringComparer.OrdinalIgnoreCase);

        return new GetPolicyOutput(ValueList.From(matching.Select(policy => new PolicyEntry(
            Sources.Of(policy),
            ContractEnum.To<ContractPolicyType>(policy.Details.Type),
            policy.Details.Title,
            policy.Details.Content,
            policy.Details.EffectiveFrom,
            policy.Details.EffectiveTo,
            policy.Details.AppliesTo is { } subject ? new Subject(ContractEnum.To<ContractSubjectKind>(subject.Kind), subject.Id) : null))));
    }
}
