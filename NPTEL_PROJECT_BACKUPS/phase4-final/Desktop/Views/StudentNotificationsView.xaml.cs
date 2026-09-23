using System.Windows;
using System.Windows.Controls;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Views;

public partial class StudentNotificationsView : UserControl
{
    public StudentNotificationsView()
    {
        InitializeComponent();
    }

    private async void StudentNotificationsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudentNotificationsViewModel vm && vm.Notifications.Count == 0 && !vm.IsLoading)
        {
            await vm.LoadNotificationsAsync();
        }
    }
}
