using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using InkSlate.App.ViewModels;
using Wpf.Ui.Controls;

namespace InkSlate.App.Views;

public partial class MainWindow : FluentWindow
{
    private readonly List<FrameworkElement> _childComponents = new();

    public MainWindow()
        : this(new MainViewModel())
    {
    }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        ViewModel.CursorPositionProvider = GetCurrentCanvasCursorPosition;

        RegisterChildComponents();

        ViewportScrollViewer.ScrollChanged += (_, _) =>
            ViewModel.UpdateVisiblePagesHydration(ViewportScrollViewer.VerticalOffset, ViewportScrollViewer.ViewportHeight);

        ViewportScrollViewer.SizeChanged += (_, _) =>
            ViewModel.UpdateVisiblePagesHydration(ViewportScrollViewer.VerticalOffset, ViewportScrollViewer.ViewportHeight);

        ViewportScrollViewer.RequestBringIntoView += (_, e) => e.Handled = true;

        ViewModel.PageNavigationScrollRequested += OnPageNavigationScrollRequested;
    }

    public MainViewModel ViewModel { get; }

    private void RegisterChildComponents()
    {
        if (PenOptionsComponent is not null) _childComponents.Add(PenOptionsComponent);
        if (EraserOptionsComponent is not null) _childComponents.Add(EraserOptionsComponent);
        if (LassoFilterComponent is not null) _childComponents.Add(LassoFilterComponent);
        if (TextOptionsComponent is not null) _childComponents.Add(TextOptionsComponent);
        if (PageSetupComponent is not null) _childComponents.Add(PageSetupComponent);
        if (PdfExportComponent is not null) _childComponents.Add(PdfExportComponent);
        if (CustomPenComponent is not null) _childComponents.Add(CustomPenComponent);
        if (StatusHudComponent is not null) _childComponents.Add(StatusHudComponent);
        if (HelpShortcutsComponent is not null) _childComponents.Add(HelpShortcutsComponent);

        var scope = NameScope.GetNameScope(this);
        if (scope is not null)
        {
            foreach (var comp in _childComponents)
            {
                RegisterNamesRecursively(comp, scope);
            }

            if (PenOptionsComponent?.FindName("PenOptionsBorder") is { } penBorder && scope.FindName("PenOptionsPopover") is null)
            {
                scope.RegisterName("PenOptionsPopover", penBorder);
            }
        }
    }

    private static void RegisterNamesRecursively(DependencyObject parent, INameScope scope)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is FrameworkElement fe)
            {
                if (!string.IsNullOrEmpty(fe.Name) && scope.FindName(fe.Name) is null)
                {
                    scope.RegisterName(fe.Name, fe);
                }
                RegisterNamesRecursively(fe, scope);
            }
        }
    }

    public new object? FindName(string name)
    {
        var found = base.FindName(name);
        if (found is not null)
        {
            return found;
        }

        if (name == "PenOptionsPopover")
        {
            var border = PenOptionsComponent?.FindName("PenOptionsBorder");
            if (border is not null)
            {
                return border;
            }
        }

        foreach (var comp in _childComponents)
        {
            var compFound = comp.FindName(name);
            if (compFound is not null)
            {
                return compFound;
            }
        }

        foreach (var fe in FindVisualChildren<FrameworkElement>(this))
        {
            if (fe.Name == name)
            {
                return fe;
            }
        }

        return null;
    }

    private void OnPageNavigationScrollRequested(PageViewModel targetPage)
    {
        if (ViewModel.IsConsecutiveView)
        {
            var targetOffset = ViewModel.GetPageVerticalOffset(targetPage) * ViewportScrollViewer.ZoomLevel;
            ViewportScrollViewer.ScrollToVerticalOffset(targetOffset);
        }
        else
        {
            ViewportScrollViewer.ScrollToVerticalOffset(0);
            ViewportScrollViewer.ScrollToHorizontalOffset(0);
        }
    }

    public void HandleTitlePillClick(int clickCount)
    {
        if (clickCount >= 2 && !ViewModel.IsEditingTitle)
        {
            ViewModel.BeginEditTitleCommand.Execute(null);
        }
    }

    private void TitleDisplayPill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            HandleTitlePillClick(e.ClickCount);
            e.Handled = true;
        }
    }

    private void TitleEditTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox && textBox.IsVisible)
        {
            Dispatcher.BeginInvoke(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void TitleEditTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.CommitTitleEditCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel.CancelTitleEditCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void TitleEditTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TitleContainer.IsKeyboardFocusWithin)
        {
            return;
        }

        if (ViewModel.IsEditingTitle)
        {
            ViewModel.CommitTitleEditCommand.Execute(null);
        }
    }

    public Point? GetCurrentCanvasCursorPosition()
    {
        var allPageCanvases = FindVisualChildren<Controls.PageCanvasControl>(this).ToList();
        Controls.PageCanvasControl? targetCanvas = null;

        foreach (var pcc in allPageCanvases)
        {
            if (pcc.IsMouseOver)
            {
                targetCanvas = pcc;
                break;
            }
        }

        if (targetCanvas is null && ViewModel.ActivePage is not null)
        {
            targetCanvas = allPageCanvases.FirstOrDefault(p => p.PageId == ViewModel.ActivePage.Id);
        }

        if (targetCanvas is not null)
        {
            var pos = Mouse.GetPosition(targetCanvas.InternalInkCanvas);
            var w = targetCanvas.ActualWidth > 0 ? targetCanvas.ActualWidth : (targetCanvas.Width > 0 ? targetCanvas.Width : double.PositiveInfinity);
            var h = targetCanvas.ActualHeight > 0 ? targetCanvas.ActualHeight : (targetCanvas.Height > 0 ? targetCanvas.Height : double.PositiveInfinity);

            if (pos.X >= 0 && pos.Y >= 0 && pos.X <= w && pos.Y <= h)
            {
                if (ViewModel.ActivePage is null || targetCanvas.PageId != ViewModel.ActivePage.Id)
                {
                    var pageVm = ViewModel.Pages.FirstOrDefault(p => p.Id == targetCanvas.PageId);
                    if (pageVm is not null)
                    {
                        ViewModel.ActivePage = pageVm;
                    }
                }
                return pos;
            }

            if (targetCanvas.LastCanvasPointerPosition is { } lastPos &&
                lastPos.X >= 0 && lastPos.Y >= 0 && lastPos.X <= w && lastPos.Y <= h)
            {
                return lastPos;
            }
        }

        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject
    {
        if (depObj is null)
        {
            yield break;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t)
            {
                yield return t;
            }

            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }
}
