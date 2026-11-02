using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InkSlate.App.ViewModels;

namespace InkSlate.App.Views.Components;

public partial class HelpShortcutsOverlay : UserControl
{
    public HelpShortcutsOverlay()
    {
        InitializeComponent();
    }

    private void HelpDialogBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, HelpDialogOverlay))
        {
            if (DataContext is MainViewModel vm)
            {
                vm.CloseHelpCommand.Execute(null);
            }
            e.Handled = true;
        }
    }

    private void HelpDialogCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}

