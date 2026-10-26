using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Application.Auditing;

/// <summary>
/// What an audited action was performed on.
/// </summary>
/// <param name="Type">The kind of thing, e.g. <c>ServiceRequest</c>.</param>
/// <param name="Id">Its identifier.</param>
/// <param name="PropertyId">The property it belongs to, if any.</param>
public sealed record AuditTarget(string Type, string Id, PropertyId? PropertyId = null);
