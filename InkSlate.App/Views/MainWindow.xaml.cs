using System.Windows.Controls;
using System.Windows.Input;
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
            else if (key == Key.F5)
            {
                ViewModel.ToggleViewModeCommand.Execute(null);
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
        }
        else if (modifiers == ModifierKeys.Control)
        {
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
}
