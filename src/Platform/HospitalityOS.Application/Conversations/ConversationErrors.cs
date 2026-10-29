using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Conversations;

/// <summary>
/// Expected failures of <see cref="IGuestConversations"/>.
/// </summary>
public static class ConversationErrors
{
    /// <summary>
    /// The conversation does not exist in the caller's tenant, or belongs to another stay: guests see only their own.
    /// </summary>
    public static Error ConversationNotFound { get; } = Error.NotFound("conversation.not_found", "There is no such conversation.");

    /// <summary>
    /// The token's stay does not exist, is not this guest's at this property, or is checked out or cancelled.
    /// </summary>
    public static Error StayNotActive { get; } =
        Error.Forbidden("guest.stay_not_active", "The stay this token was issued for is not active at this property.");
}
