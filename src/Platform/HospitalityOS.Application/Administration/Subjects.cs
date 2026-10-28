using HospitalityOS.Contracts;
using HospitalityOS.Domain.Properties.Shared;
using ContractKind = HospitalityOS.Contracts.Capabilities.SubjectKind;
using ContractSubject = HospitalityOS.Contracts.Api.Properties.SubjectReference;

namespace HospitalityOS.Application.Administration;

/// <summary>
/// A domain <see cref="SubjectReference"/> in API form.
/// </summary>
internal static class Subjects
{
    public static ContractSubject ToContract(SubjectReference subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return new ContractSubject(EnumMirror.To<ContractKind>(subject.Kind), subject.Id);
    }

    public static ContractSubject? ToContractOrNull(SubjectReference? subject) => subject is null ? null : ToContract(subject);
}
