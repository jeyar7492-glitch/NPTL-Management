using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NPTELManagement.Core.DTOs;
using NPTELManagement.Desktop.Common;
using NPTELManagement.Desktop.Services;

namespace NPTELManagement.Desktop.ViewModels;

public class AdminReportsViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;
    private string? _statusMessage;

    private string _selectedReportType = "student-registration";
    private string _selectedYear = "All";
    private string _selectedClass = "All";
    private string _searchText = string.Empty;

    private AdminReportPreviewDto? _preview;
    private ObservableCollection<AdminReportItemDto> _items = new();

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

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string SelectedReportType
    {
        get => _selectedReportType;
        set
        {
            if (SetProperty(ref _selectedReportType, value))
            {
                _ = GeneratePreviewAsync();
            }
        }
    }

    public string SelectedYear
    {
        get => _selectedYear;
        set
        {
            if (SetProperty(ref _selectedYear, value))
            {
                _ = GeneratePreviewAsync();
            }
        }
    }

    public string SelectedClass
    {
        get => _selectedClass;
        set
        {
            if (SetProperty(ref _selectedClass, value))
            {
                _ = GeneratePreviewAsync();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public AdminReportPreviewDto? Preview
    {
        get => _preview;
        set => SetProperty(ref _preview, value);
    }

    public ObservableCollection<AdminReportItemDto> Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    public ICommand GeneratePreviewCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand ExportXlsxCommand { get; }
    public ICommand ExportCsvCommand { get; }

    public AdminReportsViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;

        GeneratePreviewCommand = new AsyncRelayCommand(GeneratePreviewAsync);
        ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync);
        ExportXlsxCommand = new AsyncRelayCommand(ExportXlsxAsync);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync);

        _ = GeneratePreviewAsync();
    }

    private AdminReportFilterDto BuildFilter()
    {
        int? year = int.TryParse(_selectedYear, out var y) ? y : null;
        string? cls = _selectedClass == "All" ? null : _selectedClass;
        string? search = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText.Trim();

        return new AdminReportFilterDto
        {
            ReportType = _selectedReportType,
            Department = "CSE",
            Year = year,
            ClassSection = cls,
            Search = search
        };
    }

    public async Task GeneratePreviewAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var filter = BuildFilter();
            var preview = await _apiClient.GetAdminReportPreviewAsync(filter);
            Preview = preview;
            Items = new ObservableCollection<AdminReportItemDto>(preview.Items);
            StatusMessage = $"Preview loaded: {preview.TotalRecords} record(s) matching criteria.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to generate preview: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExportPdfAsync()
    {
        var sfd = new SaveFileDialog
        {
            Title = "Export Institutional PDF Report",
            Filter = "PDF Document (*.pdf)|*.pdf",
            FileName = $"NPTEL_{_selectedReportType}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
        };

        if (sfd.ShowDialog() != true) return;

        IsLoading = true;
        try
        {
            StatusMessage = "Rendering institutional PDF via QuestPDF engine...";
            var bytes = await _apiClient.ExportAdminReportPdfAsync(BuildFilter());
            await File.WriteAllBytesAsync(sfd.FileName, bytes);
            StatusMessage = $"PDF report exported successfully to {Path.GetFileName(sfd.FileName)}.";

            var open = MessageBox.Show("Report exported successfully. Open file now?", "Export Complete", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export PDF: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExportXlsxAsync()
    {
        var sfd = new SaveFileDialog
        {
            Title = "Export Accreditation Excel Workbook",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = $"NPTEL_{_selectedReportType}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        };

        if (sfd.ShowDialog() != true) return;

        IsLoading = true;
        try
        {
            StatusMessage = "Generating Excel spreadsheet via ClosedXML engine...";
            var bytes = await _apiClient.ExportAdminReportXlsxAsync(BuildFilter());
            await File.WriteAllBytesAsync(sfd.FileName, bytes);
            StatusMessage = $"Excel workbook exported successfully to {Path.GetFileName(sfd.FileName)}.";

            var open = MessageBox.Show("Report exported successfully. Open file now?", "Export Complete", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export Excel: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExportCsvAsync()
    {
        var sfd = new SaveFileDialog
        {
            Title = "Export RFC 4180 CSV Dataset",
            Filter = "CSV Spreadsheet (*.csv)|*.csv",
            FileName = $"NPTEL_{_selectedReportType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (sfd.ShowDialog() != true) return;

        IsLoading = true;
        try
        {
            StatusMessage = "Compiling RFC 4180 CSV export...";
            var bytes = await _apiClient.ExportAdminReportCsvAsync(BuildFilter());
            await File.WriteAllBytesAsync(sfd.FileName, bytes);
            StatusMessage = $"CSV dataset exported successfully to {Path.GetFileName(sfd.FileName)}.";

            var open = MessageBox.Show("Report exported successfully. Open file now?", "Export Complete", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export CSV: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
