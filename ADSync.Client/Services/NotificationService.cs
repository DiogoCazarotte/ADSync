using Microsoft.AspNetCore.Components.Forms;

namespace ADSync.Client.Services;

public enum NotificationKind { Information, Success, Warning, Error }
public sealed record Notification(Guid Id, string Message, NotificationKind Kind);

public sealed class NotificationService : IDisposable
{
    readonly List<Notification> notifications = [];
    readonly CancellationTokenSource lifetime = new();
    public IReadOnlyList<Notification> Items => notifications.ToArray();
    public event Action? Changed;

    public void Show(string? message, NotificationKind kind = NotificationKind.Information)
    {
        if (string.IsNullOrWhiteSpace(message) || lifetime.IsCancellationRequested) return;
        notifications.RemoveAll(item => item.Message == message && item.Kind == kind);
        var notification = new Notification(Guid.NewGuid(), message.Trim(), kind);
        notifications.Add(notification);
        if (notifications.Count > 3) notifications.RemoveAt(0);
        Changed?.Invoke();
        _ = Expire(notification.Id);
    }

    public void Error(string? message) => Show(message, NotificationKind.Error);
    public void Success(string? message) => Show(message, NotificationKind.Success);
    public void Warning(string? message) => Show(message, NotificationKind.Warning);
    public void Report(string? error, string? message)
    {
        if (!string.IsNullOrWhiteSpace(error)) Error(error);
        else Success(message);
    }
    public void Validation(EditContext context) => Error(string.Join(" ", context.GetValidationMessages().Distinct()));
    public void Dismiss(Guid id)
    {
        if (notifications.RemoveAll(item => item.Id == id) > 0) Changed?.Invoke();
    }
    async Task Expire(Guid id)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(5), lifetime.Token); Dismiss(id); }
        catch (OperationCanceledException) { }
    }
    public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); }
}
