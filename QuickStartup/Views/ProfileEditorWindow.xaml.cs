using System.Windows;
using QuickStartup.Models;
using QuickStartup.Services;
using QuickStartup.ViewModels;

namespace QuickStartup.Views;

public partial class ProfileEditorWindow : Window
{
    public ProfileEditorWindow(Profile profile, ProfileService profileService)
    {
        InitializeComponent();

        var vm = new ProfileEditorViewModel(profile, profileService)
        {
            CloseAction = () => DialogResult = true
        };
        DataContext = vm;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
