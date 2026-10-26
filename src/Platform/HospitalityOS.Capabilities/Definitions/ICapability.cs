namespace HospitalityOS.Capabilities.Definitions;

/// <summary>
/// A capability: a named, versioned operation and the only way AI agents (and the workflows and APIs that share it)
/// read platform data or change state (P7). Implement <see cref="ICapability{TInput, TOutput}"/> and register the class
/// with <c>AddCapability</c>; its <see cref="Descriptor"/> and input/output types make its
/// <see cref="CapabilityDefinition"/>.
/// </summary>
public interface ICapability
{
    /// <summary>
    /// The capability's name, version, description, and policies (spec §11).
    /// </summary>
    static abstract CapabilityDescriptor Descriptor { get; }
}
