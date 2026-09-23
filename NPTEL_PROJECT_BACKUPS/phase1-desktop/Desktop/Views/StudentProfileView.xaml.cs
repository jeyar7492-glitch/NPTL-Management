using System.Windows;
using System.Windows.Controls;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Views;

public partial class StudentProfileView : UserControl
{
    public StudentProfileView()
    {
        InitializeComponent();
        Loaded += StudentProfileView_Loaded;
    }

    private async void StudentProfileView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudentProfileViewModel vm && vm.Profile == null)
        {
            await vm.LoadProfileAsync();
        }
    }
}
