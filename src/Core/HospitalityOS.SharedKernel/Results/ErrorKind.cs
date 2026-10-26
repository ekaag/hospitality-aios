namespace HospitalityOS.SharedKernel.Results;

/// <summary>
/// The category of an expected failure. APIs map each kind to one HTTP status.
/// </summary>
public enum ErrorKind
{
    /// <summary>
    /// Input is invalid (HTTP 400).
    /// </summary>
    Validation,

    /// <summary>
    /// The target does not exist, or is not visible to the caller's tenant (HTTP 404).
    /// </summary>
    NotFound,

    /// <summary>
    /// The change conflicts with current state, e.g. a duplicate or a concurrent update (HTTP 409).
    /// </summary>
    Conflict,

    /// <summary>
    /// The operation is not allowed in the target's current state, e.g. an illegal transition (HTTP 422).
    /// </summary>
    InvalidState,

    /// <summary>
    /// The caller is not allowed to do this (HTTP 403).
    /// </summary>
    Forbidden,
}
