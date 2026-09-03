using System.Windows;
using QuickStartup.Services;
using QuickStartup.ViewModels;

namespace QuickStartup.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(ProfileService profileService)
    {
        InitializeComponent();

        var vm = new SettingsViewModel(profileService)
        {
            CloseAction = () => DialogResult = true
        };
        DataContext = vm;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
