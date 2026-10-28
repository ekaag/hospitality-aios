using System.Text.Json;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Api.Ai;

/// <summary>
/// A message sent to or received from a model.
/// </summary>
/// <param name="Role">Who it is from.</param>
/// <param name="Parts">Its parts, in order.</param>
public sealed record TraceMessage(TraceMessageRole Role, ValueList<TracePart> Parts);

/// <summary>
/// One part of a <see cref="TraceMessage"/>; which fields are set depends on <paramref name="Type"/>.
/// </summary>
/// <param name="Type">What the part is.</param>
/// <param name="Text">The text, or a tool result's content.</param>
/// <param name="CallId">The tool call it is or answers.</param>
/// <param name="Name">The tool called.</param>
/// <param name="Arguments">The tool call's arguments.</param>
/// <param name="IsError">Whether a tool result reports an error.</param>
/// <param name="Provider">The provider whose reasoning it is.</param>
public sealed record TracePart(
    TracePartType Type,
    string? Text = null,
    string? CallId = null,
    string? Name = null,
    JsonElement? Arguments = null,
    bool? IsError = null,
    string? Provider = null);
