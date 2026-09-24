using System.Collections.ObjectModel;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class StudentCoursesViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;
    private ObservableCollection<StudentCourseDto> _courses = new();

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ObservableCollection<StudentCourseDto> Courses
    {
        get => _courses;
        set
        {
            if (SetProperty(ref _courses, value))
            {
                OnPropertyChanged(nameof(HasCourses));
                OnPropertyChanged(nameof(HasNoCourses));
            }
        }
    }

    public bool HasCourses => Courses.Count > 0;
    public bool HasNoCourses => !IsLoading && Courses.Count == 0;

    public ICommand RefreshCommand { get; }
    public ICommand ViewDetailsCommand { get; }

    public StudentCoursesViewModel(IApiClient apiClient, INavigationService navigationService)
    {
        _apiClient = apiClient;
        _navigationService = navigationService;

        RefreshCommand = new AsyncRelayCommand(LoadCoursesAsync);
        ViewDetailsCommand = new RelayCommand(obj => ViewDetails(obj as StudentCourseDto));
    }

    public async Task LoadCoursesAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var courses = await _apiClient.GetStudentCoursesAsync();
            Courses = new ObservableCollection<StudentCourseDto>(courses);
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            ErrorMessage = "Unable to load student enrolled courses.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasCourses));
            OnPropertyChanged(nameof(HasNoCourses));
        }
    }

    private void ViewDetails(StudentCourseDto? course)
    {
        if (course == null) return;
        _navigationService.NavigateShellTo(new StudentCourseDetailsViewModel(_apiClient, _navigationService, course.RegistrationId));
    }
}
