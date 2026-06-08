# Content Publishing Handoff

Use this repo on another PC by cloning the GitHub repository and opening the solution in Visual Studio 2022.

## Repository
- Remote: https://github.com/CodeBySerge/ContentPublishing.git
- Branch: main

## How to Continue on Another PC
1. Install Visual Studio 2022 with ASP.NET and web development workloads.
2. Install the .NET Framework 4.8.1 Developer Pack if it is not already available.
3. Clone the repository:
   ```powershell
   git clone https://github.com/CodeBySerge/ContentPublishing.git
   ```
4. Open `ContentPublishingSystem.sln` in Visual Studio.
5. Restore NuGet packages.
6. Set `ContentPublishing.Web` as the startup project.
7. Update `src/ContentPublishing.Web/Web.config` with your local SQL Server connection string.
8. Run the app with IIS Express.

## Local Database
- The app uses EF6 automatic migrations at startup.
- If the SQL Server connection is valid and your account has permission, the database and tables will be created automatically on first launch.

## Tests
- Run unit tests with:
  ```powershell
  dotnet test tests/ContentPublishing.UnitTests/ContentPublishing.UnitTests.csproj -v minimal
  ```

## Current Status
- Git repository initialized and pushed to GitHub.
- Initial commit created on `main`.
- Unit tests pass locally.
- The app currently needs a valid local SQL Server connection string on the machine where you run it.
- The solution now targets .NET Framework 4.8.1 and carries reference assemblies in NuGet for smoother VS Code builds.