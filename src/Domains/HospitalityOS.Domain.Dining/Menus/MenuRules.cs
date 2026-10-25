using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Pricing;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Validation;

namespace HospitalityOS.Domain.Dining.Menus;

/// <summary>
/// Validates and normalizes <see cref="MenuDetails"/>, reporting every problem at once.
/// </summary>
internal static class MenuRules
{
    public const int MaxItems = 300;

    public static Result<MenuDetails> Validate(MenuDetails details)
    {
        List<FieldError> errors = [];

        string name = TextRules.Required(details.Name, "name", "menu.name", errors);
        string? description = TextRules.Optional(details.Description, "description", "menu.description", errors, TextRules.MaxLongTextLength);
        ValueList<MenuItem> items = ValidateItems(details.Items ?? [], errors);

        return errors.Count > 0
            ? ValidationError.From(errors)
            : new MenuDetails(name, items, description);
    }

    private static ValueList<MenuItem> ValidateItems(ValueList<MenuItem> items, List<FieldError> errors)
    {
        if (items.Count > MaxItems)
        {
            errors.Add(new FieldError("items", "menu.too_many_items", $"At most {MaxItems} items are allowed."));
        }

        HashSet<MenuItemId> ids = [];
        HashSet<string> currencies = new(StringComparer.Ordinal);
        List<MenuItem> normalized = [];

        for (int i = 0; i < items.Count; i++)
        {
            string path = $"items[{i}]";
            MenuItem? item = items[i];

            if (item is null)
            {
                errors.Add(new FieldError(path, "menu.item_required", $"{path} is required."));
                continue;
            }

            if (item.Id == default)
            {
                errors.Add(new FieldError(path + ".id", "menu.item_id_required", $"{path}.id is required."));
            }
            else if (!ids.Add(item.Id))
            {
                errors.Add(new FieldError(path + ".id", "menu.item_id_duplicate", $"Item ID {item.Id} appears more than once."));
            }

            Money? price = item.Price is null ? null : MoneyRules.Validate(item.Price, path + ".price", "menu.item_price", errors);

            if (price is not null)
            {
                currencies.Add(price.CurrencyCode);
            }

            normalized.Add(item with
            {
                Name = TextRules.Required(item.Name, path + ".name", "menu.item_name", errors),
                Description = TextRules.Optional(item.Description, path + ".description", "menu.item_description", errors, TextRules.MaxLongTextLength),
                Category = TextRules.Optional(item.Category, path + ".category", "menu.item_category", errors),
                Price = price,
                Dietary = NormalizeDietary(item.Dietary ?? [], path + ".dietary", errors),
            });
        }

        if (currencies.Count > 1)
        {
            errors.Add(new FieldError("items", "menu.currency_mixed", $"All prices on a menu must use one currency; found {string.Join(", ", currencies.Order(StringComparer.Ordinal))}."));
        }

        return ValueList.From(normalized);
    }

    private static ValueList<DietaryTag> NormalizeDietary(ValueList<DietaryTag> dietary, string path, List<FieldError> errors)
    {
        SortedSet<DietaryTag> normalized = [];

        foreach (DietaryTag attribute in dietary)
        {
            if (!Enum.IsDefined(attribute))
            {
                errors.Add(new FieldError(path, "menu.item_dietary_invalid", $"'{attribute}' is not a known dietary tag."));
                continue;
            }

            normalized.Add(attribute);
        }

        // Vegan food is vegetarian by definition, so a "vegetarian" search must find it.
        if (normalized.Contains(DietaryTag.Vegan))
        {
            normalized.Add(DietaryTag.Vegetarian);
        }

        if (normalized.Contains(DietaryTag.NutFree) && normalized.Contains(DietaryTag.ContainsNuts))
        {
            errors.Add(new FieldError(path, "menu.item_dietary_contradictory", "An item cannot be both nut-free and contain nuts."));
        }

        return ValueList.From(normalized);
    }
}
