using HospitalityOS.Contracts.Api.Conversations;

namespace HospitalityOS.Application.Conversations;

/// <summary>
/// A guest's message as recorded.
/// </summary>
/// <param name="Message">The message.</param>
/// <param name="Created">Whether this call recorded it; <c>false</c> when its client message ID was already recorded.</param>
public sealed record PostedMessage(GuestMessageResponse Message, bool Created);
