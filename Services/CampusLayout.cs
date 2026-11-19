namespace FacultyMonitoring.Services;

/// <summary>One floor (or basement level) of a building. AssignedSchool = the school assigned to that floor; its Dean approves room changes. Null = not assigned yet.</summary>
public sealed record FloorPlan(string Floor, string? AssignedSchool);

/// <summary>A building of a campus. A building is a place, not owned by a school. Enabled = its rooms are created in the starting data.</summary>
public sealed record BuildingPlan(string Code, string Name, IReadOnlyList<FloorPlan> Floors, bool Enabled);

/// <summary>
/// Main Campus layout. Building codes: MC, SN, SA, SJ. Room names are "&lt;floor&gt;-&lt;two-digit number&gt;",
/// for example B-1-01 (upper basement), B-2-01 (lower basement), 2-01 (second floor), 4-01 (fourth floor).
/// Rooms are fixed: they are created from this table, never typed in. IMS Personnel can change the school assigned to a floor on the Rooms tab.
/// </summary>
public static class CampusLayout
{
    /// <summary>Assumed number of classrooms on each floor of each building.</summary>
    public const int RoomsPerFloor = 10;

    private static string Engineering => AcademicCatalog.Schools[0].Name;

    public static IReadOnlyList<BuildingPlan> MainCampus { get; } = new List<BuildingPlan>
    {
        // Mount Carmel: classrooms on the 4th floor only.
        new("MC", "Mount Carmel Building", new List<FloorPlan> { new("4", null) }, Enabled: true),

        // San Nicolas: only the 2nd and 3rd floors are used.
        new("SN", "San Nicolas Building", new List<FloorPlan> { new("2", null), new("3", null) }, Enabled: true),

        // San Agustin: where the School of Engineering and Architecture is situated.
        // B-1 = upper basement, B-2 = lower basement. The 2nd floor is also used by engineering classes,
        // but other schools may use its rooms too because rooms are limited.
        new("SA", "San Agustin Building", new List<FloorPlan>
        {
            new("B-1", Engineering), new("B-2", Engineering), new("2", Engineering),
        }, Enabled: true),

        // San Jose: only the 3rd to 5th floors are used.
        new("SJ", "San Jose Building", new List<FloorPlan> { new("3", null), new("4", null), new("5", null) }, Enabled: true),
    };

    public static string RoomName(string floor, int number) => $"{floor}-{number:00}";

    /// <summary>"SA" becomes "SA - San Agustin Building" for pickers.</summary>
    public static string BuildingLabel(string code)
    {
        var b = MainCampus.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        return b is null ? code : $"{b.Code} - {b.Name}";
    }

    /// <summary>"SA - San Agustin Building" becomes "SA".</summary>
    public static string BuildingCode(string label) => label.Split(' ')[0];
}
