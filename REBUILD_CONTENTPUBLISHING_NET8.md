# Rebuild One Health Handbook as .NET 8 MVC Solution

This document contains an executable rebuild script for a new .NET 8 MVC solution with the existing module layout.

> Note: The current workspace contains a legacy `OneHealthHandbook.Web` .NET 4.8 MVC project and an empty `OneHealthHandbook.Web.Net8` folder. This script recreates the .NET 8 solution structure and connects it to the same SQL Server database. The application is named `One Health Handbook`.

## Prerequisites

- .NET 8 SDK installed
- Visual Studio 2022 installed
- SQL Server instance accessible at `SOFINE\SQLEXPRESS`
- Existing database: `OneHealthHandbookDb`

## Workspace root

Run all commands from the repository root:

```powershell
cd C:\Users\SergeN\OneHealthHandbook
```

## Step 1: Create the solution and module projects (use existing SuperClass)

This project will reuse your organization's shared library `SuperClass` rather than reimplementing common utilities. The steps below create the new .NET 8 projects and show how to add the existing `SuperClass` project or package.

```powershell
# Create solution if not present
if (-not (Test-Path OneHealthHandbook.sln)) {
    dotnet new sln -n OneHealthHandbook
}

# Create module folders and projects (web + domain/app/infrastructure/test modules)
mkdir -Force src,tests

dotnet new classlib -n OneHealthHandbook.Domain -o src\OneHealthHandbook.Domain -f net8.0
dotnet new classlib -n OneHealthHandbook.Application -o src\OneHealthHandbook.Application -f net8.0
dotnet new classlib -n OneHealthHandbook.Infrastructure -o src\OneHealthHandbook.Infrastructure -f net8.0
dotnet new mvc -n OneHealthHandbook.Web.Net8 -o src\OneHealthHandbook.Web.Net8 -f net8.0
dotnet new xunit -n OneHealthHandbook.UnitTests -o tests\OneHealthHandbook.UnitTests -f net8.0
dotnet new xunit -n OneHealthHandbook.IntegrationTests -o tests\OneHealthHandbook.IntegrationTests -f net8.0

# IMPORTANT: Add your existing SuperClass shared library to the solution instead of recreating it.
# Option A: If you have the SuperClass project source (recommended), add it to the solution:
#    dotnet sln add "<path-to-SuperClass>\SuperClass.csproj"
# Option B: If SuperClass is distributed as a NuGet package in your org, add it to projects that need it:
#    dotnet add src\OneHealthHandbook.Web.Net8\OneHealthHandbook.Web.Net8.csproj package SuperClass --version <version>
```

## Step 2: Add projects to the solution

```powershell
dotnet sln add src\OneHealthHandbook.Domain\OneHealthHandbook.Domain.csproj

dotnet sln add src\OneHealthHandbook.Application\OneHealthHandbook.Application.csproj

dotnet sln add src\OneHealthHandbook.Infrastructure\OneHealthHandbook.Infrastructure.csproj

dotnet sln add src\OneHealthHandbook.Web.Net8\OneHealthHandbook.Web.Net8.csproj

dotnet sln add tests\OneHealthHandbook.UnitTests\OneHealthHandbook.UnitTests.csproj

dotnet sln add tests\OneHealthHandbook.IntegrationTests\OneHealthHandbook.IntegrationTests.csproj
```

## Step 3: Create project references

```powershell
dotnet add src\OneHealthHandbook.Application\OneHealthHandbook.Application.csproj reference src\OneHealthHandbook.Domain\OneHealthHandbook.Domain.csproj

dotnet add src\OneHealthHandbook.Infrastructure\OneHealthHandbook.Infrastructure.csproj reference src\OneHealthHandbook.Domain\OneHealthHandbook.Domain.csproj

dotnet add src\OneHealthHandbook.Web.Net8\OneHealthHandbook.Web.Net8.csproj reference src\OneHealthHandbook.Application\OneHealthHandbook.Application.csproj src\OneHealthHandbook.Infrastructure\OneHealthHandbook.Infrastructure.csproj

dotnet add tests\OneHealthHandbook.UnitTests\OneHealthHandbook.UnitTests.csproj reference src\OneHealthHandbook.Domain\OneHealthHandbook.Domain.csproj src\OneHealthHandbook.Application\OneHealthHandbook.Application.csproj src\OneHealthHandbook.Infrastructure\OneHealthHandbook.Infrastructure.csproj

dotnet add tests\OneHealthHandbook.IntegrationTests\OneHealthHandbook.IntegrationTests.csproj reference src\OneHealthHandbook.Web.Net8\OneHealthHandbook.Web.Net8.csproj
```

## Add the existing `SuperClass` and compatibility notes

If your organization's `SuperClass` project is available, add it to the solution rather than recreating its functionality. There are three common integration approaches depending on how `SuperClass` is distributed and what target framework it uses:

- Option A â€” SuperClass source (recommended): add the project to the solution and reference it from projects that need it.

```powershell
# Example (adjust path):
dotnet sln add "..\SuperClass\SuperClass.csproj"
dotnet add src\OneHealthHandbook.Web.Net8\OneHealthHandbook.Web.Net8.csproj reference "..\SuperClass\SuperClass.csproj"
```

- Option B â€” SuperClass as internal NuGet package: add the package to the projects that need it.

```powershell
dotnet add src\OneHealthHandbook.Web.Net8\OneHealthHandbook.Web.Net8.csproj package SuperClass --version <version>
```

- Option C â€” SuperClass is a .NET Framework 4.8 assembly (binary only): either obtain a .NET Standard / .NET 8 compatible build, or create a compatibility adapter. Recommended actions:
    - Preferred: build/publish a .NET Standard 2.0 (or net6/net8) version of `SuperClass` and consume it directly.
    - Fallback: create an interop wrapper service or a small Windows service that exposes the needed functionality (e.g., via local HTTP or messaging) and call it from the .NET 8 app.

Notes about compatibility:
- Referencing a `net48` project directly from a `net8` project is unsupported. Retarget `SuperClass` to `netstandard2.0` or `net8.0` where possible.
- `SuperClass` contains Windows-specific APIs (EventLog, Registry, System.DirectoryServices) â€” if you migrate it to .NET 8, ensure those APIs are supported on your target runtime or isolate them behind platform-specific implementations.


## Step 4: Add required NuGet packages for the Web project

```powershell
dotnet add src\OneHealthHandbook.Web.Net8\OneHealthHandbook.Web.Net8.csproj package Microsoft.EntityFrameworkCore.SqlServer Microsoft.EntityFrameworkCore.Design Microsoft.AspNetCore.Identity.EntityFrameworkCore Microsoft.EntityFrameworkCore.Tools
```

## Step 5: Configure the Web project for SQL Server and MVC

Create or replace `src\OneHealthHandbook.Web.Net8\appsettings.json` with:

```json
{
  "ConnectionStrings": {
    "OneHealthHandbookDb": "Server=SOFINE\\SQLEXPRESS;Database=OneHealthHandbookDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Create or replace `src\OneHealthHandbook.Web.Net8\appsettings.Development.json` with the same connection string.

## Step 6: Add EF Core Identity data layer files

Create `src\OneHealthHandbook.Web.Net8\Data\ApplicationDbContext.cs` with:

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace OneHealthHandbook.Web.Net8.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Add DbSet<> entries for domain entities here.
    }
}
```

## Step 7: Replace `src\OneHealthHandbook.Web.Net8\Program.cs` with the .NET 8 startup code

Create `src\OneHealthHandbook.Web.Net8\Program.cs` with:

```csharp
using OneHealthHandbook.Web.Net8.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OneHealthHandbookDb")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

## Step 8: Create minimal MVC controllers and views

Create `src\OneHealthHandbook.Web.Net8\Controllers\HomeController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace OneHealthHandbook.Web.Net8.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => View();
        public IActionResult Privacy() => View();
    }
}
```

## Step 8a: Add view files for the home page

Create `src\OneHealthHandbook.Web.Net8\Views\Home\Index.cshtml`:

```html
@{
    ViewData["Title"] = "Home";
}

<h1>Welcome to One Health Handbook</h1>
<p>This is the application landing page. Use it to summarize the health handbook workflow and link to author, reviewer, and administrator areas.</p>
```

Create `src\OneHealthHandbook.Web.Net8\Views\Home\Privacy.cshtml`:

```html
@{
    ViewData["Title"] = "Privacy";
}

<h1>Privacy Policy</h1>
<p>This page describes how the application handles user data and privacy controls.</p>
```

Create `src\OneHealthHandbook.Web.Net8\Views\Shared\_Layout.cshtml`:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - One Health Handbook</title>
    <link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
</head>
<body>
    <header>
        <nav class="navbar navbar-expand-lg navbar-light bg-light">
            <a class="navbar-brand" href="/">One Health Handbook</a>
            <div class="collapse navbar-collapse">
                <ul class="navbar-nav mr-auto">
                    <li class="nav-item"><a class="nav-link" href="/">Home</a></li>
                    <li class="nav-item"><a class="nav-link" href="/Home/Privacy">Privacy</a></li>
                </ul>
            </div>
        </nav>
    </header>

    <div class="container mt-4">
        @RenderBody()
    </div>

    <footer class="border-top mt-4 pt-3 text-muted">
        <div class="container">&copy; @DateTime.Now.Year Content Publishing</div>
    </footer>

    <script src="~/lib/jquery/dist/jquery.min.js"></script>
    <script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
</body>
</html>
```

Create `src\OneHealthHandbook.Web.Net8\Views\Shared\_ViewStart.cshtml`:

```csharp
@{
    Layout = "~/Views/Shared/_Layout.cshtml";
}
```

## Step 8b: Page descriptions

- `Home/Index.cshtml`: the application landing page with a summary of the One Health Handbook workflow and primary navigation.
- `Home/Privacy.cshtml`: a static privacy policy page describing user data handling.
- `Shared/_Layout.cshtml`: the common site shell with header, nav, body placeholder, and footer.
- `Shared/_ViewStart.cshtml`: sets the default layout for views.

## Step 9: Restore, build, and run

You can use Visual Studio 2022 to open the generated solution and run the project.

```powershell
dotnet restore

dotnet build OneHealthHandbook.sln

dotnet run --project src\OneHealthHandbook.Web.Net8\OneHealthHandbook.Web.Net8.csproj
```

## Optional: Add database migration support

```powershell
dotnet tool install --global dotnet-ef --version 8.0.0

dotnet ef migrations add InitialCreate --project src\OneHealthHandbook.Web.Net8\OneHealthHandbook.Web.Net8.csproj --startup-project src\OneHealthHandbook.Web.Net8\OneHealthHandbook.Web.Net8.csproj

dotnet ef database update --project src\OneHealthHandbook.Web.Net8\OneHealthHandbook.Web.Net8.csproj --startup-project src\OneHealthHandbook.Web.Net8\OneHealthHandbook.Web.Net8.csproj
```

## Notes for the agent

- This script recreates the solution and project structure in .NET 8.
- The existing business logic from the legacy `OneHealthHandbook.Web` project must be migrated into the new `src\OneHealthHandbook.Web.Net8` and domain/application/infrastructure modules.
- If the user wants the same database schema, preserve the existing connection string and run EF migrations or seed the identity tables as needed.
- Open the generated `OneHealthHandbook.sln` in Visual Studio 2022 to develop, debug, and run the application.

