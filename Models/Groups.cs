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

/// <summary>One weekday tick box on the Schedules form. Start and End (12-hour text) are used when the days have different times.</summary>
public partial class DayChoice : ObservableObject
{
    public DayChoice(string name) => Name = name;
    public string Name { get; }
    public string ShortName => Name[..3];

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string? _start;
    [ObservableProperty] private string? _end;
}

/// <summary>Dean's Departments page: one department that opens on tap to show its Chairperson(s) and Faculty.</summary>
public partial class DepartmentSection : ObservableObject
{
    public DepartmentSection(DepartmentGroup group, bool expanded = false)
    {
        Group = group;
        _isExpanded = expanded;
    }

    public DepartmentGroup Group { get; }
    public string Department => Group.Department;
    public IReadOnlyList<DirectoryEntry> Chairpersons => Group.Chairpersons;
    public IReadOnlyList<DirectoryEntry> Faculty => Group.Faculty;
    public bool NoChairperson => Group.NoChairperson;
    public bool NoFaculty => Group.NoFaculty;
    public string CountText
    {
        get
        {
            var c = Group.Chairpersons.Count == 1 ? "1 chairperson" : $"{Group.Chairpersons.Count} chairpersons";
            return $"{c} · {Group.FacultyCountText}";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Arrow))]
    private bool _isExpanded;

    public string Arrow => IsExpanded ? "▼" : "▶";

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

/// <summary>READS Directory: one Chairperson. Tapping the name shows the Faculty of that Chairperson's department.</summary>
public partial class ChairpersonNode : ObservableObject
{
    public ChairpersonNode(DirectoryEntry entry, IReadOnlyList<DirectoryEntry> faculty, bool expanded = false)
    {
        Name = entry.Name;
        Faculty = faculty;
        _isExpanded = expanded;
    }

    public string Name { get; }
    public IReadOnlyList<DirectoryEntry> Faculty { get; }
    public bool NoFaculty => Faculty.Count == 0;
    public string CountText => Faculty.Count == 1 ? "1 faculty member" : $"{Faculty.Count} faculty members";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Arrow))]
    private bool _isExpanded;

    public string Arrow => IsExpanded ? "▼" : "▶";

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

/// <summary>READS Directory: one department listing its Chairperson(s). A department with no Chairperson shows its Faculty directly.</summary>
public sealed class DirectoryDepartment
{
    public DirectoryDepartment(string department, IReadOnlyList<ChairpersonNode> chairpersons, IReadOnlyList<DirectoryEntry> faculty)
    {
        Department = department;
        Chairpersons = chairpersons;
        Faculty = faculty;
    }

    public string Department { get; }
    public IReadOnlyList<ChairpersonNode> Chairpersons { get; }
    public IReadOnlyList<DirectoryEntry> Faculty { get; }
    public bool NoChairperson => Chairpersons.Count == 0;
    public bool NoFaculty => Faculty.Count == 0;
}
