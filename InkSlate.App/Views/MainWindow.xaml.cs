using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;
using Wpf.Ui.Controls;

namespace InkSlate.App.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
        : this(new MainViewModel())
    {
    }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        ViewportScrollViewer.ScrollChanged += (_, _) =>
            ViewModel.UpdateVisiblePagesHydration(ViewportScrollViewer.VerticalOffset, ViewportScrollViewer.ViewportHeight);

        ViewportScrollViewer.SizeChanged += (_, _) =>
            ViewModel.UpdateVisiblePagesHydration(ViewportScrollViewer.VerticalOffset, ViewportScrollViewer.ViewportHeight);

        ViewportScrollViewer.RequestBringIntoView += (_, e) => e.Handled = true;

        ViewModel.PageNavigationScrollRequested += OnPageNavigationScrollRequested;
    }

    public MainViewModel ViewModel { get; }

    public bool HandleNavigationKey(Key key, ModifierKeys modifiers)
    {
        if (modifiers == ModifierKeys.None)
        {
            if (key is Key.PageUp or Key.Left)
            {
                if (ViewModel.PreviousPageCommand.CanExecute(null))
                {
                    ViewModel.PreviousPageCommand.Execute(null);
                    return true;
                }
            }
            else if (key is Key.PageDown or Key.Right)
            {
                if (ViewModel.NextPageCommand.CanExecute(null))
                {
                    ViewModel.NextPageCommand.Execute(null);
                    return true;
                }
            }
            else if (key == Key.F1)
            {
                ViewModel.ToggleHelpCommand.Execute(null);
                return true;
            }
            else if (key == Key.Escape && ViewModel.IsHelpOpen)
            {
                ViewModel.CloseHelpCommand.Execute(null);
                return true;
            }
            else if (key == Key.F2)
            {
                if (!ViewModel.IsEditingTitle)
                {
                    ViewModel.BeginEditTitleCommand.Execute(null);
                    return true;
                }
            }
            else if (key == Key.F5)
            {
                ViewModel.ToggleViewModeCommand.Execute(null);
                return true;
            }
            else if (key == Key.F11)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return true;
            }
            else if (key == Key.V)
            {
                ViewModel.Toolbar.SelectToolCommand.Execute(ToolMode.Pointer);
                return true;
            }
            else if (key == Key.P)
            {
                ViewModel.Toolbar.SelectToolCommand.Execute(ToolMode.Pen);
                return true;
            }
            else if (key == Key.H)
            {
                ViewModel.Toolbar.SelectToolCommand.Execute(ToolMode.Highlighter);
                return true;
            }
            else if (key == Key.E)
            {
                ViewModel.Toolbar.ActivateOrCycleEraserCommand.Execute(null);
                return true;
            }
            else if (key == Key.L)
            {
                ViewModel.Toolbar.ActivateOrCycleLassoCommand.Execute(null);
                return true;
            }
            else if (key == Key.T)
            {
                ViewModel.Toolbar.SelectToolCommand.Execute(ToolMode.Text);
                return true;
            }
            else if (key == Key.I)
            {
                ViewModel.Toolbar.SelectToolCommand.Execute(ToolMode.Image);
                return true;
            }
            else if (key is Key.Delete or Key.Back)
            {
                var hasSelectedElements = ViewModel.Pages.Any(p => p.Elements.Any(e => e.IsSelected && !e.IsEditing));
                if (hasSelectedElements)
                {
                    ViewModel.DeleteSelectedCommand.Execute(null);
                    return true;
                }

                var deletedStrokes = false;
                foreach (var pcc in FindVisualChildren<Controls.PageCanvasControl>(this))
                {
                    if (pcc.DeleteSelectedStrokes())
                    {
                        deletedStrokes = true;
                    }
                }

                if (deletedStrokes)
                {
                    return true;
                }
            }
        }
        else if (modifiers == ModifierKeys.Control)
        {
            if (key == Key.N)
            {
                if (ViewModel.NewDocumentCommand.CanExecute(null))
                {
                    ViewModel.NewDocumentCommand.Execute(null);
                    return true;
                }
            }

            if (key == Key.S)
            {
                if (ViewModel.SaveCommand.CanExecute(null))
                {
                    ViewModel.SaveCommand.Execute(null);
                    return true;
                }
            }

            if (key == Key.O)
            {
                if (ViewModel.OpenCommand.CanExecute(null))
                {
                    ViewModel.OpenCommand.Execute(null);
                    return true;
                }
            }

            if (key == Key.E)
            {
                if (ViewModel.TogglePdfExportCommand.CanExecute(null))
                {
                    ViewModel.TogglePdfExportCommand.Execute(null);
                    return true;
                }
            }

            if (key == Key.Enter)
            {
                ViewModel.AddPageCommand.Execute(null);
                return true;
            }

            if (key is Key.D0 or Key.NumPad0)
            {
                ViewModel.ResetZoomCommand.Execute(null);
                return true;
            }

            if (key == Key.C)
            {
                if (ViewModel.CopySelectedCommand.CanExecute(null))
                {
                    ViewModel.CopySelectedCommand.Execute(null);
                    return true;
                }
            }

            if (key == Key.V)
            {
                ViewModel.PasteClipboardCommand.Execute(null);
                return true;
            }

            if (key == Key.Z)
            {
                if (ViewModel.UndoCommand.CanExecute(null))
                {
                    ViewModel.UndoCommand.Execute(null);
                    return true;
                }
            }
            else if (key == Key.Y)
            {
                if (ViewModel.RedoCommand.CanExecute(null))
                {
                    ViewModel.RedoCommand.Execute(null);
                    return true;
                }
            }
        }

        return false;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.OriginalSource is System.Windows.Controls.TextBox or Wpf.Ui.Controls.TextBox)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        if (e.Key == Key.Space && !e.IsRepeat)
        {
            ViewportScrollViewer?.SetSpaceKeyDown(true);
            e.Handled = true;
            return;
        }

        if (HandleNavigationKey(e.Key, Keyboard.Modifiers))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Key == Key.Space)
        {
            ViewportScrollViewer?.SetSpaceKeyDown(false);
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyUp(e);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        ViewportScrollViewer?.SetSpaceKeyDown(false);
        base.OnDeactivated(e);
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

    private void HelpDialogBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, HelpDialogOverlay))
        {
            ViewModel.CloseHelpCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void HelpDialogCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
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

    private void ColorSwatchButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ColorSwatchOption swatch)
        {
            if (swatch.IsCustom)
            {
                ViewModel.Toolbar.RemoveCustomColorCommand.Execute(swatch.Hex);
                e.Handled = true;
            }
        }
    }

    private void ColorSpectrumBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement spectrumElement)
        {
            spectrumElement.CaptureMouse();
            UpdateSpectrumSelection(spectrumElement, e.GetPosition(spectrumElement));
            e.Handled = true;
        }
    }

    private void ColorSpectrumBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement spectrumElement && spectrumElement.IsMouseCaptured)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateSpectrumSelection(spectrumElement, e.GetPosition(spectrumElement));
                e.Handled = true;
            }
            else
            {
                spectrumElement.ReleaseMouseCapture();
            }
        }
    }

    private void ColorSpectrumBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement spectrumElement && spectrumElement.IsMouseCaptured)
        {
            spectrumElement.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void UpdateSpectrumSelection(FrameworkElement element, Point position)
    {
        var width = element.ActualWidth;
        var height = element.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var saturation = Math.Clamp(position.X / width, 0.0, 1.0);
        var value = Math.Clamp(1.0 - (position.Y / height), 0.0, 1.0);
        ViewModel.Toolbar.SetCustomColorSaturationAndValue(saturation, value);
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
