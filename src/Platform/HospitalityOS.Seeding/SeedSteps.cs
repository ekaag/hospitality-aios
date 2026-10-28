using HospitalityOS.Application.Administration;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Seeding;

/// <summary>
/// Creates each seed record once, under its fixed ID, through the same administration service an administrator would
/// use, and counts what was created and what already existed. Seed data is the platform's own, so a refusal is a defect
/// in it and throws. Scoped.
/// </summary>
internal sealed class SeedSteps(RecordIds ids)
{
    public int Created { get; private set; }

    public int Existing { get; private set; }

    public Task<T> EnsureAsync<T>(string record, Guid id, Func<Task<Result<T>>> get, Func<Task<Result<T>>> create) =>
        EnsureAsync(record, [id], get, create);

    /// <summary>
    /// Returns the record <paramref name="get"/> finds, or creates it with <paramref name="create"/>, which must take
    /// exactly the <paramref name="reserved"/> IDs: the record's own first, then those of what it contains.
    /// </summary>
    public async Task<T> EnsureAsync<T>(string record, IReadOnlyList<Guid> reserved, Func<Task<Result<T>>> get, Func<Task<Result<T>>> create)
    {
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(create);

        Result<T> existing = await get().ConfigureAwait(false);

        if (existing.IsSuccess)
        {
            Existing++;

            return existing.Value;
        }

        if (existing.Error.Kind != ErrorKind.NotFound)
        {
            throw Failed(record, existing.Error);
        }

        ids.Reserve(reserved);
        Result<T> created;
        int unused;

        try
        {
            created = await create().ConfigureAwait(false);
        }
        finally
        {
            unused = ids.Unused;
            ids.Clear();
        }

        if (created.IsFailure)
        {
            throw Failed(record, created.Error);
        }

        // A record that took fewer IDs than planned was created under an ID other than its fixed one.
        if (unused > 0 || (await get().ConfigureAwait(false)).IsFailure)
        {
            throw new InvalidOperationException($"Seeding {record} did not use its fixed IDs; the order in which the service takes IDs has changed.");
        }

        Created++;

        return created.Value;
    }

    /// <summary>
    /// Requires a follow-up command (activating a policy, publishing content) to succeed.
    /// </summary>
    public static async Task RequireAsync<T>(string record, Task<Result<T>> command)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<T> result = await command.ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw Failed(record, result.Error);
        }
    }

    public void CountExisting() => Existing++;

    public void CountCreated() => Created++;

    public static InvalidOperationException Failed(string record, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        string fields = error is ValidationError validation
            ? " (" + string.Join("; ", validation.Fields.Select(field => $"{field.Field}: {field.Code}")) + ")"
            : "";

        return new InvalidOperationException($"Seeding {record} failed with {error.Code}: {error.Message}{fields}");
    }
}
