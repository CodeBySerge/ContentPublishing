# Content Publishing & Review Management System

## Stack (Current Scaffold)

- ASP.NET MVC 5 style app targeting .NET Framework 4.8
- SQL Server 2022 (connection string in Web.config)
- Tailwind CSS (CDN), jQuery + jQuery validation
- EF6, ASP.NET Identity packages, Serilog, FluentValidation

## Solution Layout

- src/OneHealthHandbook.Web
- src/OneHealthHandbook.Application
- src/OneHealthHandbook.Domain
- src/OneHealthHandbook.Infrastructure
- tests/OneHealthHandbook.UnitTests
- tests/OneHealthHandbook.IntegrationTests

## Build and Run

1. Open `OneHealthHandbookSystem.sln` in Visual Studio 2022.
2. Restore NuGet packages.
3. Set `OneHealthHandbook.Web` as startup project.
4. Run with IIS Express.

## Database Initialization

- The application uses EF6 automatic migrations at startup via `MigrateDatabaseToLatestVersion`.
- First launch will create or migrate the SQL Server schema using the `OneHealthHandbookDb` connection string in `src/OneHealthHandbook.Web/Web.config`.

### Migration Troubleshooting (EF6)

- If you see login/domain authentication errors, switch `OneHealthHandbookDb` to a local instance such as `(LocalDB)\\MSSQLLocalDB` in `src/OneHealthHandbook.Web/Web.config`.
- Build first: dotnet build OneHealthHandbookSystem.sln -v minimal
- Run EF6 update from terminal:
  - %USERPROFILE%\\.nuget\\packages\\entityframework\\6.5.1\\tools\\net45\\any\\ef6.exe database update --assembly "c:\\Users\\<your-user>\\OneHealthHandbook\\src\\OneHealthHandbook.Web\\bin\\OneHealthHandbook.Web.dll" --project-dir "c:\\Users\\<your-user>\\OneHealthHandbook\\src\\OneHealthHandbook.Web" --migrations-config "OneHealthHandbook.Web.Migrations.Configuration" --config "c:\\Users\\<your-user>\\OneHealthHandbook\\src\\OneHealthHandbook.Web\\Web.config" --connection-string-name "OneHealthHandbookDb" --verbose
- Verify applied migrations:
  - %USERPROFILE%\\.nuget\\packages\\entityframework\\6.5.1\\tools\\net45\\any\\ef6.exe migrations list --assembly "c:\\Users\\<your-user>\\OneHealthHandbook\\src\\OneHealthHandbook.Web\\bin\\OneHealthHandbook.Web.dll" --project-dir "c:\\Users\\<your-user>\\OneHealthHandbook\\src\\OneHealthHandbook.Web" --migrations-config "OneHealthHandbook.Web.Migrations.Configuration" --config "c:\\Users\\<your-user>\\OneHealthHandbook\\src\\OneHealthHandbook.Web\\Web.config" --connection-string-name "OneHealthHandbookDb"

## Workflow Email Notifications

- Registration email confirmation uses the configured SMTP settings in `src/OneHealthHandbook.Web/Web.config`.
- Workflow notifications are sent for submission, reviewer assignment, approval, rejection, and publish events.
- Update `smtpFromAddress`, SMTP host settings, and `appBaseUrl` before production use.

## Notes

- This is Phase 1 scaffold for the BRD-based implementation.
- Current implementation includes Identity auth, content/chapter CRUD, reviewer workflow, admin publishing, audit logging, and automatic EF schema initialization.


## User test

Author
Username: author.workflow@OneHealthHandbook.local
Password: WorkflowAuthor1

Reviewer
Username: reviewer.workflow@OneHealthHandbook.local
Password: WorkflowReviewer1

Admin
Username: admin.workflow@OneHealthHandbook.local
Password: WorkflowAdmin1

## cmd to run the app
& "C:\Program Files\IIS Express\iisexpress.exe" /path:"C:\Users\SergeN\ContentPublishing\src\OneHealthHandbook.Web" /port:53954
