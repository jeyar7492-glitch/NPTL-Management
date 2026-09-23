using System.Windows;
using System.Windows.Controls;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Views;

public partial class AdminProfileView : UserControl
{
    public AdminProfileView()
    {
        InitializeComponent();
        Loaded += AdminProfileView_Loaded;
    }

    private async void AdminProfileView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminProfileViewModel vm && vm.AdminData == null)
        {
            await vm.LoadProfileAsync();
        }
    }
}
