using System.Windows;
using System.Windows.Controls;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Views;

public partial class StudentDashboardView : UserControl
{
    public StudentDashboardView()
    {
        InitializeComponent();
        Loaded += StudentDashboardView_Loaded;
    }

    private async void StudentDashboardView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudentDashboardViewModel vm && vm.Profile == null)
        {
            await vm.LoadStudentProfileAsync();
        }
    }
}
