using HospitalityOS.IntegrationContracts.Messaging;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Integrations.Mock.Messaging;

/// <summary>
/// One property's view of the mock messaging gateway.
/// </summary>
internal sealed class MockMessagingConnector(MockMessagingGateway gateway, PropertyId propertyId) : IMessagingConnector
{
    public Task<Result<MessageReceipt>> SendAsync(OutboundMessage message, CancellationToken cancellationToken) =>
        Task.FromResult(gateway.Send(propertyId, message));
}
