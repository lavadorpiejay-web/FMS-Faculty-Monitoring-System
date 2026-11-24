# FMS: Faculty Monitoring System

Project notes. Keep this file in the project folder (`D:\FMS\FacultyMonitoring\NOTES.md`) and update it after each milestone. Attach it, with the project zip, when starting a new chat.

Last updated: 2026-11-19

## What it is
- Android app for monitoring faculty, built with .NET MAUI in Visual Studio 2026.
- Name: **FMS** / **Faculty Monitoring System**. The old names "FAMS" and "Faculty Attendance Monitoring System" are retired. Do not use them.
- Project folder: `D:\FMS\FacultyMonitoring`.
- Data is stored locally as JSON files (users, faculty, subjects, classrooms). There is no server yet.

## Tech
- .NET MAUI, MVVM with CommunityToolkit.Mvvm.
- Local JSON storage through `JsonStorageService`. Passwords are salted hashes (`PasswordHasher`).
- Navigation through `AppShell`, with a navigation guard that blocks any area outside the signed-in role (`RolePolicy.IsRouteAllowed`).

## Registration rules
- ID: exactly 5 characters, no spaces (labelled just "ID").
- Password: at least 8 characters with an uppercase letter, a lowercase letter, a number and a symbol.
- A Chairperson seat (per department) or Dean seat (per school) that is already taken cannot be registered again.
- READS Scholars are students, so their ID is 5 to 10 characters (student ID). Faculty, Chairperson, Dean and IMS Personnel use an employee ID of exactly 5 characters.
- Every account needs an email address (one account per email).

## Verification and approval flow
1. **Register**: the account is created but cannot sign in yet. A 6-digit code is emailed (valid 10 minutes, 5 wrong tries, 30 seconds between resends).
2. **Verify email** screen: enter the code. Sign-in with an unverified email opens this screen again.
3. **Approval** (after the email is verified): Faculty must be approved by the Chairperson of their department (Approvals tab). Chairpersons must be approved by the Dean of their school (Approvals tab). READS Scholars must be approved by IMS Personnel (Approvals tab; any IMS Personnel can approve any READS Scholar). Dean and IMS Personnel need no approval.
   - Until approved, the account cannot sign in and is hidden from directories. A pending Chairperson/Dean still holds the seat. A declined registration frees it and the person may register again.
4. **Forgot password** (Login screen): enter the account email, get a 6-digit code by email, choose a new password (same mixed-character rule).
Codes are stored as hashes only (`verifications.json`).

### Email setup
- Real email needs a mail account. Copy `Resources\Raw\emailsettings.sample.json` to `Resources\Raw\emailsettings.json` and fill it in (Gmail: turn on 2-step verification, then create an "App password" and paste the 16 characters). `emailsettings.json` is ignored by git on purpose because it holds a password, so on each computer you create it again.
- Without that file the app runs in **simulated email** mode: nothing is sent and a pop-up shows the email text (with the code) so the whole flow can still be tested.
- Code: `Services\EmailService.cs` (sending), `VerificationService.cs` (codes, verify, reset), `ApprovalService.cs` (approvals).
## Roles (hierarchy: Dean > Chairperson > Faculty; IMS Personnel > READS Scholar)
| Role | Registers with | Notes |
|---|---|---|
| Faculty | School and department | Reports to Chairperson |
| Chairperson | School and department | One chairperson per department (enforced: registration shows a "role is occupied" warning) |
| Dean | School only (no department) | One dean per school (enforced the same way) |
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
- `rds01` to `rds05`: READS Scholars, all start unassigned (IMS Personnel assigns them on the Assign tab)
- `dean1`: Dean of the School of Engineering and Architecture
- `chr01` to `chr07` and `fac01` to `fac07`: Chairperson and Faculty of each department, in catalog order (01 Electrical, 02 Computer, 03 Civil, 04 Industrial, 05 Mechanical, 06 Architecture, 07 Electronics)
- Dummy accounts are already email-verified and approved. Their emails are fake (`<id>@fms.test`), so Forgot password for them only works in simulated email mode. IDs are exactly 5 characters (READS dummies are 5 too). Because every department and school already has a dummy Chairperson and Dean in debug builds, registering a Chairperson or Dean shows the "role is occupied" warning.

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
10. Milestone 7: occupied-role warning, 5-character ID (READS up to 10), strong passwords, email verification codes, Chairperson/Dean approval of new accounts, Forgot password by email code.

11. Milestone 8: the 50% checklist features: schedules and classrooms (IMS), READS duty and monitoring areas, schedule/room change requests with Chairperson/Dean approval and IMS apply, READS duty dashboard, digital attendance logging, irregularity reports, master schedule and audit trail. See "Schedules, duty and attendance" below.

## Schedules, duty and attendance (Milestone 8)
Who does what (matches the 50% checklist):
- **IMS Personnel**: *Schedules* tab (encode faculty, subject, room, day, time; no room or Faculty clash), *Rooms* tab (campus, owning school, building, floor, room, capacity), *Duty* tab (record each READS Scholar's vacant times, then assign duty day/time inside them plus campus, school, optional department/building/floor; Edit modifies or reassigns), *Changes* tab (approved requests waiting to be applied, with a note about READS duties that may need review), *Master* tab (all schedules with duty assignments, recent attendance, irregularity reports and the change history).
- **Faculty**: *Schedule* tab: own classes, and requests for a schedule change (day, time, subject) or a room change.
- **Chairperson**: *Requests* tab approves/declines schedule changes of the department. **Dean**: *Requests* tab approves/declines room changes for rooms owned by the school.
- **READS Scholar**: *Duty* tab (current, upcoming, completed today) and *Attendance* tab (classes inside the monitoring area during duty hours; **In the Room** / **Not in the Room** buttons open a confirmation card that warns the entry is timestamped and cannot be changed, with a notes box; Not in the Room also files the irregularity report for IMS in the same step; there is no separate report form any more; own recent entries). In the Room is stored as Present, Not in the Room as Absent. Notes are saved on the attendance entry (`AttendanceLog.Notes`) and shown on the Master tab.

Rules enforced in code (`Services\AttendanceService.cs`, `DutyService.cs`, `ChangeRequestService.cs`, `ScheduleService.cs`, `ScheduleRules.cs`):
- A duty must fit inside a recorded vacant time and cannot overlap another duty of the same Scholar.
- A READS Scholar sees and logs only classes today whose room is inside the duty's monitoring area and whose time overlaps the duty window; logging is allowed only while the class is in session and the duty window is active.
- One entry per class per date. An entry for the same Faculty or room in an overlapping time slot is refused as conflicting.
- An entry for a class outside the assignment is refused and automatically saved as a flagged irregularity for IMS.
- Every entry stores the timestamp, Faculty, subject, campus, school/department, building, room, scheduled time, monitoring date and checker.
- IMS changes, requests, approvals and applied changes are written to `audit.json` (shown on the Master tab).
- Times are text "HH:mm" in 30-minute steps (06:00 to 22:00). Campuses are listed in `AcademicCatalog.Campuses`; edit that list to match the university.
- Debug builds seed sample classes (Mondays), vacant times and two duties (rds01, rds02), and the Attendance tab has a **test clock** so logging can be tried at any hour (for example Monday 08:30). Seeded data needs a fresh install of the app.
- New data files: `schedules.json`, `duties.json`, `vacancies.json`, `requests.json`, `attendance.json`, `irregularities.json`, `audit.json`. Classrooms now also have a campus, owning school and floor.

Quick test path (debug build, password in DUMMY_ACCOUNTS.txt):
1. `ims01`: Schedules, Duty and Master show the seeded data. Duty: add a vacant time, assign a duty, edit it.
2. `fac02`: Schedule tab, request a room change. `dean1`: Requests tab, approve it. `ims01`: Changes tab, apply it.
3. `fac02`: request a time change. `chr02`: Requests tab, approve. `ims01`: Changes tab, apply.
4. `rds02`: Attendance tab, turn on the test clock (Monday 08:30), tap In the Room on the Computer Engineering class (read the warning, add a note, submit), try again (duplicate refused), then use Not in the Room on another class to file an irregularity.
5. `rds01`: same day, log a class; `ims01`: Master tab shows the entries.

## Open items / next
- Not compiled or run by the assistant that wrote Milestone 8: build it in Visual Studio and report any error text.
- IMS has 10 tabs; Android shows the first four and a "More" tab. Consider merging Rooms into Schedules and moving System checks into Profile.
- No push notification yet: IMS sees approved changes on the Changes tab and Chairperson/Dean see requests on the Requests tab.
- READS Scholars' vacant times are entered by IMS (READS cannot enter their own yet).
- Class schedules repeat weekly; there is no semester calendar, holiday or one-off date handling.
- READS Directory shows name, role and department only. Decide later whether to show more.
- Add more schools and departments to the catalog. Sample subjects exist for one subject per department.
- Decide whether to enforce one chairperson per department and one dean per school (registration already warns).
- A server and shared database (the data is local JSON on each phone today).

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
