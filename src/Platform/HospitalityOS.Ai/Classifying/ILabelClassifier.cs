using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ai.Classifying;

/// <summary>
/// Picks one label for a text through a classification task (declared with <c>AddClassification</c>), e.g. a guest
/// message's intent. The task's prompt describes each label; a label the model was not offered is refused. Every call is
/// recorded on the scope's AI trace. Scoped.
/// </summary>
public interface ILabelClassifier
{
    /// <summary>
    /// Classifies <paramref name="input"/> as one of <paramref name="labels"/>.
    /// </summary>
    /// <param name="task">The classification task, e.g. <c>intent.classify</c>.</param>
    /// <param name="input">The text, e.g. the guest's message.</param>
    /// <param name="labels">The labels offered, each described in the task's prompt.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The label chosen, or <c>inference.unavailable</c> or <see cref="AiErrors.UnknownLabelCode"/>.</returns>
    Task<Result<string>> ClassifyAsync(string task, string input, IReadOnlyList<string> labels, CancellationToken cancellationToken);
}
