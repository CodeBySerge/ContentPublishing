# Current Project Blueprint â€” OneHealthHandbook.Web

Purpose: This document is the blueprint for the existing legacy application in `src/OneHealthHandbook.Web`. It is written for another agent to rebuild the current system exactly as it is implemented today, not as a future .NET 8 migration.

## 1. Project context

- Project path: `src/OneHealthHandbook.Web`
- Target framework: .NET Framework 8
- Hosting model: ASP.NET MVC 5 on OWIN with IIS Express / IIS
- Database: `OneHealthHandbookDb`
- Identity: ASP.NET Identity 2.2.4 with Entity Framework 8

## 2. Dependencies and packages

The current project depends on:
- `Microsoft.AspNet.Mvc` 5.2.9
- `Microsoft.AspNet.Razor` 3.2.9
- `Microsoft.AspNet.Web.Optimization` 1.1.3
- `Microsoft.AspNet.WebPages` 3.2.9
- `Microsoft.Owin` 4.2.2
- `Microsoft.Owin.Host.SystemWeb` 4.2.2
- `Microsoft.Owin.Security.Cookies` 4.2.2
- `EntityFramework` 6.5.1
- `Microsoft.AspNet.Identity.Core` 2.2.4
- `Microsoft.AspNet.Identity.Owin` 2.2.4
- `Microsoft.AspNet.Identity.EntityFramework` 2.2.4
- `jQuery`, `jQuery.Validation`, `Microsoft.jQuery.Unobtrusive.Validation`
- `Serilog`, `Serilog.Sinks.File`
- `FluentValidation`
- `Newtonsoft.Json`

## 3. Solution structure

The current system is organized into:
- `src/OneHealthHandbook.Domain` (domain model and shared abstractions)
- `src/OneHealthHandbook.Application` (application services, validation, rules)
- `src/OneHealthHandbook.Infrastructure` (infrastructure integration and persistence)
- `src/OneHealthHandbook.Web` (web MVC application and identity hosting)

The web project currently references the Application and Infrastructure projects.

## 4. Existing web application modules

### 4.1 Controllers
- `AccountController` â€” login, registration, email confirmation, logout, profile viewing, access denied.
- `ContentController` â€” content listing, edit flow, details, delete confirmation, submit for review, author-specific content management.
- `ChapterController` â€” chapter editing and chapter-level content workflows.
- `ReviewController` â€” reviewer dashboard, notifications, pending reviews, review details, approve/reject actions.
- `AdminController` â€” administration, user and role management, reviewer assignment, publishing, scheduling, metrics.
- `HomeController` â€” landing pages and any general views.

### 4.2 Models and data entities
- `ApplicationUser` â€” extends `IdentityUser` with `FullName`, `Description`, `RoleId`, `IsActive`, `CreatedDate`, `LastModifiedDate`, `LastLogin`.
- `ApplicationDbContext` â€” EF6 Identity context plus DbSets:
  - `Contents`
  - `Chapters`
  - `Reviews`
  - `ContentReviewerAssignments`
  - `AuditLogs`
  - `ContentImages`
  - `ContentVersions`
- `ContentEntity` â€” content header and status data.
- `ChapterEntity` â€” chapter body, order, and relations to `ContentEntity`.
- `ReviewEntity` â€” review assignment, status, comments, author change notes.
- `ContentReviewerAssignmentEntity` â€” reviewer assignment records.
- `AuditLogEntity` â€” audit records for workflow actions.
- `ContentImageEntity` â€” image metadata linked to content.
- `ContentVersionEntity` â€” snapshot/version history entries.

### 4.3 View models
- `ContentEditViewModel` â€” content edit form.
- `ContentListItemViewModel` â€” content list rows.
- `ContentDetailsViewModel` â€” content detail page.
- `ChapterListItemViewModel` â€” chapter list rows.
- Review-related view models in `ReviewViewModels.cs`.
- Admin-related view models in `AdminViewModels.cs`.

### 4.4 Services
- `WorkflowNotificationService` â€” sends email notifications for content submission, reviewer assignment, approval, rejection, publishing.
- `PublishingService` â€” publishing logic, scheduling, and status transitions.
- `ContentVersionService` â€” tracks version snapshots and audit details.
- `AuditLogService` â€” records workflow actions to audit history.
- `HtmlContentSanitizer` â€” sanitizes HTML input to prevent script injection.
- `HandbookImportService` â€” content import helper.
- `ContentImageService` â€” image upload and metadata helpers.

### 4.5 Identity and startup configuration
- `Startup.cs` â€” OWIN startup class registers identity managers and cookie authentication.
- `App_Start/IdentityConfig.cs` â€” custom `ApplicationUserManager` and `ApplicationSignInManager` creation.
- `App_Start/RouteConfig.cs`, `FilterConfig.cs`, `BundleConfig.cs` â€” MVC setup.
- `App_Start/DatabaseBootstrapper.cs` â€” database initialization logic.

## 5. Important business flows

### 5.1 Registration and login
- Registration creates a new user and assigns the `Author` role.
- The app generates an email confirmation token and attempts to send confirmation mail.
- Login requires an active account and confirmed email.
- Successful login updates `LastLogin` and `LastModifiedDate`.
- Profile page displays the user's assigned role and personal fields.

### 5.2 Role management
- Roles defined in `Security/RoleNames.cs`:
  - `Author`
  - `Reviewer`
  - `Administrator`
- Role assignment is stored in both Identity role membership and `ApplicationUser.RoleId` for quick display.
- Administrators control reviewer assignment and publishing policies.

### 5.3 Content authoring
- Authors can edit content via `ContentController` and may be redirected to chapter editing if a primary chapter exists.
- Manual content creation is disabled in the current flow; authors work with existing handbook chapters.
- Content is stored with statuses such as `Draft`, `UnderReview`, `Approved`, `Published`, `Archived`.
- HTML values are sanitized before saving.

### 5.4 Review workflow
- Reviewers see their pending review queue and notifications.
- Review items are represented by `ReviewEntity` records with statuses `Pending`, `Approved`, `Rejected`.
- Review actions update both the review record and the parent content status.
- Reviewers can request clarifications with prefixed comments.

### 5.5 Publishing workflow
- Administrators can publish approved content immediately or schedule publications.
- Publishing updates `ContentEntity.Status` to `Published`, sets timestamps, logs audit history, and sends notifications.
- Scheduling stores `ScheduledPublishDate`; the `PublishingService` can later determine when to publish.

### 5.6 Audit and version history
- Every workflow action is tracked by `AuditLogService`.
- Version snapshots are created by `ContentVersionService` for content changes and workflow state changes.
- Audit entries include actor, action, original and new values, IP address, and notes.

## 6. Required rebuild scope for another agent

To rebuild the current system, another agent must:

1. Recreate the legacy `.NET Framework 4.8` web application structure in `src/OneHealthHandbook.Web`.
2. Implement the current controllers and service classes listed above.
3. Use ASP.NET Identity 2.2.4 with OWIN cookie authentication and the same registration/login profile flow.
4. Use Entity Framework 6 for the data context and the same DbSet mappings and table names.
5. Preserve the current role names and content workflow statuses.
6. Implement the email notification patterns in `WorkflowNotificationService`.
7. Implement content editing, review, and publish flows exactly as the existing project does.
8. Ensure the database schema includes tables for `Content`, `Chapter`, `Review`, `ContentReviewerAssignment`, `AuditLog`, `ContentImage`, `ContentVersion`, and standard AspNet Identity tables.

## 7. Build and run instructions for the rebuilt app

- Open `OneHealthHandbookSystem.sln` in Visual Studio 2022 or newer.
- Restore NuGet packages.
- Build the solution.
- Run the web project using IIS Express or local IIS.
- Ensure `OneHealthHandbookDb` is accessible using the configured connection string from `Web.config`.

## 8. Notes for the new agent

- Do not migrate to .NET 8 in this blueprint; this file is specifically for the current legacy system.
- Ignore any existing `OneHealthHandbook.Web.Net8` plans unless the system later requests a separate migration branch.
- Focus on rebuilding the existing behavior and workflow, not on modernizing the platform.
- Preserve the current database naming and legacy Identity configuration.

## 9. Key files to rebuild

- `src/OneHealthHandbook.Web/OneHealthHandbook.Web.csproj`
- `src/OneHealthHandbook.Web/Startup.cs`
- `src/OneHealthHandbook.Web/App_Start/IdentityConfig.cs`
- `src/OneHealthHandbook.Web/Models/ApplicationDbContext.cs`
- `src/OneHealthHandbook.Web/Models/ApplicationUser.cs`
- `src/OneHealthHandbook.Web/Models/ContentEntity.cs`
- `src/OneHealthHandbook.Web/Models/ReviewEntity.cs`
- `src/OneHealthHandbook.Web/Controllers/AccountController.cs`
- `src/OneHealthHandbook.Web/Controllers/ContentController.cs`
- `src/OneHealthHandbook.Web/Controllers/ReviewController.cs`
- `src/OneHealthHandbook.Web/Controllers/AdminController.cs`
- `src/OneHealthHandbook.Web/Services/WorkflowNotificationService.cs`
- `src/OneHealthHandbook.Web/Services/PublishingService.cs`
- `src/OneHealthHandbook.Web/Services/ContentVersionService.cs`
- `src/OneHealthHandbook.Web/Services/AuditLogService.cs`

## 10. Validation checklist

After rebuilding, verify:

## 11. Runtime behavior verified from the current app

The following behavior was observed from the existing `src/ContentPublishing.Web` application while running under IIS Express:

- The site runs successfully on IIS Express and served `http://localhost:57923/` with HTTP 200.
- `GET /Account/Login` returns HTTP 200.
- `POST /Account/Login` returns HTTP 302 and redirects back into the application on successful sign-in.
- `POST /Account/Logout` returns HTTP 302 and redirects to `GET /Account/Login`.
- `GET /Account/Register` returns HTTP 200.
- `GET /Content/Index` returns HTTP 200 for the authenticated flow.
- `GET /Content/Published` returns HTTP 200.
- `GET /Home/RoleState` returns HTTP 200 and is used by the client-side role UI.
- Static assets `Scripts/home-index.js` and `Scripts/app-role-ui.js` are loaded successfully and participate in the current home/role experience.
- `GET /favicon.ico` returned HTTP 404, which is non-critical and does not affect application startup.

These verified routes should be preserved by any rebuild agent because they reflect the current working behavior, not just planned design.

---

This blueprint is intentionally focused on the current legacy application in `src/ContentPublishing.Web` and the exact implementation constructs used by that system.
