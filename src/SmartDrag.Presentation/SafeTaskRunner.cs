namespace SmartDrag.Presentation;

/// <summary>
/// Explicit boundary for fire-and-forget work started by the Preview host. Exceptions are observed and logged so
/// a delayed background callback cannot tear down the process without evidence.
/// </summary>
public static class SafeTaskRunner
{
    public static void FireAndForget(
        Func<Task> operation,
        string operationName,
        Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("An operation name is required.", nameof(operationName));
        }

        _ = ObserveAsync(operation, operationName, onError);
    }

    private static async Task ObserveAsync(Func<Task> operation, string operationName, Action<Exception>? onError)
    {
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            PreviewProcessLog.TryAppend(
                PreviewProcessLog.DefaultPath,
                "background-operation-failed",
                $"{operationName}: {exception}");
            try
            {
                onError?.Invoke(exception);
            }
            catch (Exception callbackException)
            {
                PreviewProcessLog.TryAppend(
                    PreviewProcessLog.DefaultPath,
                    "background-operation-error-callback-failed",
                    $"{operationName}: {callbackException}");
            }
        }
    }
}
