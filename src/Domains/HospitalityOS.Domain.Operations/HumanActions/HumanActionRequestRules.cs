using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Validation;

namespace HospitalityOS.Domain.Operations.HumanActions;

/// <summary>
/// Validates and normalizes <see cref="HumanActionRequest"/>s and completion data, reporting every problem at once.
/// </summary>
internal static class HumanActionRequestRules
{
    public const int MaxItems = 20;
    public const int MaxItemQuantity = 100;
    public const int MaxSkills = 10;
    public const int MaxNoteLength = 1000;
    public const int MaxEvidence = 10;
    public const int MaxReasonLength = 500;

    public static TimeSpan MaxSla { get; } = TimeSpan.FromDays(7);

    public static Result<HumanActionRequest> Validate(HumanActionRequest request, DateTimeOffset now)
    {
        List<FieldError> errors = [];

        string instructions = TextRules.Required(request.Instructions, "instructions", "human_action.instructions", errors, TextRules.MaxLongTextLength);
        ActionLocation? location = LocationRules.Validate(request.Location, "location", "human_action.location", errors);
        string reason = TextRules.Required(request.Reason, "reason", "human_action.reason", errors, MaxReasonLength);
        string agent = TextRules.Required(request.RequestedByAgent, "requestedByAgent", "human_action.requested_by_agent", errors, 100);
        ValueList<ActionItem> items = ValidateItems(request.Items ?? [], errors);
        ValueList<string> skills = ValidateSkills(request.SkillsRequired ?? [], errors);

        if (!Enum.IsDefined(request.ActionType))
        {
            errors.Add(new FieldError("actionType", "human_action.action_type_invalid", $"'{request.ActionType}' is not a known action type."));
        }
        else if (request.ActionType == HumanActionType.DeliverItems && items.Count == 0)
        {
            errors.Add(new FieldError("items", "human_action.items_required", "A delivery needs at least one item."));
        }

        if (!Enum.IsDefined(request.Priority))
        {
            errors.Add(new FieldError("priority", "human_action.priority_invalid", $"'{request.Priority}' is not a known priority."));
        }

        if (request.Sla is { } sla && (sla <= TimeSpan.Zero || sla > MaxSla))
        {
            errors.Add(new FieldError("sla", "human_action.sla_out_of_range", $"The SLA must be positive and at most {MaxSla.TotalDays} days."));
        }

        if (request.Deadline is { } deadline && deadline <= now)
        {
            errors.Add(new FieldError("deadline", "human_action.deadline_in_past", "The deadline must be in the future."));
        }

        return errors.Count > 0
            ? ValidationError.From(errors)
            : request with
            {
                Instructions = instructions,
                Location = location!,
                Reason = reason,
                RequestedByAgent = agent,
                Items = items,
                SkillsRequired = skills,
            };
    }

    public static Result<HumanActionCompletion> ValidateCompletion(StaffMemberId staffMemberId, string? note, IEnumerable<string>? evidence, DateTimeOffset now)
    {
        List<FieldError> errors = [];

        string? trimmedNote = TextRules.Optional(note, "note", "human_action.note", errors, MaxNoteLength);
        List<string> keys = [];

        foreach (string? key in evidence ?? [])
        {
            keys.Add(TextRules.Required(key, $"evidence[{keys.Count}]", "human_action.evidence", errors, 500));
        }

        if (keys.Count > MaxEvidence)
        {
            errors.Add(new FieldError("evidence", "human_action.too_much_evidence", $"At most {MaxEvidence} evidence files are allowed."));
        }

        return errors.Count > 0
            ? ValidationError.From(errors)
            : new HumanActionCompletion(staffMemberId, now, trimmedNote, ValueList.From(keys));
    }

    public static Result<string> ValidateReason(string? reason)
    {
        List<FieldError> errors = [];
        string trimmed = TextRules.Required(reason, "reason", "human_action.reason", errors, MaxReasonLength);

        return errors.Count > 0 ? ValidationError.From(errors) : trimmed;
    }

    private static ValueList<ActionItem> ValidateItems(ValueList<ActionItem> items, List<FieldError> errors)
    {
        if (items.Count > MaxItems)
        {
            errors.Add(new FieldError("items", "human_action.too_many_items", $"At most {MaxItems} items are allowed."));
        }

        List<ActionItem> normalized = [];

        for (int i = 0; i < items.Count; i++)
        {
            string path = $"items[{i}]";
            ActionItem? item = items[i];

            if (item is null)
            {
                errors.Add(new FieldError(path, "human_action.item_required", $"{path} is required."));
                continue;
            }

            string name = TextRules.Required(item.Name, path + ".name", "human_action.item_name", errors);
            string? code = item.Code is null ? null : TextRules.Code(item.Code, path + ".code", "human_action.item_code", errors);

            if (item.Quantity is < 1 or > MaxItemQuantity)
            {
                errors.Add(new FieldError(path + ".quantity", "human_action.item_quantity_out_of_range", $"Quantity must be between 1 and {MaxItemQuantity}."));
            }

            normalized.Add(new ActionItem(name, item.Quantity, code));
        }

        return ValueList.From(normalized);
    }

    private static ValueList<string> ValidateSkills(ValueList<string> skills, List<FieldError> errors)
    {
        SortedSet<string> normalized = new(StringComparer.Ordinal);

        for (int i = 0; i < skills.Count; i++)
        {
            string skill = TextRules.Code(skills[i], $"skillsRequired[{i}]", "human_action.skill", errors);

            if (skill.Length > 0)
            {
                normalized.Add(skill);
            }
        }

        if (normalized.Count > MaxSkills)
        {
            errors.Add(new FieldError("skillsRequired", "human_action.too_many_skills", $"At most {MaxSkills} skills are allowed."));
        }

        return ValueList.From(normalized);
    }
}
