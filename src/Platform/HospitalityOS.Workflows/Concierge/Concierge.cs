namespace HospitalityOS.Workflows.Concierge;

/// <summary>
/// The concierge's inference task and what it tells guests when it cannot answer from a source.
/// </summary>
public static class Concierge
{
    /// <summary>
    /// Answers a guest's question from authoritative sources; its prompt is <c>Prompts/concierge.answer</c>, and
    /// <c>Inference:Tasks:concierge.answer</c> picks its model.
    /// </summary>
    public const string AnswerTask = "concierge.answer";

    /// <summary>
    /// The read capabilities its model may call, at pinned versions (P7).
    /// </summary>
    public static IReadOnlyList<string> Tools { get; } =
    [
        "Property.GetOperatingHours@1",
        "Property.GetPolicy@1",
        "Property.SearchContent@1",
        "Dining.SearchRestaurants@1",
        "Dining.GetMenu@1",
        "Events.SearchPropertyEvents@1",
    ];

    /// <summary>
    /// The guest's answer when no authoritative source answers the question (P4, Journey 17).
    /// </summary>
    public const string NoSourceAnswer =
        "I don't have confirmed information about that, so I'd rather not guess. The front desk will be glad to help.";

    /// <summary>
    /// The guest's answer when the model cannot be reached or does not finish.
    /// </summary>
    public const string UnavailableAnswer =
        "I'm sorry, I can't answer right now. Please try again in a moment, or ask the front desk.";

    /// <summary>
    /// Where a status check's context lists the guest's requests.
    /// </summary>
    public const string RequestsContext = "requests";

    /// <summary>
    /// What reads them.
    /// </summary>
    public const string RequestStatusCapability = "GuestService.GetRequestStatus";

    /// <summary>
    /// The intent recorded on the concierge's traces of a question.
    /// </summary>
    public const string QuestionIntent = "question";

    /// <summary>
    /// The intent recorded on the concierge's traces of a status check.
    /// </summary>
    public const string StatusIntent = "request_status";
}
