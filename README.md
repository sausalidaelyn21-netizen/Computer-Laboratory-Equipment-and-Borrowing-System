# CLEMS — Computer Laboratory Equipment and Borrowing System

ASP.NET Core MVC (.NET 10) · Entity Framework Core · MySQL / MariaDB (Pomelo) · Bootstrap 5

A single-administrator web application for managing computer laboratory equipment, borrowers, borrowing
requests, releases, returns with inspection, maintenance, incidents, reports and an audit log.

> **Checkpoint status — read first.** This source was written in an environment without the .NET SDK,
> NuGet access or a database server. **It has not been compiled, migrated or run yet.** Follow
> "First run" below in Visual Studio; if the build reports any error, fix it before continuing (see
> "Remaining work"). `docs/TEST_PLAN.md` lists all 50 required test cases with empty result columns.

---

## 1. What is implemented

| Area | Status in source |
|---|---|
| Crimson technical-grid sign-in page (desktop + responsive) | Done — rendered and visually checked |
| Single-administrator authentication (no registration, no roles) | Done |
| Admin initialization from user secrets, lockout, rate limiting, session stamp | Done |
| Offline password reset (`dotnet run -- reset-admin-password`) + working "Forgot Password?" page | Done |
| Authenticated layout: sidebar, top bar, breadcrumb, profile menu, toasts | Done |
| Dashboard with real statistics and empty-state setup guide | Done |
| Categories, locations (activate/deactivate, no unsafe deletes) | Done |
| Equipment inventory: CRUD, search, filters, sort, pagination, archive/restore, history | Done |
| Borrowers (records only — no passwords, no login) | Done |
| Borrowing: request → approve (reserve) / reject → release → return | Done |
| Returns with inspection: partial/full, good/damaged/missing, auto incidents, duplicate protection | Done |
| Maintenance: create, edit, complete (repaired / not repairable), cancel | Done |
| Incidents: create, assign, update, resolve with inventory action, close, link to maintenance | Done |
| 10 reports with filters, print layout, CSV export | Done |
| Append-only activity log | Done |
| EF Core model, relationships, unique indexes, CHECK constraints | Done |
| EF Core migration files | **Not generated** — needs `dotnet ef` on your machine (step 5) |
| Automated tests (xUnit, 40+ cases) | Written, **not yet run** |

---

## 2. Requirements on your computer

- Visual Studio 2026 (or a version that supports .NET 10) with the **ASP.NET and web development** workload
- .NET 10 SDK (`dotnet --version` should print 10.x)
- MySQL 8.0+ **or** MariaDB 10.4+ (XAMPP is fine) and MySQL Workbench
- Internet access the first time (NuGet restore; the pages load Bootstrap, Bootstrap Icons, jQuery validation
  and the Inter font from jsDelivr / Google Fonts)

### Why EF Core 9 on .NET 10

`Pomelo.EntityFrameworkCore.MySql` has no EF Core 10 release yet; its newest version (9.0.0) only accepts EF Core
9.0.x. The project therefore targets **net10.0** but pins every `Microsoft.EntityFrameworkCore.*` package to
**9.0.10**. Do **not** let Visual Studio "update all packages" to EF Core 10 — the restore will fail with NU1608 /
NU1107. EF Core 9 support ends on 10 November 2026; when Pomelo publishes a 10.x build, upgrade Pomelo and all
EF Core packages together.

---

## 3. First run (step by step)

### Step 1 — Start the database server
- **XAMPP:** open the XAMPP Control Panel → *Start* next to **MySQL** (this is MariaDB).
- **MySQL Server:** make sure the `MySQL80` Windows service is running.

### Step 2 — Create the database and an application user (MySQL Workbench)
1. Open MySQL Workbench → connect to your server (Host `127.0.0.1`, your port — usually `3306`, user `root`).
2. *File → Open SQL Script* → `database/01_create_database_and_user.sql`.
3. Replace `CHANGE_ME_STRONG_PASSWORD` with a real password, run the script (lightning icon).
4. Note the value of `SELECT VERSION()` at the end, e.g. `8.0.36` or `10.4.32-MariaDB`.

### Step 3 — Open the solution
Open `ComputerLaboratoryBorrowingSystem.sln` in Visual Studio. Wait for the NuGet restore to finish.

### Step 4 — Store secrets with User Secrets (never in appsettings.json)
In Solution Explorer right-click the **ComputerLaboratoryBorrowingSystem** project → **Manage User Secrets**, and
paste (with your own values):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=3306;Database=computer_lab_borrowing_db;User=clems_app;Password=YOUR_APP_DB_PASSWORD;"
  },
  "Database": {
    "ServerVersion": "10.4.32-mariadb"
  },
  "InitialAdmin": {
    "Username": "admin",
    "Password": "Choose-A-Strong-Passw0rd!"
  }
}
```

- `ServerVersion`: use `8.0.36-mysql` style for MySQL or `10.4.32-mariadb` style for MariaDB (your real version).
- Use your real port if it is not 3306.
- The initial password must have at least 10 characters with upper case, lower case, a number and a symbol.

Command-line equivalent (run inside the `ComputerLaboratoryBorrowingSystem` project folder):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=127.0.0.1;Port=3306;Database=computer_lab_borrowing_db;User=clems_app;Password=YOUR_APP_DB_PASSWORD;"
dotnet user-secrets set "Database:ServerVersion" "10.4.32-mariadb"
dotnet user-secrets set "InitialAdmin:Username" "admin"
dotnet user-secrets set "InitialAdmin:Password" "Choose-A-Strong-Passw0rd!"
```

User secrets are stored in your Windows profile, outside the project folder, so they are never committed to Git.

### Step 5 — Build, then create and apply the migration
1. **Build → Rebuild Solution.** Fix any error before continuing.
2. Create the initial migration — either
   - Visual Studio: *Tools → NuGet Package Manager → Package Manager Console*, default project
     **ComputerLaboratoryBorrowingSystem**, then
     ```powershell
     Add-Migration InitialCreate
     Update-Database
     ```
   - or a terminal in the solution folder:
     ```bash
     dotnet tool restore
     dotnet ef migrations add InitialCreate --project ComputerLaboratoryBorrowingSystem
     dotnet ef database update --project ComputerLaboratoryBorrowingSystem
     ```
3. Commit the generated `Migrations/` folder to source control.

(The app also applies pending migrations on startup when `Database:ApplyMigrationsOnStartup` is `true`.)

### Step 6 — Verify the schema in Workbench
Open and run `database/02_verify_schema.sql`. Expect 12 tables, RESTRICT delete rules on every foreign key, the
unique indexes and the CHECK constraints listed. `database/03_constraint_smoke_test.sql` lets you prove the
database itself rejects invalid quantities and unsafe deletes.

### Step 7 — Run
Press **F5** (profile `https`). On first start the app creates the administrator from `InitialAdmin` (only when no
administrator exists). Sign in, then **remove the initial password secret**:

```bash
dotnet user-secrets remove "InitialAdmin:Password"
```

### Step 8 — Run the automated tests
*Test → Run All Tests* in Visual Studio, or `dotnet test` in the solution folder. Record the results in
`docs/TEST_PLAN.md`, then do the manual checks listed there.

---

## 4. The administrator account

- There is exactly one system user. A unique index plus a CHECK constraint on `administrators.SingletonKey`
  makes a second administrator row impossible at the database level. There is no registration page,
  no role selection and no user management.
- Passwords are hashed with ASP.NET Core's `PasswordHasher` (PBKDF2-HMAC-SHA512, salted, 100,000 iterations).
- 5 failed sign-ins lock sign-in for 15 minutes (`Security` section in `appsettings.json`); sign-in posts are also
  rate-limited to 10 per minute per IP address. Because there is only one account, someone repeatedly typing
  wrong passwords can delay the real administrator for the lockout period — keep the server on a trusted network.
- Sessions expire after 30 minutes of inactivity; "Remember me" keeps a sign-in for 7 days.
- **Change the password:** *My Account → Change password*. All other signed-in sessions are signed out.
- **Forgot the password:** stop the app, open a terminal in the project folder and run
  ```bash
  dotnet run -- reset-admin-password
  ```
  Type the new password twice (masked). This needs access to the server and its database, which is the recovery
  channel for a single-administrator system. The sign-in page's *Forgot Password?* link explains this.

**Why not full ASP.NET Core Identity?** Identity is designed for many users, roles, registration, e-mail
confirmation and external logins — none of which this system may have. The app reuses Identity's password hasher
with standard cookie authentication, a security stamp checked on every request, lockout, rate limiting,
anti-forgery tokens on every POST, and a global authorization filter, so every page requires the administrator
unless it is explicitly the sign-in, forgot-password or error page.

---

## 5. Inventory and borrowing rules

Every unit of an equipment record is in exactly one bucket, and MySQL enforces
`Total = Available + Reserved + Borrowed + Maintenance + Damaged + Missing` with a CHECK constraint.

| Event | Movement |
|---|---|
| Request submitted (Pending) | none — availability is only pre-checked |
| Approved | Available → **Reserved** (re-checked atomically) |
| Rejected / cancelled while pending | none |
| Cancelled after approval | Reserved → Available |
| Released | Reserved → **Borrowed** |
| Returned good | Borrowed → Available |
| Returned damaged | Borrowed → **Damaged** (never directly available) |
| Not returned | Borrowed → **Missing** |
| Maintenance opened | Available or Damaged → **Maintenance** |
| Maintenance completed — repaired | Maintenance → Available |
| Maintenance completed — not repairable | Maintenance → Damaged |
| Maintenance cancelled | Maintenance → original bucket |
| Incident resolved — recovered | Missing → Available |
| Incident resolved — write off | Damaged/Missing → removed from Total (record archived at 0) |

- **Status** (Available, Partially Available, Fully Borrowed, Under Maintenance, Damaged, Missing, Retired) is
  always computed from the buckets; **Condition** (Good … Non-functional) is a separate, manually set field.
  Damaged or Non-functional condition blocks borrowing.
- **Transaction states:** Pending → Approved | Rejected | Cancelled; Approved → Released | Cancelled;
  Released → Partially Returned | Returned. Rejected, Returned and Cancelled are final. *Overdue* is computed
  (released/partially returned and past the due date), so it is never stale.
- Every state change re-reads the records, runs inside a database transaction and uses optimistic concurrency
  stamps; two simultaneous approvals of the last unit cannot both succeed. Returns carry a one-time submission
  token so a double-submitted form is never recorded twice.
- Nothing with history is deleted: equipment is archived, categories/locations/borrowers are deactivated,
  incidents and activity logs are kept.

---

## 6. Project structure

```
ComputerLaboratoryBorrowingSystem.sln
ComputerLaboratoryBorrowingSystem/
  Controllers/      Account, Dashboard, Equipment, Categories, Locations, Borrowers, Borrowing, Returns,
                    Maintenance, Incidents, Reports, ActivityLogs, AccountSettings, Home (errors)
  Data/             ApplicationDbContext, DatabaseInitializer, transaction helper
  Infrastructure/   security (claims, password policy, session validation), paging, UI helpers, CLI reset
  Models/           entities and enums
  Services/         AdminAccount, Inventory, Borrowing, Return, Maintenance, Incident, Dashboard, Report, ActivityLog
  ViewModels/       form and page models
  Views/            Razor views (Shared/_Layout, Shared/_AccountLayout, partials)
  wwwroot/          css/auth.css, site.css, dashboard.css, forms.css, tables.css · js/auth.js, site.js · images/
ComputerLaboratoryBorrowingSystem.Tests/   xUnit tests (EF Core InMemory)
database/          Workbench scripts: create DB/user, verify schema, constraint smoke test
docs/TEST_PLAN.md  the 50 required test cases with result columns
```

---

## 7. Remaining work (exact next steps)

1. **Build the solution** in Visual Studio and fix any compiler errors (none are known, but the code has not
   been compiled yet).
2. **Generate and apply the `InitialCreate` migration** (step 5) and run `database/02_verify_schema.sql`.
3. **Run the automated tests** and the manual checks in `docs/TEST_PLAN.md`; record real results.
4. Optional: replace the CDN references in `Views/Shared/_Layout.cshtml` and `_AccountLayout.cshtml` with local
   copies (Visual Studio → right-click `wwwroot` → *Add → Client-Side Library…*) if the lab PCs have no internet.

## 8. Deploying beyond your own PC

User secrets only work in the Development environment. On a server, supply the same settings as environment
variables, e.g. `ConnectionStrings__DefaultConnection`, `Database__ServerVersion`, `InitialAdmin__Username`,
`InitialAdmin__Password` (remove the password variable after the first start), and serve the site over HTTPS
(the authentication cookie is HTTPS-only outside Development).
