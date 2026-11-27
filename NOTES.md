# FMS: Faculty Monitoring System

Project notes. Keep this file in the project folder (`D:\FMS\FacultyMonitoring\NOTES.md`) and update it after each milestone. Attach it, with the project zip, when starting a new chat.

Last updated: 2026-11-27 (floor ownership, CRE, READS on Schedules, Work Time)

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
3. **Approval** (after the email is verified): Faculty must be approved by the Chairperson of their department (Home tab). Chairpersons must be approved by the Dean of their school (Home tab). READS Scholars must be approved by IMS Personnel (Home tab; any IMS Personnel can approve any READS Scholar). Dean and IMS Personnel need no approval.
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

## System checks (removed)
The "System checks" tab was only needed for the 25% progress checklist. It was removed from every role in Milestone 11. The files (`StatusPage`, `StatusViewModel`, `ReadinessService`) are still in the project but nothing links to them; delete them later if you want.

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

12. Milestone 11: removed the System checks tab for all roles.
13. Milestone 12: the Home page of Chairperson, Dean and IMS Personnel is now one combined approvals page (`Views\ApprovalsHomePage.xaml`, `ViewModels\ApprovalsHomeViewModel.cs`). Chairperson: Faculty accounts + schedule change requests. Dean: Chairperson accounts + classroom change requests. IMS Personnel: READS Scholar accounts + approved changes to apply to the master schedule. The separate Approvals, Requests and Changes tabs are gone. The Dean has a Departments tab again. Faculty and READS Scholar Home are unchanged (READS keeps its attendance confirmation prompt and duplicate/conflict checks on the Attendance tab). The old `ApprovalsPage`, `RequestsPage` and `ChangesPage` files are still in the project but no tab links to them.
14. Milestone 13: READS monitoring is based on the classroom schedule, not on the faculty. The Duty tab no longer asks for a school or department; the area is campus + optional building + optional floor, checked against the real rooms. The READS Attendance list now shows the room and its scheduled class first, with the scheduled faculty as a detail. Seeded duties changed (rds01 = Main building, rds02 = Engineering building floor 3), so a fresh install is needed.

15. Milestone 14 (partial): the Schedules tab picks the day with a calendar (`DatePicker`); schedules still repeat weekly, so the date only decides the weekday. Times are still stored as 24-hour "HH:mm" but are shown in 12-hour form ("8:00 AM") through `Services\TimeFormat.cs` (`To12h`, `To24h`, `Range`). The Schedules tab also picks the Faculty member school first, then department, then person. Done only on the Schedules tab and the schedule conflict messages; the other screens still show 24-hour times until they use `TimeFormat`.

## Collapsible lists and room filter (latest change)
- **Faculty Schedule tab**: "Request a change" is a toggle (tap the heading). The form is hidden until tapped and folds away again after a request is sent; the success or error message stays visible under the heading.
- **Room change picker** only lists rooms of the building(s) the class's school is situated in. `CampusLayout.SchoolBuildings` maps a school to its buildings (School of Engineering and Architecture = SA); a school not in the map is not limited yet. The class's current room is left out. `ChangeRequestService.SubmitAsync` refuses a room outside the school's buildings as well. To limit another school, add it to `SchoolBuildings`.
- **Dean, Departments tab**: each department is a toggle (`DepartmentSection` in `Models\Groups.cs`); tapping it shows its Chairperson(s) and Faculty. Open departments stay open after a reload.
- **READS Scholar, Directory tab**: the Dean card is unchanged. A "Departments" toggle lists every department of the assigned school with its Chairperson(s); tapping a Chairperson shows that department's Faculty (`DirectoryDepartment`, `ChairpersonNode`). A department with no Chairperson shows its Faculty directly. `DirectoryResult` now also carries `Departments`.
- Not compiled by the assistant (no .NET SDK there): build in Visual Studio and report any error text.

## Weekly timetable view (latest change)
- `Controls\WeeklyTimetable.cs`: a Monday-to-Sunday timetable (hours down, coloured block per class or vacant time, swipe sideways on a phone). Data is a list of `TimetableBlock` (`Models\Scheduling.cs`). It replaced the plain tables here: **READS Duty tab** (my vacant times; tap a block to remove it), **Faculty Schedule tab** (my classes), **IMS Duty tab** (each READS Scholar's vacant times, `Compact="True"`). Use the same control for any future schedule table.
- Not changed: the IMS Rooms table (rooms against days, not one person's week), IMS Schedules and Master lists (many faculty at once, with Remove and filters).
- **Assignment notice:** when IMS assigns or clears a READS Scholar's school (`ScholarAssignmentService`), the Scholar gets a bell notification (and the phone alert when the app is next opened). The assignment itself still takes effect immediately.

## Phone alerts without a server (latest change)
- Package `Plugin.LocalNotification` (`FacultyMonitoring.csproj`, `.UseLocalNotification()` in `MauiProgram`), permissions in `Platforms\Android\AndroidManifest.xml`. Code: `Services\LocalAlertService.cs`.
- **Reminders (repeat every week, 10 minutes before):** READS Scholar "Duty starts in 10 minutes" from their own vacant times; Faculty "Class starts in 10 minutes" from their schedule. Other roles get none.
- **New-notification alert:** when the app is opened or resumed, or someone signs in, one phone alert if there are unread bell notifications not alerted yet.
- `LocalAlertService.SyncAsync` runs after sign-in (`AppShell.EnterRoleAsync`), on app resume (`App.CreateWindow`), and after a READS Scholar adds or removes a vacant time. Sign-out cancels the reminders. Permission is asked once.
- Limits: it only reaches the phone the app is on (no server), reminders refresh when the app is opened, and Android may fire them a few minutes late unless "Alarms & reminders" is allowed for the app. The emulator needs a fresh install to ask for notification permission.
- Not compiled by the assistant: if NuGet cannot restore `Plugin.LocalNotification` 12.*, install the newest version in Visual Studio (Manage NuGet Packages). If `LocalNotificationCenter` is not found, the package uses the older name `NotificationCenter`.

## Notification bell (latest change)
- Every signed-in page has a bell with a red unread count in the top bar (`Controls\NotificationBell.cs`, attached to each page by `AppShell.AttachBell`). Tapping it opens the Notifications page (`Views\NotificationsPage.xaml`, route `Notifications`, allowed for every signed-in role in `RolePolicy`). Tap a notice to mark it read, or use "Mark all as read".
- `NotificationService` / `AppNotification` (`notifications.json`): a notice is addressed to one person or to a role (optionally a school and department). Role notices are a shared inbox: IMS Personnel, the Chairperson of the department, the Dean of the school.
- Who gets what: Chairperson (schedule change) or Dean (room change) get "new request"; IMS Personnel get "approved" (with the checkers to adjust); the Faculty member gets approved / declined by Chairperson or Dean, applied, or declined by IMS.
- The old "New approvals" card on the IMS Home tab was removed; the bell replaces it. The approved-changes list stays on Home.

## Approval notification to IMS
- When a Chairperson (schedule change) or Dean (room change) approves a request, `ChangeRequestService.DecideAsync` writes a notification for IMS Personnel (`NotificationService`, `notifications.json`, model `ImsNotification`). It appears as a highlighted "New approvals" card at the top of the IMS Home tab, above the approved-changes list, until IMS applies or declines that request or taps "Mark all as read".
- The notice names the request, who approved it, and the **checkers to adjust**. Checkers are READS Scholars assigned to the class's school; one is touched when one of their vacant times overlaps the class's old or new day and time (`ChangeRequestService.CheckerImpactAsync`). A schedule change lists the checkers who covered the old time and those free at the new time, and warns when nobody is free. A room change lists the checkers monitoring that class so they can be told the new room.
- The message after IMS applies a change uses the same check (the old duty-based check was removed, because duties are no longer used).
- The Faculty "My requests" list shows "approved, IMS Personnel notified and will apply it".
- Room changes: a Dean only sees room requests of Faculty of their own school, and the new room is limited to the school's building (see "Collapsible lists and room filter").

## Schedules, duty and attendance (Milestone 8)
Who does what (matches the 50% checklist):
- **IMS Personnel**: *Schedules* tab (encode faculty, subject, room, day, time; no room or Faculty clash), *Rooms* tab (campus, owning school, building, floor, room, capacity), *Duty* tab (record each READS Scholar's vacant times, then assign duty day/time inside them plus the monitoring area: campus and optional building/floor; Edit modifies or reassigns), *Home* tab (approved requests waiting to be applied, with a note about READS duties that may need review, plus READS Scholar account approvals), *Master* tab (all schedules with duty assignments, recent attendance, irregularity reports and the change history).
- **Faculty**: *Schedule* tab: own classes, and requests for a schedule change (day, time, subject) or a room change.
- **Chairperson**: *Home* tab approves/declines schedule changes of the department. **Dean**: *Home* tab approves/declines room changes for rooms owned by the school.
- **READS Scholar**: *Duty* tab (current, upcoming, completed today) and *Attendance* tab (classes inside the monitoring area during duty hours, In the Room / Not in the Room buttons open a confirmation card (timestamped, cannot be changed) with a notes box; Not in the Room also files the irregularity report for IMS, there is no separate report form; own recent entries).

Rules enforced in code (`Services\AttendanceService.cs`, `DutyService.cs`, `ChangeRequestService.cs`, `ScheduleService.cs`, `ScheduleRules.cs`):
- A duty must fit inside a recorded vacant time and cannot overlap another duty of the same Scholar.
- The monitoring area is a place (campus, then building and floor if set). A READS Scholar sees and logs only the classes scheduled today in rooms inside that area whose time overlaps the duty window, whoever teaches them: the faculty's school or department is not used. IMS Personnel cannot save an area whose building or floor does not exist in the Rooms list; logging is allowed only while the class is in session and the duty window is active.
- One entry per class per date. An entry for the same Faculty or room in an overlapping time slot is refused as conflicting.
- An entry for a class outside the assignment is refused and automatically saved as a flagged irregularity for IMS.
- Every entry stores the timestamp, Faculty, subject, campus, school/department, building, room, scheduled time, monitoring date and checker.
- IMS changes, requests, approvals and applied changes are written to `audit.json` (shown on the Master tab).
- Times are text "HH:mm" in 30-minute steps (06:00 to 22:00). Campuses are listed in `AcademicCatalog.Campuses`; edit that list to match the university.
- Debug builds seed sample classes (Mondays), vacant times and two duties (rds01, rds02), and the Attendance tab has a **test clock** so logging can be tried at any hour (for example Monday 08:30). Seeded data needs a fresh install of the app.
- New data files: `schedules.json`, `duties.json`, `vacancies.json`, `requests.json`, `attendance.json`, `irregularities.json`, `audit.json`. Classrooms now also have a campus, owning school and floor.

Quick test path (debug build, password in DUMMY_ACCOUNTS.txt):
1. `ims01`: Schedules, Duty and Master show the seeded data. Duty: add a vacant time, assign a duty, edit it.
2. `fac02`: Schedule tab, request a room change. `dean1`: Home tab, approve it. `ims01`: Home tab, apply it.
3. `fac02`: request a time change. `chr02`: Home tab, approve. `ims01`: Home tab, apply.
4. `rds02`: Attendance tab, turn on the test clock (Monday 08:30), log the Computer Engineering class with In the Room (read the warning, add a note, submit), try again (duplicate refused), use Not in the Room on another class to file an irregularity.
5. `rds01`: same day, log a class; `ims01`: Master tab shows the entries.

## Open items / next
- Not compiled or run by the assistant that wrote Milestone 8: build it in Visual Studio and report any error text.
- IMS now has 8 tabs (Home, Schedules, Rooms, Duty, Master, Assign, Profile, plus More on Android). Consider merging Rooms into Schedules.
- No phone push notification yet (the notice is in-app, on the IMS Home tab; see "Approval notification to IMS").
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


## Lists (latest change)
- Classrooms no longer have a capacity (only faculty are checked, not students). Old saved rooms still load; the extra field is ignored.
- IMS Rooms tab lists classrooms in collapsible sections, one per building (`RowGroup` in `Models\Groups.cs`).
- IMS Schedules tab lists encoded schedules by school, then department, each collapsible (`SchoolGroup` and `RowGroup`).

## Main Campus buildings and rooms (latest change)
- Main Campus has four buildings, coded **MC** (Mount Carmel), **SN** (San Nicolas), **SA** (San Agustin), **SJ** (San Jose). A building is a place where schools are situated; no school owns a building. Defined in `Services\CampusLayout.cs`; the building code is stored in `Classroom.Building`.
- Floors used: MC floor 4; SN floors 2-3; SA B-1 (upper basement), B-2 (lower basement) and 2; SJ floors 3-5. Assumed 10 classrooms per floor (`CampusLayout.RoomsPerFloor`). Room names are `<floor>-<two digits>`, e.g. `B-1-01`, `2-01`, `4-01`.
- Rooms are fixed and created from `CampusLayout` on first launch (90 rooms). Nothing adds a classroom by typing any more.
- Rooms and floors belong to no school (schools share buildings and floors). The old floor "assigned school" was removed; see "Rooms tab and room-change approval" below.
- **Rooms tab (IMS)**: see "Rooms tab and room-change approval" below.
- **Schedules tab (IMS)**: classroom chosen as building, floor, room. One subject can have several days (tick Mon..Sun). One shared start/end time, or switch off "Same time for every day" to set each day's time. Each day is saved as its own schedule row (all days saved together or none). Removing removes one day at a time.
- Seed data: Engineering sample classes use SA basement rooms, other schools' sample classes use SA floor 2; duties still use building SA (rds01 whole building, rds02 floor B-1). Needs a fresh install.

## READS Scholars: school assignment, vacant times, two-check attendance (Milestone 15 part 2)
- **Registration**: a READS Scholar picks the school they study in (`UserAccount.OwnSchool`). Dummy accounts: rds01-rds03 study in the School of Business and Management and monitor the School of Engineering and Architecture; rds04-rds05 study in the School of Engineering and Architecture and monitor the School of Arts and Sciences.
- **Assign tab (IMS)**: a Scholar is assigned to a school only (all of its departments). The Scholar's own school is not offered and is refused by `ScholarAssignmentService`. `AssignedDepartment` is no longer used.
- **Duty tab (READS Scholar, `MyDutyPage`)**: the Scholar enters their own vacant times: tick several days, one start and end, then a table with Remove. The vacant times are the duty hours (`VacantTimeService`). **Duty tab (IMS, `DutyPage`)** is read-only: each Scholar, the school they monitor and their vacant times (`DutyService.GetOverviewAsync`). IMS no longer records vacant times, duty areas or duties (the duties file and `DutyAssignment` stay only so old data loads).
- **Home (READS Scholar, `ReadsHomePage`)**: today's duty hours (Current / Upcoming / Completed) and the checks completed today.
- **Directory (READS Scholar)**: the Dean, Chairpersons and Faculty of the assigned school (all departments).
- **Attendance tab (READS Scholar)**: nothing to enter. It lists today's classes of the assigned school that overlap the Scholar's vacant times (`AttendanceService.GetMonitorRowsAsync`). Two checks per class:
  1. First check opens 5 minutes after the start: "In the room" or "Not in the room" (+ optional notes).
  2. End check opens 5 minutes before the end: same choices.
  - In the room at the first check: **Present** (if not in the room at the end check it is still Present, with the observation "left early").
  - Not in the room at the first check, in the room at the end check: **Late**.
  - Not in the room at the end check (after not being there at the start): **Absent**, wherever the faculty is (office included).
  - The log (`AttendanceLog`) keeps both check times, the observation and the notes. Status is provisional after the first check and final after the end check. A class whose first check was missed cannot be logged any more. A check also needs the Scholar to be inside one of their vacant times.
  - A class outside the assigned school or vacant times is refused and flagged as an irregularity for IMS.
- Debug builds still have the test clock on the Attendance tab. Its time picker (READS Scholar only; `AttendanceLogViewModel.Times`) lists the check moments, :05, :25, :35 and :55 of every hour, in 12-hour form. For a class at 8:00-9:30 AM use 8:05 AM for the first check and 9:25 AM for the end check; for 9:30-11:00 AM use 9:35 AM and 10:55 AM. Schedules and every other screen keep their 30-minute times.
- Old data: `UserAccount` has the new `OwnSchool` field (empty on old accounts). Seed data changed, so a fresh install is needed.

## Rooms tab and room-change approval (IMS Personnel)
- **Rooms tab** (`RoomsViewModel`, `RoomsPage.xaml`): pick a building, then a floor, then optionally one room ("All rooms" by default), then the days and the time the same way as the Schedules tab: tick any days (Mon to Sun), one start and end time, or switch off "Same time for every day" to set each ticked day's own time. The default is today ticked, 8:00 to 9:00 AM.
- A "Show vacant rooms" switch (on by default) hides the vacant rows so only the classes are listed.
- The table appears only after a floor is chosen, and then lists just that floor (or the one chosen room).
- The result is a table (swipe sideways on a phone) with the columns Room, Day, Time, Availability, Faculty, Subject and School / Department. Every room of the chosen floor (or the chosen room) appears once per ticked day. A vacant room shows the chosen time with Vacant in green. An occupied room shows the class's time, Occupied in red, the Faculty, the subject (code and title) and the school and department using it; a room with two classes in the window gets two rows. The table shows at most 400 rows (choose one room or fewer days to narrow it).
- The **assigned school** of a floor (picker and Save button) was removed, along with `ScheduleService.SetFloorSchoolAsync`. Seeded rooms no longer carry a school (`Classroom.School` stays in the model only so old data loads).
- **Room change**: Faculty request -> the Dean of the requesting Faculty's school approves or declines (Home tab) -> IMS Personnel check the availability and give the final approval, or decline (Home tab). Schedule changes follow the same last step: Chairperson, then IMS Personnel.
- IMS Home, "Approved changes": each request shows an availability line ("the room is vacant at that time" or "NOT available" with the clash), a **Approve and apply** button and a **Decline** button (`ChangeRequestService.GetAvailabilityNoteAsync`, `DeclineAsync`, `ApplyAsync`). Approving applies it to the master schedule; if the room has been taken meanwhile, applying is refused with the clash message.
- The Faculty Schedule tab now shows the stage of each request: waiting for the Dean (or Chairperson), approved and waiting for IMS Personnel, applied, or declined (with who decided).
- Seed data changed (rooms no longer carry a school), so a fresh install is needed.
- After IMS applies a change, the message names every checker (READS Scholar) whose duty covered the class before or covers it now, with the day, time and area, so IMS can adjust those duties on the Duty tab. Duties are adjusted by IMS by hand; nothing is moved automatically.
- **Final say of IMS Personnel**: a room change goes Faculty -> Dean of the Faculty's school -> IMS Personnel (Home tab shows who approved it and the availability check). **Approve** sets the requested room in the master schedule automatically (if the room was taken meanwhile, it is refused with the clash). **Decline** asks for a reason (`DeclineAsync(requestId, reason)`, stored in `ChangeRequest.DeclineReason`), changes nothing in the schedule, and the Faculty member sees "Declined because: ..." in their request list.

## Update 2026-11-22 (rooms layout, Duty toggles, room names, READS notices, READS Home table)
- **IMS Rooms tab**: the Day and time card (days, same-time switch, start/end) is now first. Below it, the Room availability card holds Building, Floor, Room and the "Show vacant rooms" switch, directly above the Rooms table, so the list is easier to reach (`Views\RoomsPage.xaml` only; the view model is unchanged).
- **IMS Duty tab**: each READS Scholar is a toggle (tap the name to show or hide their vacant timetable). `ScholarDutyRow` (in `Services\DutyService.cs`) is now an observable class with `IsExpanded` / `ToggleCommand`; open Scholars stay open after a reload (`DutyViewModel.LoadAsync`).
- **My classes (Faculty) timetable**: in the full-size `WeeklyTimetable` the room is now the second line of each block (right under the subject, bold, prefixed "Room"), the time is the last line, day columns are wider (118) and hour rows taller (72). The room used to be the last line and was cut off when the subject or time wrapped. The compact version (IMS Duty) is unchanged.
- **READS notification after a room or schedule change**: when IMS Personnel apply (give final approval to) a change, `ChangeRequestService.NotifyCheckersAsync` writes a bell notice (`NotificationService.ForChecker`) to every READS Scholar assigned to the class's school whose vacant time overlaps the class's old or new day and time. A room change says "moved from X to Y"; a schedule change says what it was and what it is now. The Dean/Chairperson approval alone does not notify READS (the change is not final until IMS applies it). The IMS message after applying says how many checkers were notified. The phone alert is the existing "new bell notice" alert when the READS Scholar next opens the app.
- **READS Home**: new "My assigned classes today" table (Room, Time, Subject, Faculty, Status, Check) with the In the room / Not in the room buttons for whichever check is open (first check or end check), same confirmation and rules as the Attendance tab (`ReadsHomeViewModel` is now a `PageViewModelBase`; `MonitorRow` has extra `Room`, `Time`, `Subject`, `Faculty` fields filled by `AttendanceService.GetMonitorRowsAsync`). The Attendance tab is unchanged.
- Not compiled by the assistant (no .NET SDK there): build in Visual Studio and report any error text. No seed data changed, so a fresh install is not needed.

## Update 2026-11-22 part 2 (removal choices, Attendance table, Directory person pages)
- **Removal asks what to delete** (`ViewModels\RemovePrompt.cs`, `Views\PickItemsPage.cs`). Tapping a vacant-time block (READS Duty tab) or Remove on a schedule (IMS Schedules tab) opens a list of scopes: only that one, everything on that day, everything at that same time, (IMS: everything of the same subject, all classes of that Faculty member), or "Choose which ones to delete..." (a tick list). A scope that would match only one row is left out. A count confirmation follows. New service methods: `VacantTimeService.RemoveManyAsync`, `ScheduleService.RemoveManyAsync` (all or nothing, one audit entry). IMS scopes stay inside one Faculty member's classes so one tap cannot wipe other people's schedules.
- **READS Attendance tab**: "Classes to check today" is now a table (Room, Time, Subject, Faculty, Status, Check) with a "Finished x of y" line. A class stays in the table all day; when both checks are done (or the class is over after the first check) the Check column shows "Finished" with Present / Late / Absent and the notes show under Status. The READS Home table shows Finished the same way. `MonitorRow` got `IsFinished`, `Result`, `Notes`, `LogId`, `FacultyUserId`.
- **READS Directory**: every Dean, Chairperson and Faculty name opens a page (`Views\PersonDetailPage.xaml`, `ViewModels\PersonDetailViewModel.cs`, route `ReadsScholarPerson`). It shows ID, email, school, department, then: Faculty = subjects with every day, time and room, today's classes to check (In the room / Not in the room, same rules as Attendance) and "My notes on this faculty member"; Chairperson = department subjects and Faculty; Dean = the school's Chairpersons.
- **Notes and status from the Directory**: "Change status or note" / "Change" lets the checker edit the note and set Present (bathroom break / in a meeting with the Chairperson / other reason), Late or Absent for the entry. `AttendanceLog.StatusAdjusted` marks a hand-set status; the end check keeps it. `AttendanceService.UpdateLogAsync` (only the checker who made the entry; audit trail entry). IMS Master shows the adjusted status and notes like any entry.
- `DirectoryEntry` now carries UserId, Role, SisId, Email, School, Department. `DirectoryService.LoadPersonAsync` re-checks that the person belongs to the Scholar's assigned school.
- Not compiled by the assistant (no .NET SDK there): build in Visual Studio and report any error text. No seed data changed, so a fresh install is not needed (old attendance entries load fine; the new field defaults to false).

## Update 2026-11-22 part 3 (Chairperson and Dean teach too)
- Chairpersons and Deans can be encoded as the teacher on the IMS Schedules tab (a Dean is listed under every department of the school; the department chosen decides the subject list). `ScheduleService.CanTeach`, `SubjectFits`, `GetMineAsync`.
- **Chairperson**: Faculty tab lists every Faculty member with the subjects they handle and their days, times and rooms (`FacultyWithSubjects`). New tab **My schedule** (`Views\MySchedulePage.xaml`, `ViewModels\MyScheduleViewModel.cs`, route `ChairpersonSchedule`).
- **Dean**: new tab **My subjects** (same page, route `DeanSubjects`), subjects grouped with their department shown. Subjects exist only per department in the catalog; there is no separate General Education list yet.
- READS Directory pages of a Chairperson or Dean show their classes, today's checks and notes (see part 2).
- Not compiled by the assistant: build in Visual Studio and report any error text.

## Update 2026-11-22 part 3 (IMS assigns classes and floors, READS Attendance cards)
- **IMS Assign tab** has a new card "Classes and floor to check". Pick the READS Scholar (the school assignment stays as before), then a building and a floor. The list shows the classes of the Scholar's assigned school on that floor that lie **completely inside one of the Scholar's vacant times** (the Scholar's vacant times are shown above the pickers). Tick the classes and press "Save classes for this floor" ("Tick all" / "Untick all" help). Saving with nothing ticked takes that floor off the Scholar.
  - Rules (`ScholarAssignmentService.GetClassesAsync` / `SaveClassesAsync`): one class is checked by one Scholar only (a class another Scholar has is listed but cannot be ticked); two classes at the same time cannot go to one Scholar; a class that is no longer inside the vacant times stays in the list (marked) so IMS can untick it. The Scholar gets a bell notice listing the classes. Changing or removing the Scholar's school drops their class assignments.
  - The "Current assignments" list shows how many classes each Scholar checks and on which floors.
- **New data**: `CheckAssignment` (`Models\Scheduling.cs`), file `checkassignments.json` (`JsonStorageService.CheckAssignmentsFile`). The file is created automatically on the next launch, so no fresh install is needed. In debug builds it starts with sample assignments for rds01 and rds02 (Engineering classes, shared between them) and rds04 (Arts and Sciences). Any other Scholar sees no classes until IMS assigns them.
- **READS Attendance tab** is no longer a table. Each assigned class today is a card: floor, room, time, subject, the person who handles it (Faculty, Chairperson or Dean) and two big buttons, **In the Room** and **Not in the Room**, shown whenever a check is open (first check 5 minutes after the start, end check 5 minutes before the end). Same confirmation and rules as before. Cards that can be checked now come first, then upcoming, then finished ones. The header shows "Your floors: ...".
- **Scope change** (`AttendanceService.InScope`): a Scholar now checks only classes IMS assigned to them (and that overlap their vacant times today). Logging any other class is refused and flagged for IMS. The READS Home table and the Directory person pages use the same list, so they also show only the assigned classes. The READS Home table is unchanged in layout.
- **Change requests** (`ChangeRequestService`): when IMS applies a schedule or room change, the checkers notified are the Scholars assigned to that class; the IMS message names them and says whether the new time still lies inside their vacant times. A room change that moves a class to another floor updates the assigned floor.
- Not compiled by the assistant (no .NET SDK there): build in Visual Studio and report any error text.

## Update 2026-11-22 part 4 (shift handover: first check and end check can go to different Scholars)
- A READS Scholar's vacant time rarely lines up with the class times. A class that ends after the Scholar's vacant time is split: this Scholar does the **first check** (5 minutes after the start) and another Scholar, whose vacant time starts there, takes over and does the **end check** (5 minutes before the end).
- **IMS Assign tab**: each class now has two tick boxes, "First check" and "End check". A box is enabled only when that check's moment lies inside the Scholar's vacant time and nobody else holds that check. A class is listed when at least one of its checks fits. Notes under a class say who holds the other check or that a check is outside the vacant time. "Tick all" ticks every enabled box. One Scholar cannot hold two checks less than 5 minutes apart on the same day (this floor and their other floors are both counted).
- **Data**: `CheckAssignment.Part` is `Both`, `Start` or `End` (`CheckPart`); old/seeded rows default to `Both`. `AttendanceLog.EndCheckerUserId` / `EndCheckerName` are filled when the end check is done by a different Scholar than the first check.
- **READS Attendance / Home / Directory**: a card shows "Your part: first check only" or "end check only" when the class is split. The first-check Scholar sees the class as Finished ("your first check is done") once they submit; the end-check Scholar sees "Another Scholar does the first check. Your end check opens at ..." and gets the buttons when the end check opens (they can only do it if the first check exists). If the first check was never done, the class cannot be logged any more, as before. Either Scholar can open the entry in the Directory to add notes or change the status. IMS Master shows both checkers' names.
- Scope no longer requires the class to overlap the vacant time at the assignment level: the check is allowed only while the Scholar is inside a vacant time (unchanged), and IMS only offers checks that fit.
- Not compiled by the assistant (no .NET SDK there): build in Visual Studio and report any error text.

## Update 2026-11-22 part 5 (one Scholar, one floor, classes that all end together)
- Problem: a Scholar on one floor (for example SA B-1) with 10 rooms that all run 8:00 to 9:30 cannot do ten end checks in the 5 minutes before 9:30.
- The **end check is now a window**: it opens **15 minutes before the class ends** (`AttendanceService.EndWindowMinutes = 15`) and closes when the class ends, so the Scholar can walk the floor. The first check is unchanged (opens 5 minutes after the start and stays open until the end window opens). The test clock list now includes :15 and :45.
- IMS can give one Scholar **many classes of the same floor at the same time**. The only limit left: end checks on two **different floors** that open less than 15 minutes apart cannot go to one Scholar (`SaveClassesAsync`).
- The Scholar's vacant time must cover the start of the end window for IMS to offer the end check (`ScholarAssignmentService.CheckMoment`).
- Not compiled by the assistant: build in Visual Studio and report any error text.

## Update 2026-11-26 (READS wording, Assign tab, one-line schedules)
- **Wording**: the app says **READS** everywhere on screen (not "READS Scholar" / "Scholars"), because a READS is already a scholar. Code names such as `UserRole.ReadsScholar` and `ScholarAssignmentService` are unchanged.
- **IMS Assign tab**: when a READS already has an assigned school, the school picker is hidden and IMS only chooses a **building and a floor** (a "Change school" button opens the picker again; its buttons are now **Save school** / **Remove school**). The **Classes and floor to check** section now always shows **Save assignment** and **Remove assignment** once a READS with a school, a building and a floor are chosen. Remove assignment takes every class of that floor off the READS' list, even when no class is listed.
- **SeedData schedules**: `MySchedules` is one line per subject, `(FacultyId, SubjectCode, Days, Start, End, Building, Floor, Room)`, with day letters `M T W Th F Sa Su` (`"MWF"`, `"TTh"`, `"Sa"`). Days that need a different time or room go on their own line for the same person and subject, for example `"MW" 08:30-09:30` and `"F" 10:30-11:30`. Nothing is replaced, so write each day once (a day written twice is reported in the Output window).
- **SeedData vacant times**: `MyVacantTimes` uses the same day letters, `(ReadsId, Days, Start, End)`, one line per vacant time. Days with a different time go on their own line (`"MW" 07:30-09:30`, `"F" 10:30-12:00`). The list is empty by default (READS can also enter their own on the Duty tab).
- **Seed schedules**: Alexis Dacanay (30001), Jeren Yacanad (30002), Robel Branianola (30003), Chairperson Sean Pareja (20001) and Dean Dennis Trillo (10001) each have 4 to 5 subjects in the San Agustin building (51 class meetings in all).
- Not compiled by the assistant (no .NET SDK there): build in Visual Studio and report any error text. Uninstall the app or clear its storage first so the seed data is written again.
- **IMS Schedules page, Encoded schedules**: a class that meets at the same time in the same room shows as one row with its days (`AC 2A1 ... MWF 11:00 AM - 12:00 PM`). A day with a different time or room gets its own row. Remove offers "This schedule" (all its days), "Only <day>", then the existing subject / time / every-class options.

## Update 2026-11-27 (resignations)
- **Profile page, Resignation card** (Faculty, Chairperson and Dean only): the person states a reason and submits it. They see where it is (`Chairperson: ✓ Forwarded by ...`, `Dean: ... waiting for a decision`, `IMS Personnel: next`) and can **Withdraw** it until it is official. If it was declined, the reason and who declined it are shown and they may submit again.
- **Route**: Faculty -> their department's Chairperson -> the school's Dean -> IMS Personnel. Chairperson -> Dean -> IMS Personnel. Dean -> IMS Personnel. A step is skipped when nobody holds that post (no Chairperson in the department, no Dean in the school).
- **Home page** of the Chairperson, Dean and IMS Personnel has a **Resignations** card: **Forward to Dean** / **Forward to IMS** / **Make official**, and **Decline** (a reason is required, and the person sees it). IMS Personnel also see how many classes the person still handles.
- **Make official** turns the account **inactive** (cannot sign in, leaves the directories and the schedule pickers) and sends the person an email (best effort). Their classes are not deleted: reassign or remove them on the Schedules tab. IMS Personnel are told if the department has no Chairperson or the school has no Dean afterwards.
- Every step writes a bell notice (next person, and the person resigning) and an audit entry. Data: `resignations.json` (`ResignationService`, `Models/Resignation.cs`).
- **READS can resign too** (Profile page, same card): the resignation goes straight to IMS Personnel. When IMS make it official the account becomes inactive and the class checks assigned to that READS are removed (IMS are told how many, to assign them to another READS on the Assign tab).

## Update 2026-11-27 (floor ownership, CRE, READS on the Schedules page, "Work Time")
- **Floor ownership** (`Services\CampusLayout.cs`, second argument of each `FloorPlan`): **SEA** owns SA basements B-1 and B-2 and the SA 2nd floor. **SBM** owns the SN 2nd floor and the SJ 3rd floor. All other floors (MC 4, SN 3, SJ 4 and 5) are unowned: anyone may use them with no permission. To change who owns a floor, edit that table.
- **Permission rule**: a class may use a room on a floor owned by **another** school only with the **Dean of the owning school** saying yes. Own floors and unowned floors need no extra permission. This replaces the old rule that limited SEA to building SA (`CampusLayout.IsRoomAllowed` / `SchoolBuildings` are gone): SEA may now ask for SBM floors and the other way round.
- **Faculty room change** (Schedule tab): rooms on another school's floor are marked "(owned by SBM, needs the Dean's permission)" in the picker. For those the request goes to the Dean of the **owning** school (not the Faculty's own Dean), then IMS Personnel apply it as before. If the owning school has no Dean in the app, the request is refused with a message. Room requests for a floor of the Faculty's own school or an unowned floor still go to the Faculty's own Dean.
- **IMS Schedules page**: encoding a class in a room owned by another school does **not** save it. It creates a `ChangeKind.RoomUse` request (`ChangeRequest.RoomUsePlan` holds the classes as JSON) for the owner's Dean. The Dean sees it in **Classroom requests** on Home. **Approve** saves the classes at once (clashes are re-checked) and gives the chosen READS; **Decline** saves nothing. IMS Personnel get a bell notice either way. Under the room picker a note says who owns the chosen floor.
- **CRE (Center for Religious Education)**: an independent department, under **no school**. `AcademicCatalog.CreName`, `IndependentUnits`, `UnitNames` (schools + CRE), `IsValidUnit`, `IsIndependent`. Its accounts store CRE's name in **both** `School` and `Department`, so all school/department matching keeps working (`AcademicCatalog.UnitLine` avoids showing the name twice). No Dean, no rooms owned. Faculty and Chairperson can pick CRE at registration (Dean and READS "school you study in" cannot). The **CRE Chairperson is approved by IMS Personnel** (`ApprovalService`, `LocalAuthService.ApproverOf(role, school)`). A CRE Faculty room change is approved by the CRE Chairperson (or the owner's Dean for another school's floor). IMS can assign a READS to CRE like a school. The ReEd 1 to 4 subjects in `SeedData` now belong to CRE.
- **READS on the Schedules page**: below the room and times, two optional pickers, "READS for the first check" and "READS for the end check". They list only READS assigned to the class's school (or CRE) whose **Work Time covers that check on every class day** (first check = 5 minutes after the start, end check = last 15 minutes). The same READS for both = the whole class; two READS = a hand-over. The lists refresh when the Faculty member, days or times change. `ScholarAssignmentService.GetOptionsAsync` and `AssignChecksAsync` (checked before anything is saved; one READS cannot do end checks on two floors that open less than 15 minutes apart). The Assign tab still works for changes later.
- **"Vacant Time" is now "Work Time"** everywhere the READS or IMS see it (Duty tab, Home, Assign tab, Master, role descriptions, alerts, messages). Code names (`VacantSlot`, `VacantTimeService`, `vacancies.json`, `MyVacantTimes` in SeedData) were **kept** so saved data and seed data still load. Rooms are still "vacant" or "occupied" on the IMS Rooms tab (different meaning).
- Not compiled by the assistant (no .NET SDK there): build in Visual Studio and report any error text. Uninstall the app or clear its storage first if you want the seed data (ReEd subjects under CRE) written again. Existing saved accounts keep working.
