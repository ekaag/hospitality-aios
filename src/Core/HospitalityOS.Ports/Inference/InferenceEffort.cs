namespace HospitalityOS.Ports.Inference;

/// <summary>
/// How much reasoning a model spends on a call: more is slower and costs more, and pays off only on harder work.
/// </summary>
public enum InferenceEffort
{
    /// <summary>
    /// Least: routing, classification, simple extraction.
    /// </summary>
    Low,

    /// <summary>
    /// Everyday answers.
    /// </summary>
    Medium,

    /// <summary>
    /// Answers that need care, e.g. judging.
    /// </summary>
    High,

    /// <summary>
    /// The most the model can spend.
    /// </summary>
    Max,
}
