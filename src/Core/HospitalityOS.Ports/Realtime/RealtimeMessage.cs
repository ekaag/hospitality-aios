using System.Text.Json;

namespace HospitalityOS.Ports.Realtime;

/// <summary>
/// A message for the clients on one channel.
/// </summary>
/// <param name="Channel">Who hears it, e.g. a tenant's staff; <see cref="IRealtimeAudience"/> decides who may listen.</param>
/// <param name="Type">What happened, e.g. <c>WorkChanged</c>; clients subscribe by it.</param>
/// <param name="Payload">The details, as JSON.</param>
public sealed record RealtimeMessage(string Channel, string Type, JsonElement Payload);
