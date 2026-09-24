using System.Windows;
using System.Windows.Controls;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Views;

public partial class StudentCoursesView : UserControl
{
    public StudentCoursesView()
    {
        InitializeComponent();
    }

    private async void StudentCoursesView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudentCoursesViewModel vm && vm.Courses.Count == 0 && !vm.IsLoading)
        {
            await vm.LoadCoursesAsync();
        }
    }
}
