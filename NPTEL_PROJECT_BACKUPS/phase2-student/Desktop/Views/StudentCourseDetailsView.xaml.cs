using System.Windows;
using System.Windows.Controls;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Views;

public partial class StudentCourseDetailsView : UserControl
{
    public StudentCourseDetailsView()
    {
        InitializeComponent();
    }

    private async void StudentCourseDetailsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudentCourseDetailsViewModel vm && vm.Details == null && !vm.IsLoading)
        {
            await vm.LoadDetailsAsync();
        }
    }
}
