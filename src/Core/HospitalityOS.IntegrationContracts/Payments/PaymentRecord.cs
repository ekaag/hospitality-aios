using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.IntegrationContracts.Payments;

/// <summary>
/// A payment as the provider reports it. Card details never cross this boundary (P10).
/// </summary>
/// <param name="Id">The provider's ID for the payment.</param>
/// <param name="Amount">The amount.</param>
/// <param name="Status">Where the payment is in its lifecycle.</param>
/// <param name="UpdatedAt">When the provider last changed it.</param>
public sealed record PaymentRecord(ExternalId Id, Money Amount, PaymentStatus Status, DateTimeOffset UpdatedAt);
