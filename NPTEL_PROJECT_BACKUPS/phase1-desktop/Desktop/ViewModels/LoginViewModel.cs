using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public enum LoginRole
{
    Student,
    Staff,
    Admin
}

public class LoginViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;
    private readonly IAuthenticationSession _session;

    private LoginRole _selectedRole = LoginRole.Student;
    private string _identifier = string.Empty;
    private string _password = string.Empty;
    private bool _isPasswordVisible;
    private bool _isLoading;
    private string? _errorMessage;

    public LoginRole SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (SetProperty(ref _selectedRole, value))
            {
                OnPropertyChanged(nameof(IdentifierLabel));
                OnPropertyChanged(nameof(IdentifierWatermark));
                OnPropertyChanged(nameof(IsStudentRole));
                OnPropertyChanged(nameof(IsStaffRole));
                OnPropertyChanged(nameof(IsAdminRole));
                ErrorMessage = null;
            }
        }
    }

    public bool IsStudentRole
    {
        get => _selectedRole == LoginRole.Student;
        set { if (value) SelectedRole = LoginRole.Student; }
    }

    public bool IsStaffRole
    {
        get => _selectedRole == LoginRole.Staff;
        set { if (value) SelectedRole = LoginRole.Staff; }
    }

    public bool IsAdminRole
    {
        get => _selectedRole == LoginRole.Admin;
        set { if (value) SelectedRole = LoginRole.Admin; }
    }

    public string IdentifierLabel => _selectedRole switch
    {
        LoginRole.Student => "Register Number",
        LoginRole.Staff => "Staff ID",
        LoginRole.Admin => "Admin ID",
        _ => "Identifier"
    };

    public string IdentifierWatermark => _selectedRole switch
    {
        LoginRole.Student => "e.g. 951021104001",
        LoginRole.Staff => "e.g. CSE-STF-01",
        LoginRole.Admin => "e.g. ADM-CSE-01",
        _ => "Enter identifier"
    };

    public string Identifier
    {
        get => _identifier;
        set
        {
            if (SetProperty(ref _identifier, value))
            {
                ErrorMessage = null;
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ErrorMessage = null;
            }
        }
    }

    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand TogglePasswordVisibilityCommand { get; }

    public LoginViewModel(IApiClient apiClient, INavigationService navigationService, IAuthenticationSession? session = null)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;
        _session = session ?? AuthenticationSession.Instance;

        LoginCommand = new AsyncRelayCommand(ExecuteLoginAsync, CanExecuteLogin);
        TogglePasswordVisibilityCommand = new RelayCommand(() => IsPasswordVisible = !IsPasswordVisible);
    }

    private bool CanExecuteLogin()
    {
        return !IsLoading;
    }

    public async Task ExecuteLoginAsync()
    {
        ErrorMessage = null;

        var id = Identifier?.Trim();
        var pass = Password;

        if (string.IsNullOrWhiteSpace(id))
        {
            ErrorMessage = $"Please enter your {IdentifierLabel.ToLowerInvariant()}.";
            return;
        }

        if (string.IsNullOrWhiteSpace(pass))
        {
            ErrorMessage = "Please enter your password.";
            return;
        }

        IsLoading = true;

        try
        {
            AuthSuccessResponse authResult;

            switch (SelectedRole)
            {
                case LoginRole.Student:
                    authResult = await _apiClient.LoginStudentAsync(new StudentLoginDto
                    {
                        RegisterNumber = id,
                        Password = pass
                    });
                    break;

                case LoginRole.Staff:
                    authResult = await _apiClient.LoginStaffAsync(new StaffLoginDto
                    {
                        StaffId = id,
                        Password = pass
                    });
                    break;

                case LoginRole.Admin:
                    authResult = await _apiClient.LoginAdminAsync(new AdminLoginDto
                    {
                        AdminId = id,
                        Password = pass
                    });
                    break;

                default:
                    throw new InvalidOperationException("Unsupported login role.");
            }

            // Clear password from ViewModel memory
            Password = string.Empty;

            // Transition to Main Shell
            _navigationService.NavigateTo<MainViewModel>();
        }
        catch (UnauthorizedException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (AccessDeniedException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (NetworkUnavailableException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            ErrorMessage = "An unexpected error occurred during login. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
