using System.Windows;

namespace HCC.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(ViewModels.MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
