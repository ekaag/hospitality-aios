using HospitalityOS.Ai.Tracing;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ai.Classifying;

/// <summary>
/// Runs classifications (see <see cref="ILabelClassifier"/>).
/// </summary>
internal sealed class LabelClassifier(IInferenceRouter router, AITraceRecorder recorder) : ILabelClassifier
{
    public async Task<Result<string>> ClassifyAsync(string task, string input, IReadOnlyList<string> labels, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(labels);

        string? instructions = null;
        Result<InferenceOutcome<Classification>> outcome = await router.RunAsync<IClassifier, Classification>(
            task,
            (classifier, model, prompt, token) =>
            {
                instructions = (prompt ?? throw new InvalidOperationException($"Classification task '{task}' has no prompt; declare it with AddClassification."))
                    .Render(new Dictionary<string, string>());

                return classifier.ClassifyAsync(new ClassificationRequest(instructions, input, ValueList.From(labels)), model, token);
            },
            cancellationToken).ConfigureAwait(false);

        if (outcome.IsFailure)
        {
            recorder.RecordClassification(task, instructions, input, null, outcome.Error is InferenceUnavailableError unavailable ? unavailable.Attempts : []);

            return outcome.Error;
        }

        Classification classification = outcome.Value.Value;
        recorder.RecordClassification(task, instructions, input, classification, outcome.Value.Attempts);

        return labels.Contains(classification.Label, StringComparer.Ordinal) ? classification.Label : AiErrors.UnknownLabel(task, classification.Label);
    }
}
