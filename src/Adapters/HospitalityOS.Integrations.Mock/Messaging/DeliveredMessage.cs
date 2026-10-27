using HospitalityOS.IntegrationContracts.Messaging;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Integrations.Mock.Messaging;

/// <summary>
/// A message the mock delivered.
/// </summary>
internal sealed record DeliveredMessage(PropertyId PropertyId, OutboundMessage Message, MessageReceipt Receipt);
