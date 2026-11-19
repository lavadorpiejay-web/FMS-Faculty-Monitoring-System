using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FacultyMonitoring.Models;

/// <summary>A collapsible section holding a flat list of rows (e.g. one building's rooms, or one department's schedules).</summary>
public partial class RowGroup : ObservableObject
{
    public RowGroup(string key, string title, string subtitle, IReadOnlyList<ListRow> rows, bool expanded = false)
    {
        Key = key;
        Title = title;
        Subtitle = subtitle;
        Rows = rows;
        _isExpanded = expanded;
    }

    /// <summary>Stable identity so the open/closed state survives a reload.</summary>
    public string Key { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public IReadOnlyList<ListRow> Rows { get; }
    public bool HasSubtitle => Subtitle.Length > 0;
    public string CountText => Rows.Count == 1 ? "1 item" : $"{Rows.Count} items";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Arrow))]
    private bool _isExpanded;

    public string Arrow => IsExpanded ? "▼" : "▶";

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

/// <summary>A collapsible school section that contains one collapsible <see cref="RowGroup"/> per department.</summary>
public partial class SchoolGroup : ObservableObject
{
    public SchoolGroup(string title, IReadOnlyList<RowGroup> departments, bool expanded = false)
    {
        Title = title;
        Departments = departments;
        _isExpanded = expanded;
    }

    public string Title { get; }
    public IReadOnlyList<RowGroup> Departments { get; }
    public string CountText
    {
        get
        {
            var n = Departments.Sum(d => d.Rows.Count);
            return n == 1 ? "1 schedule" : $"{n} schedules";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Arrow))]
    private bool _isExpanded;

    public string Arrow => IsExpanded ? "▼" : "▶";

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}
