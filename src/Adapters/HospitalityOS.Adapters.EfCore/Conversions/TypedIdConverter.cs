using HospitalityOS.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HospitalityOS.Adapters.EfCore.Conversions;

/// <summary>
/// Stores a strongly typed ID as its <see cref="Guid"/>. Applied to every ID type by convention.
/// </summary>
internal sealed class TypedIdConverter<TId>() : ValueConverter<TId, Guid>(id => id.Value, value => TypedIds<TId>.Create(value))
    where TId : struct, ITypedId;
