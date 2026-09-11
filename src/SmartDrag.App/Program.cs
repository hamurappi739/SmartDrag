using System.IO;
using SmartDrag.Presentation;

namespace SmartDrag.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var selfCheckArgument = args.FirstOrDefault(argument =>
            argument.StartsWith("--self-check", StringComparison.OrdinalIgnoreCase));
        if (selfCheckArgument is not null)
        {
            var separator = selfCheckArgument.IndexOf('=');
            var reportPath = separator >= 0 ? selfCheckArgument[(separator + 1)..] : null;
            return Preview.PreviewSelfCheck.RunAsync(reportPath).GetAwaiter().GetResult();
        }

        if (args.Any(argument => string.Equals(argument, "--preview", StringComparison.OrdinalIgnoreCase)))
        {
            var application = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose
            };
            LogPreviewEvent("preview-started");
            application.DispatcherUnhandledException += (_, eventArgs) =>
            {
                LogPreviewException(eventArgs.Exception);
                // Async UI handlers are guarded in the window, but the dispatcher remains a final containment
                // boundary so one unexpected callback cannot close the preview process after a delayed event.
                eventArgs.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                if (eventArgs.ExceptionObject is Exception exception)
                {
                    LogPreviewException(exception);
                }
            };
            TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
            {
                LogPreviewException(eventArgs.Exception);
                eventArgs.SetObserved();
            };
            try
            {
                application.Run(new Preview.PreviewWindow());
                return 0;
            }
            catch (Exception exception)
            {
                LogPreviewException(exception);
                return 1;
            }
            finally
            {
                LogPreviewEvent("preview-exited");
            }
        }

        // Production UI/runtime startup remains deliberately blocked until the Windows P0/G2 proof gates pass.
        // The safety bootstrap (single-instance -> journal recovery) is implemented in StartupSafetyBootstrap
        // so the eventual composition cannot accidentally register hooks before stale partial reconciliation.
        // Run tools/SmartDrag.Windows.Probe for current Windows validation.
        return 0;
    }

    private static void LogPreviewException(Exception exception)
    {
        PreviewProcessLog.TryAppend(PreviewProcessLog.DefaultPath, "unhandled-exception", exception.ToString());
    }

    private static void LogPreviewEvent(string eventName) =>
        PreviewProcessLog.TryAppend(PreviewProcessLog.DefaultPath, eventName);
}
