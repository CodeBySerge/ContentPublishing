
# Rebuild One Health Handbook â€” Visual Studio Code (Logical Instructions)

Purpose: concise, step-by-step logic to rebuild the One Health Handbook application in Visual Studio Code. This file contains the procedure and verification checklist; it intentionally describes actions and decisions (not full source code) so another agent or developer can follow and implement them.

Prerequisites:
- Install .NET 8 SDK and confirm `dotnet` works on PATH.
- Install Visual Studio Code and the C# extension (OmniSharp) plus NuGet / EF Core tooling as needed.
- Ensure SQL Server instance reachable (e.g., `SOFINE\\SQLEXPRESS`) and the `OneHealthHandbookDb` exists or can be created.
- Obtain access to the organization's `SuperClass` library (project source, internal NuGet package, or binary) and confirm license/usage.

High-level goals:
- Produce a .NET 8 MVC solution named One Health Handbook.
- Reuse `SuperClass` rather than reimplement shared utilities.
- Configure EF Core Identity to use the existing SQL Server database.
- Provide guidance for migration of legacy logic and platform-specific features.

Procedure â€” logical steps (implement each step using the command/tool of choice):

1) Prepare repository workspace
- Open the repository root in VS Code.
- Ensure a clean workspace (stash/commit local changes) and create a new solution file if needed.

2) Create projects and solution structure
- Create the following module projects (target .NET 8): Domain, Application, Infrastructure, Web (MVC), and test projects.
- Add projects to the solution and create project-to-project references: Application -> Domain; Infrastructure -> Domain; Web -> Application + Infrastructure; tests referencing relevant projects.

3) Integrate `SuperClass`
- If you have the `SuperClass` project source: add it to the solution and reference it from projects that need shared functionality (Web, Infrastructure, etc.).
- If you have `SuperClass` as an internal NuGet package: add the package to consumer projects and pin a version.
- If `SuperClass` is only available as a net48 binary: plan a compatibility strategy (preferred: obtain/produce a .NET Standard or net8 build; fallback: create an adapter service or run compatible parts inside a net48 compatibility process and call it via IPC/HTTP).
- Verify API surface required by existing code (globals, logger, email, crypto, helpers) and plan adapter interfaces where platform differences exist.

4) Add NuGet dependencies (logical list)
- For Web project: EF Core SQL Server provider, EF Core Design/Tools, ASP.NET Core Identity EF Core integration, and any web packages (e.g., Bootstrap or static assets pipeline) you use.
- For Infrastructure: data providers, SMTP/email client or wrappers, logging sinks that align with `SuperClass` if applicable.

5) Configuration and secrets
- Create configuration files for production and development environments (connection strings, logging levels, allowed hosts).
- Do NOT store secrets in source control; use environment variables or a secrets manager for DB credentials and SMTP passwords.

6) Data layer and Identity
- Implement an EF Core `ApplicationDbContext` derived from IdentityDbContext (or equivalent) and include DbSet entries for domain entities.
- Configure the context to use the connection string pointing to the SQL Server instance.
- Add migration plan: create an initial migration that matches the current schema or write a migration that brings the database to the required shape while preserving existing data.
- If the production database already contains legacy Identity tables, prefer writing migration scripts that map existing tables to the EF Core model, or use a dedicated Identity migration strategy to avoid data loss.

7) Startup wiring (application host logic)
- Register MVC controllers and Razor views, configure the DB context and Identity services, and set cookie/auth options consistent with legacy behavior (login path, session/cookie expiry).
- Add static file middleware and routing configuration.

8) Controllers, views, and pages
- Implement minimal controllers and views to validate startup (Home index, privacy, account login/logout pages).
- Add view layout/_ViewStart that matches the legacy site shell and include navigation to major flows: author, reviewer, admin.

9) SuperClass compatibility details
- Inventory `SuperClass` APIs used by legacy app: list classes and methods (Globals, Security, Emailer, Crypto, Logger, Export, etc.).
- For each API, decide: consume directly (if compatible), port to .NET 8, or create an adapter that exposes the same methods but maps to new implementations.
- For cryptography: prefer modern APIs on .NET 8 (Aes instead of RijndaelManaged) and verify data compatibility when encrypting/decrypting existing ciphertext.

10) Build, run, and debugging in VS Code
- Create/validate tasks and a launch configuration so debugging can be started from VS Code (attach to Kestrel/IIS Express if required).
- Run the application in Development mode, verify the default route and identity login pages load, and check the application uses the intended SQL Server database.

11) Database migration and verification
- If using EF migrations, add an initial migration capturing the model and run `database update` against a test copy of the production DB first.
- Verify identity tables exist and user roles/claims are intact. If necessary, create migration or script to recreate missing tables (e.g., `AspNetUserClaims`) preserving data.

12) Legacy code migration guidance
- Identify high-level modules in the legacy `OneHealthHandbook.Web` (controllers, services, view models, views, and data access).
- Move non-UI business logic into `OneHealthHandbook.Application` and `OneHealthHandbook.Infrastructure` with clear interfaces and unit tests.
- Migrate views gradually: port the layout and a small set of critical pages first, verifying functionality end-to-end.

13) Tests and CI
- Add unit/integration tests covering identity, core domain rules, and critical controller flows.
- Add a CI pipeline that restores, builds, runs tests, and optionally applies migrations in a controlled environment.

14) Deployment notes
- For local testing, prefer Kestrel (dotnet run) or IIS/IIS Express for parity with legacy hosting. For production, containerization or an Azure App Service are options; ensure Windows-only `SuperClass` features are available or replaced.

Verification checklist (what to confirm after rebuild):
- Solution opens in VS Code and all projects restore.
- The application builds and starts in Development mode.
- The site connects to the SQL Server instance and Identity tables are present.
- Login and role-based areas function; seeded admin user exists or can be created.
- Shared functions from `SuperClass` are available and behave equivalently (logging, email sending, exports).
- Edge cases handled: encryption/decryption compatibility, AD lookups or Windows-only APIs either work or are isolated behind adapters.

Review guidance for an agent implementing the steps:
- Implement each step incrementally and run the app after each major change.
- When integrating `SuperClass`, prefer adding the project source to the solution for easier debugging. If only a binary is available, create small tests to validate expected behavior.
- When in doubt about schema changes, create a test copy of the database and run migrations there before applying to production.
- Keep commits focused and small (e.g., "Add Application project", "Wire Identity context", "Port Home views").

Files to produce (logical list for the implementer):
- Solution file `OneHealthHandbook.sln`.
- Projects: `OneHealthHandbook.Domain`, `OneHealthHandbook.Application`, `OneHealthHandbook.Infrastructure`, `OneHealthHandbook.Web.Net8`.
- `ApplicationDbContext` and Identity wiring in the Web project.
- Minimal controllers and views to validate startup.
- Tasks/launch configuration for VS Code debugging.
- Migration scripts or EF Core migrations for the database.

Post-creation review checklist (what I checked while drafting these instructions):
- Confirmed target is VS Code + .NET 8 per user request.
- Included SuperClass integration options and compatibility guidance.
- Omitted exact source code; steps provide clear logic for an agent to implement.
- Listed verification steps to ensure safe migration of live data.

Next steps I can take for you:
- Convert this logic into an executable PowerShell script with commands, if you want runnable automation.
- Generate a sample VS Code `launch.json` and `tasks.json` to speed up debugging.
- Attempt to detect a local `SuperClass` project in the workspace and automatically add it to the solution.

---
Generated by the assistant as a logical rebuild plan for implementation in VS Code.

