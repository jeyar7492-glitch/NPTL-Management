namespace NPTELManagement.Desktop.ViewModels;

public class PlaceholderViewModel : ViewModelBase
{
    private string _title = "Module";
    private string _description = "This module is planned for subsequent project phases.";

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public PlaceholderViewModel() { }

    public PlaceholderViewModel(string title, string description)
    {
        Title = title;
        Description = description;
    }
}
