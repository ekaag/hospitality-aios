namespace HospitalityOS.Integrations.Mock.Messaging;

/// <summary>
/// How the mock messaging provider behaves.
/// </summary>
public sealed class MockMessagingOptions
{
    /// <summary>
    /// Recipient addresses the mock refuses as unreachable, compared exactly, to exercise failure handling.
    /// </summary>
    public HashSet<string> UnreachableAddresses { get; } = new(StringComparer.Ordinal);
}
