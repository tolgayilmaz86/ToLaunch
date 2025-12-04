namespace ToLaunch.ViewModels;

/// <summary>
/// Marker class used as a placeholder in the cards collection to represent the "Add Program" card.
/// This allows the add button to flow naturally within the WrapPanel alongside other cards.
/// </summary>
public class AddCardPlaceholder
{
    public static AddCardPlaceholder Instance { get; } = new();

    private AddCardPlaceholder() { }
}
