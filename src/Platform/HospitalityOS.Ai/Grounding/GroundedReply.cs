using System.ComponentModel;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ai.Grounding;

/// <summary>
/// Whether the sources answer the question.
/// </summary>
internal enum GroundedReplyStatus
{
    Answered,
    Unknown,
}

/// <summary>
/// The shape the model's final reply must take (its JSON Schema is sent as the response schema).
/// </summary>
[Description("Your final reply to the guest.")]
internal sealed record GroundedReply(
    [property: Description("Answered when the sources you were given or retrieved answer the question; Unknown when they do not.")]
    GroundedReplyStatus Status,
    [property: Description("Your reply to the guest, using only what the sources say.")]
    string Answer,
    [property: Description("The sources your answer relies on, copied exactly (type, id, version) from the context or tool results; empty when Unknown.")]
    ValueList<SourceReference> Citations);
