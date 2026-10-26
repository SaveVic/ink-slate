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
                ViewModel.Toolbar.SelectToolCommand.Execute(ToolMode.Eraser);
                return true;
            }
            else if (key == Key.L)
            {
                ViewModel.Toolbar.SelectToolCommand.Execute(ToolMode.Lasso);
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

        if (HandleNavigationKey(e.Key, Keyboard.Modifiers))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
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

    private void TitleDisplayPill_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ViewModel.BeginEditTitleCommand.Execute(null);
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
