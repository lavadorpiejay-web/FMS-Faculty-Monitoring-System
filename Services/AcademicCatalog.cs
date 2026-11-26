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

    public static readonly IReadOnlyList<School> Schools = new List<School>
    {
        new("School of Engineering and Architecture", new[]
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

        new("School of Business and Management", new[]
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

    public static IReadOnlyList<string> SchoolNames { get; } = Schools.Select(s => s.Name).ToList();

    public static IReadOnlyList<string> AllDepartments { get; } = Schools.SelectMany(s => s.Departments).ToList();

    public static IReadOnlyList<string> DepartmentsOf(string? school) =>
        Schools.FirstOrDefault(s => s.Name == school)?.Departments ?? Array.Empty<string>();

    public static bool IsValidSchool(string? school) => Schools.Any(s => s.Name == school);

    public static bool IsValidDepartment(string? school, string? department) =>
        DepartmentsOf(school).Contains(department ?? "");
}
