using System.Text.Json;
using HospitalityOS.Ai.Tracing;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ai.StructuredOutput;

/// <summary>
/// Runs typed extractions (see <see cref="ITypedExtractor"/>).
/// </summary>
internal sealed class TypedExtractor(IInferenceRouter router, AITraceRecorder recorder) : ITypedExtractor
{
    /// <summary>
    /// Calls per extraction: the first, and one corrective retry.
    /// </summary>
    public const int MaxCalls = 2;

    public async Task<Result<TypedExtraction<T>>> ExtractAsync<T>(
        string task,
        string input,
        IReadOnlyDictionary<string, string>? promptValues,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(input);

        JsonElement schema = Schema<T>.Value;
        List<InferenceAttempt> attempts = [];
        InferenceUsage usage = InferenceUsage.None;
        string? problem = null;

        for (int call = 0; call < MaxCalls; call++)
        {
            string? correction = problem;
            string? instructions = null;
            Result<InferenceOutcome<Extraction>> outcome = await router.RunAsync<IStructuredExtractor, Extraction>(
                task,
                (extractor, model, prompt, token) =>
                {
                    instructions = Instructions(task, prompt, promptValues, correction);

                    return extractor.ExtractAsync(new ExtractionRequest(instructions, input, typeof(T).Name, schema), model, token);
                },
                cancellationToken).ConfigureAwait(false);

            if (outcome.IsFailure)
            {
                recorder.RecordExtraction(task, instructions, input, null, outcome.Error is InferenceUnavailableError unavailable ? unavailable.Attempts : []);

                return outcome.Error;
            }

            Extraction extraction = outcome.Value.Value;
            recorder.RecordExtraction(task, instructions, input, extraction, outcome.Value.Attempts);
            attempts.AddRange(outcome.Value.Attempts);
            usage = new InferenceUsage(usage.InputTokens + extraction.Usage.InputTokens, usage.OutputTokens + extraction.Usage.OutputTokens);

            try
            {
                T value = extraction.Output.Deserialize<T>(ContractJson.Strict) ?? throw new JsonException("The output is null.");

                return new TypedExtraction<T>(value, ValueList.From(attempts), usage);
            }
            catch (JsonException exception)
            {
                problem = exception.Message;
            }
        }

        return AiErrors.InvalidOutput(task, problem!);
    }

    private static string Instructions(string task, Prompt? prompt, IReadOnlyDictionary<string, string>? values, string? correction)
    {
        string instructions = (prompt ?? throw new InvalidOperationException($"Extraction task '{task}' has no prompt; declare it with AddExtraction."))
            .Render(values ?? new Dictionary<string, string>());

        return correction is null
            ? instructions
            : $"{instructions.TrimEnd()}\n\nYour previous output did not match the required schema: {correction}\nProduce output that matches the schema exactly.";
    }

    private static class Schema<T>
    {
        public static JsonElement Value { get; } = ContractSchemas.For(typeof(T));
    }
}
