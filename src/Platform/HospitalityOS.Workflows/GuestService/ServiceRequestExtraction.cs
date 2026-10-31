using System.ComponentModel;

namespace HospitalityOS.Workflows.GuestService;

/// <summary>
/// The service a guest's message asks for, as the model reads it against the property's services.
/// </summary>
[Description("The service a guest's message asks for.")]
internal sealed record ServiceRequestExtraction(
    [property: Description("Whether the message clearly asks for one of the listed services.")]
    bool Understood,
    [property: Description("The service's code, exactly as listed; empty when not understood.")]
    string? ServiceCode,
    [property: Description("The item's code, exactly as listed, for a service with items; otherwise empty.")]
    string? ItemCode,
    [property: Description("How many items; 1 unless the guest said otherwise.")]
    int? Quantity,
    [property: Description("For a service without items, the problem in the guest's words, e.g. \"The air conditioning is not working\"; otherwise empty.")]
    string? Description);
