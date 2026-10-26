using InkSlate.App.ViewModels;
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
    }

    public MainViewModel ViewModel { get; }
}
