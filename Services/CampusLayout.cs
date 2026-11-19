namespace FacultyMonitoring.Services;

/// <summary>One floor (or basement level) of a building. School = the owning school; null means shared between schools.</summary>
public sealed record FloorPlan(string Floor, string? School);

/// <summary>A building of a campus. Enabled = its rooms are created in the starting data.</summary>
public sealed record BuildingPlan(string Code, string Name, IReadOnlyList<FloorPlan> Floors, bool Enabled);

/// <summary>
/// Main Campus layout. Building codes: MC, SN, SA, SJ. Room names are "&lt;floor&gt;-&lt;two-digit number&gt;",
/// for example B-1-01 (upper basement), B-2-01 (lower basement) and 2-01 (second floor).
/// To switch on another building, set Enabled to true (and fill in its floors); the starting data picks it up.
/// </summary>
public static class CampusLayout
{
    /// <summary>Assumed number of classrooms on each floor of each building.</summary>
    public const int RoomsPerFloor = 10;

    private static string Engineering => AcademicCatalog.Schools[0].Name;

    public static IReadOnlyList<BuildingPlan> MainCampus { get; } = new List<BuildingPlan>
    {
        // Mount Carmel: floors not decided yet, so nothing is created.
        new("MC", "Mount Carmel Building", new List<FloorPlan>(), Enabled: false),

        // San Nicolas: only the 2nd and 3rd floors are used. Owning school not decided yet (null = shared).
        new("SN", "San Nicolas Building", new List<FloorPlan> { new("2", null), new("3", null) }, Enabled: false),

        // San Agustin: School of Engineering and Architecture. Both basements belong to the school.
        // The 2nd floor is used by engineering classes but the school using it varies (limited rooms), so it stays shared.
        new("SA", "San Agustin Building", new List<FloorPlan>
        {
            new("B-1", Engineering),   // upper basement
            new("B-2", Engineering),   // lower basement
            new("2", null),            // shared
        }, Enabled: true),

        // San Jose: only the 3rd to 5th floors are used. Owning school not decided yet.
        new("SJ", "San Jose Building", new List<FloorPlan> { new("3", null), new("4", null), new("5", null) }, Enabled: false),
    };

    public static string RoomName(string floor, int number) => $"{floor}-{number:00}";
}
