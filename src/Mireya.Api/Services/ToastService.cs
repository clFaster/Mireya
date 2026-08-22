namespace Mireya.Api.Services;

public enum ToastLevel
{
    Success,
    Error,
    Info,
}

public sealed class ToastMessage
{
    public required Guid Id { get; init; }
    public required ToastLevel Level { get; init; }
    public required string Text { get; init; }
}

/// <summary>
///     Scoped, in-memory toast notification queue for the admin UI. Pages call
///     <see cref="ShowSuccess" /> / <see cref="ShowError" /> and the <c>ToastHost</c>
///     component renders and auto-dismisses the messages.
/// </summary>
public sealed class ToastService
{
    private readonly List<ToastMessage> _messages = [];

    public IReadOnlyList<ToastMessage> Messages => _messages;

    public event Action? OnChange;

    public void ShowSuccess(string text) => Show(ToastLevel.Success, text);

    public void ShowError(string text) => Show(ToastLevel.Error, text);

    public void ShowInfo(string text) => Show(ToastLevel.Info, text);

    public void Show(ToastLevel level, string text)
    {
        _messages.Add(
            new ToastMessage
            {
                Id = Guid.NewGuid(),
                Level = level,
                Text = text,
            }
        );
        OnChange?.Invoke();
    }

    public void Remove(Guid id)
    {
        var removed = _messages.RemoveAll(m => m.Id == id);
        if (removed > 0)
            OnChange?.Invoke();
    }
}
