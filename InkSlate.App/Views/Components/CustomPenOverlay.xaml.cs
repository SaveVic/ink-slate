using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InkSlate.App.ViewModels;

namespace InkSlate.App.Views.Components;

public partial class CustomPenOverlay : UserControl
{
    public CustomPenOverlay()
    {
        InitializeComponent();
    }

    private void CustomPenPresetButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is CustomPenPresetViewModel preset)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Toolbar.OpenEditCustomPenEditorCommand.Execute(preset);
                e.Handled = true;
            }
        }
    }

    private void CustomPenShortcutTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System)
        {
            return;
        }

        if (key == Key.Tab)
        {
            return;
        }

        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (key == Key.Escape)
        {
            vm.Toolbar.CloseCustomPenEditorCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (key == Key.Enter)
        {
            vm.Toolbar.SaveCustomPenPresetCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if ((key is Key.Back or Key.Delete) && Keyboard.Modifiers == ModifierKeys.None)
        {
            vm.Toolbar.ClearCustomPenEditorShortcutCommand.Execute(null);
            e.Handled = true;
            return;
        }

        vm.Toolbar.TrySetCustomPenEditorShortcutFromKey(key, Keyboard.Modifiers);
        e.Handled = true;
    }
}

