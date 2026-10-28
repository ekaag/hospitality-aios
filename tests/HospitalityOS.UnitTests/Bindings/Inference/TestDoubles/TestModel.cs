using System.Collections.Concurrent;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Inference;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace HospitalityOS.UnitTests.Bindings.Inference.TestDoubles;

/// <summary>
/// What test models were created and asked, shared by every instance in a test host.
/// </summary>
internal sealed class ModelCalls
{
    public ConcurrentQueue<(string Label, string Model, InferenceParameters Parameters)> Calls { get; } = new();

    public ConcurrentQueue<string> Created { get; } = new();
}

/// <summary>
/// A chat model and classifier whose behaviour is set by its provider's settings, so a test configures it like any
/// provider: <c>Label</c> names it in answers; <c>Behavior</c> is <c>ok</c> (default), <c>unavailable</c> (throws
/// <see cref="InferenceUnavailableException"/>), or <c>broken</c> (throws another exception); <c>Takes</c> advances the
/// fake clock by that long per call; <c>Healthy</c> <c>false</c> makes its probe fail.
/// </summary>
internal sealed class TestModel(IConfiguration settings, ModelCalls calls, FakeTimeProvider clock) : IChatModel, IClassifier, IAdapterHealthProbe
{
    private string Label => settings["Label"] ?? "model";

    public async Task<ChatCompletion> CompleteAsync(ChatRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        await RespondAsync(model, cancellationToken).ConfigureAwait(false);

        return new ChatCompletion(ChatMessage.Assistant($"{Label}:{model.Model}"), ChatStopReason.Completed, InferenceUsage.None, model.Model);
    }

    public async Task<Classification> ClassifyAsync(ClassificationRequest request, ModelSelection model, CancellationToken cancellationToken)
    {
        await RespondAsync(model, cancellationToken).ConfigureAwait(false);

        return new Classification(request.Labels[0], 1, InferenceUsage.None, model.Model);
    }

    public Task<AdapterHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(settings["Healthy"] == "false" ? AdapterHealth.Unhealthy($"{Label} is down") : AdapterHealth.Healthy);

    private Task RespondAsync(ModelSelection model, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        calls.Calls.Enqueue((Label, model.Model, model.Parameters));

        if (TimeSpan.TryParse(settings["Takes"], System.Globalization.CultureInfo.InvariantCulture, out TimeSpan takes))
        {
            clock.Advance(takes);
        }

        return settings["Behavior"] switch
        {
            "unavailable" => throw new InferenceUnavailableException($"{Label} is overloaded"),
            "broken" => throw new InvalidOperationException($"{Label} rejected the request"),
            _ => Task.CompletedTask,
        };
    }
}

/// <summary>
/// Creates <see cref="TestModel"/>s for one port under an adapter name; <c>Behavior</c> <c>uncreatable</c> makes
/// creation fail, and an unknown behaviour is a settings error.
/// </summary>
internal sealed class TestModelFactory<TPort>(string adapterName) : IAdapterFactory<TPort>
    where TPort : class
{
    private static readonly string[] behaviors = ["ok", "unavailable", "broken", "uncreatable"];

    public string AdapterName => adapterName;

    public TPort Create(IServiceProvider services, IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        if (settings["Behavior"] == "uncreatable")
        {
            throw new InvalidOperationException("Cannot reach the provider.");
        }

        ModelCalls calls = (ModelCalls)services.GetService(typeof(ModelCalls))!;
        calls.Created.Enqueue($"{settings["Label"]}/{typeof(TPort).Name}");

        return (TPort)(object)new TestModel(settings, calls, (FakeTimeProvider)services.GetService(typeof(TimeProvider))!);
    }

    public IEnumerable<string> ValidateSettings(IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings["Behavior"] is { } behavior && !behaviors.Contains(behavior))
        {
            yield return $"Behavior '{behavior}' is not one of {string.Join(", ", behaviors)}.";
        }
    }
}
