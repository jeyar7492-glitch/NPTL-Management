using System.Collections.ObjectModel;
using System.Windows.Input;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class StaffStudentDetailsViewModel : ViewModelBase
{
    private readonly Guid _studentId;
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;

    private StaffStudentDetailsDto? _student;
    private StaffStudentCourseDto? _selectedCourse;
    private ObservableCollection<StudentTimelineItemDto> _timeline = new();

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

    public StaffStudentDetailsDto? Student
    {
        get => _student;
        set
        {
            if (SetProperty(ref _student, value))
            {
                OnPropertyChanged(nameof(StudentDisplayName));
                OnPropertyChanged(nameof(StudentDisplayRegisterNo));
                OnPropertyChanged(nameof(HasCourses));
                OnPropertyChanged(nameof(HasNoCourses));
            }
        }
    }

    public string StudentDisplayName => Student?.Name ?? "—";
    public string StudentDisplayRegisterNo => Student?.RegisterNumber ?? "—";
    public bool HasCourses => Student?.Courses != null && Student.Courses.Count > 0;
    public bool HasNoCourses => !IsLoading && !HasError && !HasCourses;

    public StaffStudentCourseDto? SelectedCourse
    {
        get => _selectedCourse;
        set
        {
            if (SetProperty(ref _selectedCourse, value))
            {
                _ = LoadTimelineForCourseAsync(value);
            }
        }
    }

    public ObservableCollection<StudentTimelineItemDto> Timeline
    {
        get => _timeline;
        set => SetProperty(ref _timeline, value);
    }

    public ICommand BackCommand { get; }
    public ICommand RefreshCommand { get; }

    public StaffStudentDetailsViewModel(Guid studentId, IApiClient apiClient, INavigationService navigationService)
    {
        _studentId = studentId;
        _apiClient = apiClient;
        _navigationService = navigationService;

        BackCommand = new RelayCommand(() => _navigationService.NavigateShellTo<StaffStudentsViewModel>());
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);

        _ = LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var student = await _apiClient.GetStaffStudentDetailsAsync(_studentId);
            Student = student;

            if (student?.Courses != null && student.Courses.Count > 0)
            {
                SelectedCourse = student.Courses[0];
            }
            else
            {
                Timeline.Clear();
            }
        }
        catch (ApiException ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            ErrorMessage = "Failed to load student details.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadTimelineForCourseAsync(StaffStudentCourseDto? course)
    {
        if (course == null)
        {
            Timeline.Clear();
            return;
        }

        try
        {
            var timelineItems = await _apiClient.GetStaffStudentTimelineAsync(_studentId, course.RegistrationId);
            Timeline.Clear();
            if (timelineItems != null)
            {
                foreach (var item in timelineItems)
                {
                    Timeline.Add(item);
                }
            }
        }
        catch
        {
            // Timeline fallback
            Timeline.Clear();
        }
    }
}
