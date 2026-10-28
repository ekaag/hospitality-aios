using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ai.StructuredOutput;

/// <summary>
/// Extracts a typed value from text through an extraction task (declared with <c>AddExtraction</c>), validating the
/// model's output (spec §35): the schema is generated from the type, and the output is read strictly, so a missing
/// required member, a wrong type, an unknown enum name, or an invented member is refused. Invalid output is sent back
/// once with what was wrong; if it is still invalid, the result is <see cref="AiErrors.InvalidOutputCode"/>. Scoped.
/// </summary>
public interface ITypedExtractor
{
    /// <summary>
    /// Extracts a <typeparamref name="T"/> from <paramref name="input"/>.
    /// </summary>
    /// <typeparam name="T">A record or class written as a JSON object in the wire format.</typeparam>
    /// <param name="task">The extraction task, e.g. <c>guestservice.extract</c>.</param>
    /// <param name="input">The text to read, e.g. the guest's message.</param>
    /// <param name="promptValues">Values for the task prompt's variables, if it has any.</param>
    /// <param name="cancellationToken">Cancels the extraction.</param>
    /// <returns>The value, or <c>inference.unavailable</c> or <see cref="AiErrors.InvalidOutputCode"/>.</returns>
    Task<Result<TypedExtraction<T>>> ExtractAsync<T>(
        string task,
        string input,
        IReadOnlyDictionary<string, string>? promptValues,
        CancellationToken cancellationToken);
}
