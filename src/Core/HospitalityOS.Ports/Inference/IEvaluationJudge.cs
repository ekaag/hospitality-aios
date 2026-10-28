namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Grades an AI response against criteria (LLM-as-judge in AI evaluations).
/// </summary>
public interface IEvaluationJudge
{
    /// <summary>
    /// Judges <see cref="JudgeRequest.Output"/>.
    /// </summary>
    /// <exception cref="InferenceUnavailableException">The provider cannot serve the call now.</exception>
    Task<Judgement> JudgeAsync(JudgeRequest request, ModelSelection model, CancellationToken cancellationToken);
}
