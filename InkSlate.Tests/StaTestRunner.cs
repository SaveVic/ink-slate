using System.Collections.Concurrent;
using System.Windows;

namespace InkSlate.Tests;

internal static class StaTestRunner
{
    private static readonly BlockingCollection<Action> WorkQueue = [];
    private static readonly Thread StaThread;

    static StaTestRunner()
    {
        StaThread = new Thread(() =>
        {
            if (Application.Current is null)
            {
                var app = new Application();
                app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark });
                app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
            }

            foreach (var action in WorkQueue.GetConsumingEnumerable())
            {
                action();
            }
        });
        StaThread.SetApartmentState(ApartmentState.STA);
        StaThread.IsBackground = true;
        StaThread.Start();
    }

    public static void Run(Action action)
    {
        var tcs = new TaskCompletionSource();
        WorkQueue.Add(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        tcs.Task.GetAwaiter().GetResult();
    }
}
