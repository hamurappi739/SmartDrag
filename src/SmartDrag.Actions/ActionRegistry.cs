using SmartDrag.Core.Actions;
using SmartDrag.Core.Payload;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Actions;

public sealed class ActionRegistry : IActionRegistry
{
    private readonly IReadOnlyDictionary<ActionId, ActionDefinition> _definitions;

    public ActionRegistry(IEnumerable<ActionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var dictionary = new Dictionary<ActionId, ActionDefinition>();
        foreach (var definition in definitions)
        {
            ValidateDefinition(definition);
            if (!dictionary.TryAdd(definition.Id, definition))
            {
                throw new ArgumentException($"Duplicate ActionId '{definition.Id}'.", nameof(definitions));
            }
        }

        _definitions = dictionary;
    }

    public IReadOnlyList<ActionDefinition> GetAvailable(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Payload.IsSupported || context.Payload.Files.Count == 0)
        {
            return Array.Empty<ActionDefinition>();
        }

        if (context.Payload.Files.Any(file => file.Category == FileCategory.Image) && !context.Capabilities.ImagingAvailable)
        {
            return Array.Empty<ActionDefinition>();
        }

        var categories = context.Payload.Files.Select(file => file.Category).Distinct().ToArray();

        return _definitions.Values
            .Where(definition => definition.ExecutesLocally)
            .Where(definition => definition.Id != BuiltInActionIds.ConvertImageToWebP || context.Capabilities.WebpEncodingAvailable)
            .Where(definition => context.Payload.Files.Count == 1 || definition.CanBatch)
            .Where(definition => categories.All(definition.SupportedInputTypes.Contains))
            .OrderBy(definition => definition.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public bool TryGet(ActionId id, out ActionDefinition definition) =>
        _definitions.TryGetValue(id, out definition!);

    private static void ValidateDefinition(ActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id.Value))
        {
            throw new ArgumentException("Action definitions require a non-empty id.", nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            throw new ArgumentException($"Action '{definition.Id}' requires a display name.", nameof(definition));
        }

        if (definition.SupportedInputTypes is null || definition.SupportedInputTypes.Count == 0)
        {
            throw new ArgumentException($"Action '{definition.Id}' must declare at least one supported input type.", nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.IconId))
        {
            throw new ArgumentException($"Action '{definition.Id}' requires an icon id.", nameof(definition));
        }
    }
}
