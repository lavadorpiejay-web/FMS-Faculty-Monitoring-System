namespace FacultyMonitoring.Services;

/// <summary>One floor (or basement level) of a building. AssignedSchool = the school that OWNS the floor (null = nobody owns it).</summary>
public sealed record FloorPlan(string Floor, string? AssignedSchool);

/// <summary>A building of a campus. Enabled = its rooms are created in the starting data.</summary>
public sealed record BuildingPlan(string Code, string Name, IReadOnlyList<FloorPlan> Floors, bool Enabled);

/// <summary>
/// Main Campus layout. Building codes: MC, SN, SA, SJ. Room names are "&lt;floor&gt;-&lt;two-digit number&gt;",
/// for example B-1-01 (upper basement), B-2-01 (lower basement), 2-01 (second floor), 4-01 (fourth floor).
/// Rooms are fixed: they are created from this table, never typed in.
///
/// FLOOR OWNERSHIP: a school "owns" the floors it holds its classes on. Its own classes use them freely. Any other school that wants a
/// room on an owned floor must first get permission from the Dean of the owning school (see ChangeRequestService). Floors that nobody
/// owns need no permission. To change who owns a floor, edit the table below (the second argument of each FloorPlan).
/// The Center for Religious Education (CRE) is not a school and owns nothing.
/// </summary>
public static class CampusLayout
{
    /// <summary>Assumed number of classrooms on each floor of each building.</summary>
    public const int RoomsPerFloor = 10;

    private const string Sea = AcademicCatalog.SeaName;
    private const string Sbm = AcademicCatalog.SbmName;

    public static IReadOnlyList<BuildingPlan> MainCampus { get; } = new List<BuildingPlan>
    {
        // Mount Carmel: classrooms on the 4th floor only. Not owned.
        new("MC", "Mount Carmel Building", new List<FloorPlan> { new("4", null) }, Enabled: true),

        // San Nicolas: only the 2nd and 3rd floors are used. The 2nd floor belongs to the School of Business and Management.
        new("SN", "San Nicolas Building", new List<FloorPlan> { new("2", Sbm), new("3", null) }, Enabled: true),

        // San Agustin: the 2nd floor and both basement levels (B-1 upper, B-2 lower) belong to the School of Engineering and Architecture.
        new("SA", "San Agustin Building", new List<FloorPlan>
        {
            new("B-1", Sea), new("B-2", Sea), new("2", Sea),
        }, Enabled: true),

        // San Jose: only the 3rd to 5th floors are used. The 3rd floor belongs to the School of Business and Management.
        new("SJ", "San Jose Building", new List<FloorPlan> { new("3", Sbm), new("4", null), new("5", null) }, Enabled: true),
    };

    /// <summary>The school that owns this floor, or null when nobody owns it.</summary>
    public static string? OwnerOf(string building, string floor) =>
        MainCampus.FirstOrDefault(b => string.Equals(b.Code, building, StringComparison.OrdinalIgnoreCase))
            ?.Floors.FirstOrDefault(f => string.Equals(f.Floor, floor, StringComparison.OrdinalIgnoreCase))?.AssignedSchool;

    public static string? OwnerOf(Models.Classroom room) => OwnerOf(room.Building, room.Floor);

    /// <summary>True when the school must ask the owner's Dean before using this room: the floor is owned by a different school.</summary>
    public static bool NeedsPermission(string? school, Models.Classroom room) =>
        OwnerOf(room) is { } owner && !string.Equals(owner, school, StringComparison.OrdinalIgnoreCase);

    /// <summary>The floors a school owns, for example "SA floor B-1, SA floor B-2, SA floor 2".</summary>
    public static string FloorsOwnedBy(string school) =>
        string.Join(", ", MainCampus.SelectMany(b => b.Floors.Where(f => f.AssignedSchool == school).Select(f => $"{b.Code} floor {f.Floor}")));

    /// <summary>"owned by SEA" for a room on an owned floor, "" otherwise.</summary>
    public static string OwnerNote(Models.Classroom room) =>
        OwnerOf(room) is { } owner ? $"owned by {AcademicCatalog.ShortName(owner)}" : "";

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
