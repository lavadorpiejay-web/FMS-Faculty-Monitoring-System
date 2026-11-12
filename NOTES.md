# FMS: Faculty Monitoring System

Project notes. Keep this file in the project folder (`D:\FMS\FacultyMonitoring\NOTES.md`) and update it after each milestone. Attach it, with the project zip, when starting a new chat.

Last updated: 2026-10-25

## What it is
- Android app for monitoring faculty, built with .NET MAUI in Visual Studio 2026.
- Name: **FMS** / **Faculty Monitoring System**. The old names "FAMS" and "Faculty Attendance Monitoring System" are retired. Do not use them.
- Project folder: `D:\FMS\FacultyMonitoring`.
- Data is stored locally as JSON files (users, faculty, subjects, classrooms). There is no server yet.

## Tech
- .NET MAUI, MVVM with CommunityToolkit.Mvvm.
- Local JSON storage through `JsonStorageService`. Passwords are salted hashes (`PasswordHasher`).
- Navigation through `AppShell`, with a navigation guard that blocks any area outside the signed-in role (`RolePolicy.IsRouteAllowed`).

## Roles (hierarchy: Dean > Chairperson > Faculty; IMS Personnel > READS Scholar)
| Role | Registers with | Notes |
|---|---|---|
| Faculty | School and department | Reports to Chairperson |
| Chairperson | School and department | One chairperson per department (not enforced yet) |
| Dean | School only (no department) | One dean per school |
| READS Scholar | Nothing; placed in IMS Office automatically | Working student; attendance checking not enabled yet |
| IMS Personnel | Nothing; placed in IMS Office automatically | Manages the app; supervises READS Scholars in the IMS Office |

## Schools and departments
Defined in one place: `Services\AcademicCatalog.cs`. To add a school or department, add an entry there; nothing else needs to change.

Currently only:
- **School of Engineering and Architecture**
  - Department of Electrical Engineering
  - Department of Computer Engineering
  - Department of Civil Engineering
  - Department of Industrial Engineering
  - Department of Mechanical Engineering
  - Department of Architecture
  - Department of Electronics Engineering

More schools will be added later. IT, Computer Science and College of Computing were removed from the list on purpose.

## What the "System checks" tab is
The StatusPage runs per-role verification checks left over from the foundation milestone: signed-in session, local data files (users, faculty, subjects, classrooms) readable with record counts, and the Android platform. It shows a green check or red cross for each. It is useful to confirm the app's local storage works; it is not a feature for end users.

## Key files
- `Models\Models.cs`: UserAccount (with School and Department), FacultyMember, Subject, Classroom, RegistrationRequest, etc.
- `Services\AcademicCatalog.cs`: schools and departments.
- `Services\SeedData.cs`: starting data written on first launch: dummy accounts and faculty (debug builds only), one sample subject per department, and classrooms.
- `Services\AuthService.cs`: sign-in, registration, validation against the catalog, lockout after 5 failed attempts.
- `Services\RolePolicy.cs`: what each role can see and its routes.
- `Services\ReadinessService.cs`: per-role verification checks.
- `ViewModels\RegisterViewModel.cs` and `Views\RegisterPage.xaml`: registration (school picker, then department picker filtered by school).
- `ViewModels\LoginViewModel.cs`, `AppShell.xaml.cs`: login and role navigation.
- `Services\ScholarAssignmentService.cs`, `ViewModels\ScholarAssignmentsViewModel.cs`, `Views\AssignmentsPage.xaml`: IMS Personnel assigns READS Scholars (UserAccount.AssignedSchool / AssignedDepartment).
- `Services\DirectoryService.cs` and the Directory / SchoolDirectory / DepartmentFaculty view models and pages: read-only directories for READS Scholar (assigned department), Dean (own school, all departments) and Chairperson (own department). Always filtered by the signed-in account's stored school and department, never by the screen.
- `Resources\AppIcon\appicon.svg` and `appiconfg.svg`: FMS app icon (background and logo). `Resources\Splash\splash.png`: FMS splash image (clock-and-pen logo). Background colour is set in `FacultyMonitoring.csproj` (`#0C2D23`).

## Accounts
**Debug builds (running from Visual Studio)** create dummy accounts on first launch so login is quick. All share the password `SeedData.DummyPassword` (the full list is in `DUMMY_ACCOUNTS.txt` in the project folder; nothing is shown on the login screen):
- `ims01`: IMS Personnel
- `reads01`: READS Scholar, already assigned to the Department of Computer Engineering
- `dean.sea`: Dean of the School of Engineering and Architecture
- `chair.CODE` and `faculty.CODE`: Chairperson and Faculty of each department, where CODE is `ee` (Electrical), `cpe` (Computer), `ce` (Civil), `ie` (Industrial), `me` (Mechanical), `arch` (Architecture), `ece` (Electronics)

Accounts are generated from `Services\AcademicCatalog.cs`, so a new school or department automatically gets a dummy Dean, Chairperson and Faculty.

**Release builds** create no accounts; everyone registers on the Register screen.

Accounts are written only on first launch and live in the app's private storage, so uninstall the app to regenerate them. Uninstalling also erases any accounts you registered by hand.

## Milestones so far
1. Milestone 1: foundation (login, roles, local JSON storage).
2. Milestone 2: role screens and readiness checks.
3. Milestone 2b: department picker fix. Splash screen changed to FMS.
4. Milestone 3: school and department registration, new sample data.
5. Milestone 4: IMS Personnel assigns READS Scholars to a school and department; READS Scholars get a read-only Directory (Dean, Chairperson, Faculty of their assigned department).
6. Milestone 4b: removed all test accounts and sample faculty; login hint removed.
7. Milestone 4c: FMS app icon (clock-and-pen logo), app name shown as "FMS", the Status / System / Platform / Storage tab renamed "System checks" for every role.
8. Milestone 5: Dean sees every department of their school with its Chairperson(s) and Faculty; Chairperson sees the Faculty of their department (both read-only).
9. Milestone 6: dummy accounts (debug builds only) for every school, department, READS Scholar and IMS Personnel.

## Open items / next
- READS Scholar and IMS Personnel schedule features (not started; READS and IMS still cannot access or add schedules).
- READS Directory shows name, role and department only. Decide later whether to show more.
- Add more schools and departments to the catalog.
- Sample subjects exist only for Computer Engineering and Electrical Engineering, so a Chairperson in another department fails the "Unit mapping" check until subjects are added.
- Decide whether to enforce one chairperson per department and one dean per school.

## How to apply an update zip
Run from `D:\FMS\FacultyMonitoring`:
```powershell
Expand-Archive -Path "D:\Downloads\<zip name>.zip" -DestinationPath D:\FMS -Force
```
Then check a changed file (for example `Select-String "<new text>" .\Services\<file>.cs`).
Then **uninstall the app from the emulator**, run **Build > Clean Solution**, and press play. The seed data is only written on first launch, so a fresh install is required for data changes.

## Development setup
- Laptop: Ryzen 3 (2018), 8 GB RAM, 120 GB SSD (C:) + 1 TB HDD (D:). Visual Studio is installed on D:.
- Emulator: Pixel 3a, API 30, x86_64, Google APIs, 1536 MB RAM, 2 cores, 3 GB storage, cold boot only.
- Emulator files live in `D:\Android\avd` (`ANDROID_AVD_HOME` is set to this).
- Needs Windows Hypervisor Platform turned on, and virtualization enabled in BIOS.
- Keep C: above 10 GB free. If it fills up, clear `%LOCALAPPDATA%\Temp` and check `D:\Android\avd`.
- A real Android phone over USB (USB debugging on) is a lighter alternative to the emulator.

## Backups
- Back up the folder regularly: copy without `bin`, `obj`, `.vs`, zip it, and keep a copy on USB or cloud.
- Keep milestone zips in `D:\Downloads`.
