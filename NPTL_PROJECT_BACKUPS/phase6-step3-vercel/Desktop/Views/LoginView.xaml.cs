using System.Windows.Controls;
using System.Windows.Input;
using NPTELManagement.Desktop.ViewModels;

namespace NPTELManagement.Desktop.Views;

public partial class LoginView : UserControl
{
    private bool _isSyncingPassword;

    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += LoginView_DataContextChanged;
    }

    private void LoginView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LoginViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }
        if (e.NewValue is LoginViewModel newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is LoginViewModel vm)
        {
            if (e.PropertyName == nameof(LoginViewModel.Password) && !_isSyncingPassword)
            {
                _isSyncingPassword = true;
                if (TxtPasswordMasked.Password != vm.Password)
                {
                    TxtPasswordMasked.Password = vm.Password ?? string.Empty;
                }
                _isSyncingPassword = false;
            }
            else if (e.PropertyName == nameof(LoginViewModel.IsPasswordVisible))
            {
                if (vm.IsPasswordVisible)
                {
                    TxtPasswordPlain.Text = TxtPasswordMasked.Password;
                    TxtPasswordPlain.Focus();
                    TxtPasswordPlain.CaretIndex = TxtPasswordPlain.Text.Length;
                }
                else
                {
                    TxtPasswordMasked.Password = TxtPasswordPlain.Text;
                    TxtPasswordMasked.Focus();
                }
            }
        }
    }

    private void TxtPasswordMasked_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isSyncingPassword) return;

        if (DataContext is LoginViewModel vm)
        {
            _isSyncingPassword = true;
            vm.Password = TxtPasswordMasked.Password;
            _isSyncingPassword = false;
        }
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is LoginViewModel vm && vm.LoginCommand.CanExecute(null))
            {
                vm.LoginCommand.Execute(null);
            }
        }
    }
}
