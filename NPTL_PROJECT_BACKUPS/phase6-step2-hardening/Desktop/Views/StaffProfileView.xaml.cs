using System.Windows;
using System.Windows.Controls;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Views;

public partial class StaffProfileView : UserControl
{
    public StaffProfileView()
    {
        InitializeComponent();
        Loaded += StaffProfileView_Loaded;
    }

    private async void StaffProfileView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StaffProfileViewModel vm && vm.Profile == null)
        {
            await vm.LoadProfileAsync();
        }
    }
}
