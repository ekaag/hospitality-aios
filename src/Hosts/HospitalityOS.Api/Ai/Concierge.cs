namespace HospitalityOS.Api.Ai;

/// <summary>
/// The concierge's inference tasks.
/// </summary>
internal static class Concierge
{
    /// <summary>
    /// Answers a guest's question from authoritative sources; its prompt is <c>Prompts/concierge.answer</c>.
    /// </summary>
    public const string AnswerTask = "concierge.answer";
}
