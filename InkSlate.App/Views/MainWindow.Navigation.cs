using System.Windows.Input;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;

namespace InkSlate.App.Views;

public partial class MainWindow
{
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
            else if (key == Key.Escape && ViewModel.Toolbar.IsCustomPenEditorOpen)
            {
                ViewModel.Toolbar.CloseCustomPenEditorCommand.Execute(null);
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
                WindowState = WindowState == System.Windows.WindowState.Maximized
                    ? System.Windows.WindowState.Normal
                    : System.Windows.WindowState.Maximized;
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
                var pastePos = GetCurrentCanvasCursorPosition();
                ViewModel.PasteClipboardCommand.Execute(pastePos);
                foreach (var pcc in FindVisualChildren<Controls.PageCanvasControl>(this))
                {
                    if (pcc.PageId == ViewModel.ActivePage?.Id)
                    {
                        pcc.SyncSelectionFromViewModel();
                    }
                }
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

        if (ViewModel.Toolbar.TryActivateCustomPenByShortcut(key, modifiers))
        {
            return true;
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

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Space && !e.IsRepeat)
        {
            ViewportScrollViewer?.SetSpaceKeyDown(true);
            e.Handled = true;
            return;
        }

        if (HandleNavigationKey(key, Keyboard.Modifiers))
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
}

