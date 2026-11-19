using CommunityToolkit.Mvvm.ComponentModel;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>Error / success messages and the busy flag shared by the form screens. The Banners control shows the messages.</summary>
public abstract partial class PageViewModelBase : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuccess))]
    private string _successMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);
    public bool IsNotBusy => !IsBusy;

    protected void ClearMessages()
    {
        ErrorMessage = "";
        SuccessMessage = "";
    }

    protected void Show(OperationResult result)
    {
        if (result.Success) SuccessMessage = result.Message;
        else ErrorMessage = result.Message;
    }
}
