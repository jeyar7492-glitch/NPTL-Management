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

public class AdminCertificatesViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;
    private string? _statusMessage;

    private string _searchText = string.Empty;
    private string _selectedStatus = "All";

    private int _page = 1;
    private int _pageSize = 50;
    private int _totalCount;

    private ObservableCollection<AdminCertificateDto> _certificates = new();
    private AdminCertificateDto? _selectedCertificate;

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

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _page = 1;
                _ = LoadCertificatesAsync();
            }
        }
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value))
            {
                _page = 1;
                _ = LoadCertificatesAsync();
            }
        }
    }

    public ObservableCollection<AdminCertificateDto> Certificates
    {
        get => _certificates;
        set => SetProperty(ref _certificates, value);
    }

    public AdminCertificateDto? SelectedCertificate
    {
        get => _selectedCertificate;
        set => SetProperty(ref _selectedCertificate, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        set => SetProperty(ref _totalCount, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand UploadCertificateCommand { get; }
    public ICommand VerifyCertificateCommand { get; }
    public ICommand ViewSignedUrlCommand { get; }

    public AdminCertificatesViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;

        RefreshCommand = new AsyncRelayCommand(LoadCertificatesAsync);
        UploadCertificateCommand = new AsyncRelayCommand<AdminCertificateDto>(UploadCertificateAsync);
        VerifyCertificateCommand = new AsyncRelayCommand<AdminCertificateDto>(VerifyCertificateAsync);
        ViewSignedUrlCommand = new AsyncRelayCommand<AdminCertificateDto>(ViewSignedUrlAsync);

        _ = LoadCertificatesAsync();
    }

    public async Task LoadCertificatesAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var statusFilter = _selectedStatus == "All" ? null : _selectedStatus;
            var search = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText;

            var result = await _apiClient.GetAdminCertificatesAsync(search, statusFilter, _page, _pageSize);
            Certificates = new ObservableCollection<AdminCertificateDto>(result.Items);
            TotalCount = result.TotalCount;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to load certificates: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UploadCertificateAsync(AdminCertificateDto? cert)
    {
        var target = cert ?? SelectedCertificate;
        if (target == null) return;

        var openFileDialog = new OpenFileDialog
        {
            Title = $"Select Certificate PDF for {target.StudentName} ({target.CourseCode})",
            Filter = "PDF Documents (*.pdf)|*.pdf",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() != true) return;

        var filePath = openFileDialog.FileName;
        if (!File.Exists(filePath))
        {
            MessageBox.Show("Selected file does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > 10 * 1024 * 1024)
        {
            MessageBox.Show("Certificate PDF must not exceed 10 MB.", "File Size Limit", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoading = true;
        try
        {
            StatusMessage = "Uploading certificate to private Supabase cloud storage...";
            await _apiClient.UploadAdminCertificateAsync(target.RegistrationId, filePath);
            StatusMessage = "Certificate uploaded and registered successfully.";
            await LoadCertificatesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Upload failed: {ex.Message}", "Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task VerifyCertificateAsync(AdminCertificateDto? cert)
    {
        var target = cert ?? SelectedCertificate;
        if (target == null) return;

        if (!target.HasFile)
        {
            MessageBox.Show("Cannot verify certificate before a PDF file is uploaded.", "Verification Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var newStatus = target.VerifiedStatus == "Verified" ? "Pending" : "Verified";
        var confirmMsg = target.VerifiedStatus == "Verified"
            ? $"Mark certificate for '{target.StudentName} - {target.CourseCode}' back to 'Pending'?"
            : $"Confirm academic verification for '{target.StudentName} - {target.CourseCode}'? This certifies academic credit entitlement.";

        var confirm = MessageBox.Show(confirmMsg, "Certificate Verification", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            await _apiClient.UpdateAdminCertificateStatusAsync(target.RegistrationId, newStatus, newStatus == "Verified" ? DateTime.UtcNow : null);
            StatusMessage = $"Certificate status updated to '{newStatus}'.";
            await LoadCertificatesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Verification update failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ViewSignedUrlAsync(AdminCertificateDto? cert)
    {
        var target = cert ?? SelectedCertificate;
        if (target == null) return;

        if (!target.HasFile)
        {
            MessageBox.Show("No certificate file is attached to this registration.", "File Missing", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsLoading = true;
        try
        {
            StatusMessage = "Generating short-lived secure signed URL from Supabase Storage...";
            var access = await _apiClient.GetCertificateAccessAsync(target.CertificateId);

            if (string.IsNullOrWhiteSpace(access.AccessUrl))
            {
                MessageBox.Show("Failed to obtain secure signed URL from storage provider.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Launch browser / viewer securely with short-lived signed URL
            Process.Start(new ProcessStartInfo(access.AccessUrl) { UseShellExecute = true });
            StatusMessage = $"Certificate access link generated. Valid until {access.ExpiresAt.ToLocalTime():HH:mm:ss}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to obtain secure certificate access: {ex.Message}", "Access Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
