using System.Windows.Controls;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Views;

public partial class StaffStudentsView : UserControl
{
    public StaffStudentsView()
    {
        InitializeComponent();
    }

    private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row && row.Item is StaffStudentListDto student)
        {
            if (DataContext is StaffStudentsViewModel vm && vm.ViewDetailsCommand.CanExecute(student))
            {
                vm.ViewDetailsCommand.Execute(student);
            }
        }
    }
}
