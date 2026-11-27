namespace FacultyMonitoring.Services;

public sealed record School(string Name, IReadOnlyList<string> Departments);

public static class AcademicCatalog
{
   
    public const string ImsOffice = "IMS Office";

    public const string DefaultCampus = "Main Campus";

    /// <summary>Campuses READS Scholars can be assigned to. Edit this list to match the university.</summary>
    public static readonly IReadOnlyList<string> Campuses = new List<string>
    {
        "Main Campus", "Basak Campus", "Quadricentennial Campus", "Balamban Campus",
    };

    /// <summary>School names used by the floor-ownership table (CampusLayout) and elsewhere.</summary>
    public const string SeaName = "School of Engineering and Architecture";
    public const string SbmName = "School of Business and Management";

    /// <summary>
    /// The Center for Religious Education. It is NOT under any school: it is an independent department that handles the ReEd subjects.
    /// Its accounts keep this name in both the School and the Department fields, so every school/department comparison in the app
    /// keeps working. It has a Chairperson (approved by IMS Personnel) and Faculty, but no Dean, and it owns no rooms.
    /// </summary>
    public const string CreName = "Center for Religious Education (CRE)";

    public static readonly IReadOnlyList<School> Schools = new List<School>
    {
        new(SeaName, new[]
        {
            "Department of Electrical Engineering",
            "Department of Computer Engineering",
            "Department of Civil Engineering",
            "Department of Industrial Engineering",
            "Department of Mechanical Engineering",
            "Department of Architecture",
            "Department of Electronics Engineering",
            "Department of Engineering, Mathematics, and Enhancement Program",
        }),

        new("School of Law", new[]
        {
            "Juris Doctor Program",
        }),

        new("School of Arts and Sciences", new[]
        {
            "Department of Communication, Languages, and Literature",
            "Department of Mathematics and Sciences",
            "Department of Social Sciences and Philosophy",
            "Department of Psychology and Library Information Science",
        }),

        new(SbmName, new[]
        {
            "Department of Accountancy and Finance",
            "Department of Business and Entrepreneurship",
            "Department of Marketing and Human Resource Management",
            "Department of Tourism and Hospitality Management",
        }),

        new("School of Allied Medical Sciences", new[]
        {
            "Department of Nursing",
            "Department of Medical Laboratory Science / Medical Technology",
        }),

        new("School of Computer Science", new[]
        {
            "Department of Computer Science",
            "Department of Information Technology",
        }),

        new("School of Education", new[]
        {
            "Department of Teacher Education",
            "Department of Physical Education",
            "Department of Special Needs Education",
        }),
    };

    /// <summary>Departments that belong to no school.</summary>
    public static readonly IReadOnlyList<string> IndependentUnits = new List<string> { CreName };

    public static IReadOnlyList<string> SchoolNames { get; } = Schools.Select(s => s.Name).ToList();

    /// <summary>Every school plus the independent units (CRE). Used where a unit may be chosen: Faculty and Chairperson registration, schedules, READS assignment.</summary>
    public static IReadOnlyList<string> UnitNames { get; } = Schools.Select(s => s.Name).Concat(IndependentUnits).ToList();

    public static IReadOnlyList<string> AllDepartments { get; } = Schools.SelectMany(s => s.Departments).Concat(IndependentUnits).ToList();

    public static bool IsIndependent(string? unit) => unit is not null && IndependentUnits.Contains(unit);

    public static IReadOnlyList<string> DepartmentsOf(string? school) =>
        IsIndependent(school) ? new[] { school! }
        : Schools.FirstOrDefault(s => s.Name == school)?.Departments ?? Array.Empty<string>();

    /// <summary>A real school (Dean seats, "school you study in"). The independent units are not schools.</summary>
    public static bool IsValidSchool(string? school) => Schools.Any(s => s.Name == school);

    /// <summary>A school or an independent unit such as CRE.</summary>
    public static bool IsValidUnit(string? unit) => IsValidSchool(unit) || IsIndependent(unit);

    public static bool IsValidDepartment(string? school, string? department) =>
        DepartmentsOf(school).Contains(department ?? "");

    /// <summary>Short name for labels: "School of Engineering and Architecture" gives "SEA", "School of Business and Management" gives "SBM", CRE gives "CRE".</summary>
    public static string ShortName(string? unit)
    {
        if (string.IsNullOrEmpty(unit)) return "";
        if (IsIndependent(unit)) return "CRE";
        var letters = unit.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !w.Equals("of", StringComparison.OrdinalIgnoreCase) && !w.Equals("and", StringComparison.OrdinalIgnoreCase))
            .Select(w => char.ToUpperInvariant(w[0]));
        return string.Concat(letters);
    }

    /// <summary>"School · Department" for profile lines, without repeating a name when both are the same (CRE).</summary>
    public static string UnitLine(string? school, string? department) =>
        string.Join(" · ", new[] { school, department }.Where(x => !string.IsNullOrEmpty(x)).Distinct()!);
}
