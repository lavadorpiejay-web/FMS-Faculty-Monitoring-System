using FacultyMonitoring.Models;
using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

/// <summary>READS Directory: one Dean, Chairperson or Faculty member. The person is passed by the Directory page as the "person" parameter.</summary>
public partial class PersonDetailPage : ContentPage, IQueryAttributable
{
    private readonly PersonDetailViewModel _vm;

    public PersonDetailPage(PersonDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("person", out var value) && value is DirectoryEntry entry)
            _vm.SetPerson(entry);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
