using System.Security.Claims;

namespace HospitalityOS.Ports.Realtime;

/// <summary>
/// Which channels a connecting client may listen on, from its authenticated caller (P10: a client hears only its own
/// tenant). Implemented by the host, which knows its callers; used by every <see cref="IRealtimeNotifier"/> adapter when a
/// client connects.
/// </summary>
public interface IRealtimeAudience
{
    /// <summary>
    /// The channels <paramref name="caller"/> hears; none when it may hear nothing.
    /// </summary>
    IReadOnlyList<string> ChannelsFor(ClaimsPrincipal caller);
}
