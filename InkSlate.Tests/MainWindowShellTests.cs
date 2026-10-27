using System.Windows;
using InkSlate.App.Views;
using Wpf.Ui.Controls;
using Xunit;

namespace InkSlate.Tests;

public sealed class MainWindowShellTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void MainWindowHasFluentMicaConfiguration()
    {
        RunInSta(() =>
        {
            var window = new MainWindow();

            Assert.Equal(WindowBackdropType.Mica, window.WindowBackdropType);
            Assert.True(window.ExtendsContentIntoTitleBar);
            Assert.Equal(WindowCornerPreference.Round, window.WindowCornerPreference);
            Assert.Equal(1280.0, window.Width);
            Assert.Equal(800.0, window.Height);
        });
    }

    [Fact]
    public void FloatingCommandBarHasAllSeventeenIconButtonsWithTooltips()
    {
        RunInSta(() =>
        {
            var window = new MainWindow();

            var bar = window.FindName("FloatingCommandBar") as System.Windows.Controls.Border;
            Assert.NotNull(bar);

            var buttonNamesAndTooltips = new (string Name, string Tooltip)[]
            {
                ("OpenButton", "Open (Ctrl+O)"),
                ("SaveButton", "Save (Ctrl+S)"),
                ("ExportPdfButton", "Export PDF (Ctrl+E)"),
                ("PointerToolButton", "Pointer / Select & Pan (V)"),
                ("PenToolButton", "Pen (P)"),
                ("HighlighterToolButton", "Highlighter (H)"),
                ("EraserToolButton", "Eraser (E)"),
                ("LassoToolButton", "Lasso (L)"),
                ("TextToolButton", "Text (T)"),
                ("ImageToolButton", "Image (I)"),
                ("UndoButton", "Undo (Ctrl+Z)"),
                ("RedoButton", "Redo (Ctrl+Y)"),
                ("AddPageButton", "Add Page (Ctrl+Enter)"),
                ("PageSetupButton", "Page Setup"),
                ("ToggleViewModeButton", "Toggle View: Consecutive / Separated (F5)"),
                ("CyclePlacementButton", "Cycle Toolbar Position: Fixed Top / Left Sidebar / Floating"),
                ("HelpButton", "Help & Shortcuts (F1)")
            };

            foreach (var (name, expectedTooltip) in buttonNamesAndTooltips)
            {
                var button = window.FindName(name) as Wpf.Ui.Controls.Button;
                Assert.NotNull(button);
                Assert.Equal(expectedTooltip, button.ToolTip?.ToString());
                Assert.IsType<SymbolIcon>(button.Content);
            }
        });
    }

    [Fact]
    public void CornerHudPillHasNavigationAndZoomReset()
    {
        RunInSta(() =>
        {
            var window = new MainWindow();

            var hud = window.FindName("CornerHudPill") as System.Windows.Controls.Border;
            Assert.NotNull(hud);
            Assert.Equal(new CornerRadius(12), hud.CornerRadius);

            var prev = window.FindName("PreviousPageButton") as Wpf.Ui.Controls.Button;
            Assert.NotNull(prev);
            Assert.Equal("Previous Page (PageUp)", prev.ToolTip?.ToString());
            Assert.IsType<SymbolIcon>(prev.Content);

            var text = window.FindName("PageIndicatorText") as System.Windows.Controls.TextBlock;
            Assert.NotNull(text);
            Assert.Equal("1 / 1", text.Text);

            var next = window.FindName("NextPageButton") as Wpf.Ui.Controls.Button;
            Assert.NotNull(next);
            Assert.Equal("Next Page (PageDown)", next.ToolTip?.ToString());
            Assert.IsType<SymbolIcon>(next.Content);

            var zoom = window.FindName("ResetZoomButton") as Wpf.Ui.Controls.Button;
            Assert.NotNull(zoom);
            Assert.Equal("100%", zoom.Content?.ToString());
            Assert.Equal("Reset Zoom (Ctrl+0)", zoom.ToolTip?.ToString());
        });
    }

    [Fact]
    public void ViewportRegionContainsZoomScrollViewer()
    {
        RunInSta(() =>
        {
            var window = new MainWindow();

            var region = window.FindName("ViewportRegion") as System.Windows.Controls.Border;
            Assert.NotNull(region);

            var scrollViewer = window.FindName("ViewportScrollViewer") as InkSlate.App.Controls.ZoomScrollViewer;
            Assert.NotNull(scrollViewer);
            Assert.Equal(1.0, scrollViewer.ZoomLevel);
            Assert.True(scrollViewer.IsManipulationEnabled);
        });
    }
}
