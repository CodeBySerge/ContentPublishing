# Content Publishing & Review Management System

## Stack (Current Scaffold)

- ASP.NET MVC 5 style app targeting .NET Framework 4.8
- SQL Server 2022 (connection string in Web.config)
- Tailwind CSS (CDN), jQuery + jQuery validation
- EF6, ASP.NET Identity packages, Serilog, FluentValidation

## Solution Layout

- src/ContentPublishing.Web
- src/ContentPublishing.Application
- src/ContentPublishing.Domain
- src/ContentPublishing.Infrastructure
- tests/ContentPublishing.UnitTests
- tests/ContentPublishing.IntegrationTests

## Build and Run

1. Open `ContentPublishingSystem.sln` in Visual Studio 2022.
2. Restore NuGet packages.
3. Set `ContentPublishing.Web` as startup project.
4. Run with IIS Express.

## Database Initialization

- The application uses EF6 automatic migrations at startup via `MigrateDatabaseToLatestVersion`.
- First launch will create or migrate the SQL Server schema using the `ContentPublishingDb` connection string in `src/ContentPublishing.Web/Web.config`.

### Migration Troubleshooting (EF6)

- If you see login/domain authentication errors, switch `ContentPublishingDb` to a local instance such as `(LocalDB)\\MSSQLLocalDB` in `src/ContentPublishing.Web/Web.config`.
- Build first: dotnet build ContentPublishingSystem.sln -v minimal
- Run EF6 update from terminal:
  - %USERPROFILE%\\.nuget\\packages\\entityframework\\6.5.1\\tools\\net45\\any\\ef6.exe database update --assembly "c:\\Users\\<your-user>\\ContentPublishing\\src\\ContentPublishing.Web\\bin\\ContentPublishing.Web.dll" --project-dir "c:\\Users\\<your-user>\\ContentPublishing\\src\\ContentPublishing.Web" --migrations-config "ContentPublishing.Web.Migrations.Configuration" --config "c:\\Users\\<your-user>\\ContentPublishing\\src\\ContentPublishing.Web\\Web.config" --connection-string-name "ContentPublishingDb" --verbose
- Verify applied migrations:
  - %USERPROFILE%\\.nuget\\packages\\entityframework\\6.5.1\\tools\\net45\\any\\ef6.exe migrations list --assembly "c:\\Users\\<your-user>\\ContentPublishing\\src\\ContentPublishing.Web\\bin\\ContentPublishing.Web.dll" --project-dir "c:\\Users\\<your-user>\\ContentPublishing\\src\\ContentPublishing.Web" --migrations-config "ContentPublishing.Web.Migrations.Configuration" --config "c:\\Users\\<your-user>\\ContentPublishing\\src\\ContentPublishing.Web\\Web.config" --connection-string-name "ContentPublishingDb"

## Workflow Email Notifications

- Registration email confirmation uses the configured SMTP settings in `src/ContentPublishing.Web/Web.config`.
- Workflow notifications are sent for submission, reviewer assignment, approval, rejection, and publish events.
- Update `smtpFromAddress`, SMTP host settings, and `appBaseUrl` before production use.

## Notes

- This is Phase 1 scaffold for the BRD-based implementation.
- Current implementation includes Identity auth, content/chapter CRUD, reviewer workflow, admin publishing, audit logging, and automatic EF schema initialization.
