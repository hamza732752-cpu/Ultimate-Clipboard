using System.Windows;
using UltimateClipboard.ViewModels;

namespace UltimateClipboard.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
