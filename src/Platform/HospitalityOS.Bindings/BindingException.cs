namespace HospitalityOS.Bindings;

/// <summary>
/// Thrown when a port cannot be resolved because its binding configuration is missing or invalid.
/// Startup validation is designed to surface these before the first request.
/// </summary>
public sealed class BindingException : InvalidOperationException
{
    public BindingException()
    {
    }

    public BindingException(string message)
        : base(message)
    {
    }

    public BindingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
