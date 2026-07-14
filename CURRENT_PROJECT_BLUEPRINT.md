# Current Project Blueprint — ContentPublishing.Web

Purpose: This document is the blueprint for the existing legacy application in `src/ContentPublishing.Web`. It is written for another agent to rebuild the current system exactly as it is implemented today, not as a future .NET 8 migration.

## 1. Project context

- Project path: `src/ContentPublishing.Web`
- Target framework: .NET Framework 8
- Hosting model: ASP.NET MVC 5 on OWIN with IIS Express / IIS
- Database: `ContentPublishingDb`
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
- `src/ContentPublishing.Domain` (domain model and shared abstractions)
- `src/ContentPublishing.Application` (application services, validation, rules)
- `src/ContentPublishing.Infrastructure` (infrastructure integration and persistence)
- `src/ContentPublishing.Web` (web MVC application and identity hosting)

The web project currently references the Application and Infrastructure projects.

## 4. Existing web application modules

### 4.1 Controllers
- `AccountController` — login, registration, email confirmation, logout, profile viewing, access denied.
- `ContentController` — content listing, edit flow, details, delete confirmation, submit for review, author-specific content management.
- `ChapterController` — chapter editing and chapter-level content workflows.
- `ReviewController` — reviewer dashboard, notifications, pending reviews, review details, approve/reject actions.
- `AdminController` — administration, user and role management, reviewer assignment, publishing, scheduling, metrics.
- `HomeController` — landing pages and any general views.

### 4.2 Models and data entities
- `ApplicationUser` — extends `IdentityUser` with `FullName`, `Description`, `RoleId`, `IsActive`, `CreatedDate`, `LastModifiedDate`, `LastLogin`.
- `ApplicationDbContext` — EF6 Identity context plus DbSets:
  - `Contents`
  - `Chapters`
  - `Reviews`
  - `ContentReviewerAssignments`
  - `AuditLogs`
  - `ContentImages`
  - `ContentVersions`
- `ContentEntity` — content header and status data.
- `ChapterEntity` — chapter body, order, and relations to `ContentEntity`.
- `ReviewEntity` — review assignment, status, comments, author change notes.
- `ContentReviewerAssignmentEntity` — reviewer assignment records.
- `AuditLogEntity` — audit records for workflow actions.
- `ContentImageEntity` — image metadata linked to content.
- `ContentVersionEntity` — snapshot/version history entries.

### 4.3 View models
- `ContentEditViewModel` — content edit form.
- `ContentListItemViewModel` — content list rows.
- `ContentDetailsViewModel` — content detail page.
- `ChapterListItemViewModel` — chapter list rows.
- Review-related view models in `ReviewViewModels.cs`.
- Admin-related view models in `AdminViewModels.cs`.

### 4.4 Services
- `WorkflowNotificationService` — sends email notifications for content submission, reviewer assignment, approval, rejection, publishing.
- `PublishingService` — publishing logic, scheduling, and status transitions.
- `ContentVersionService` — tracks version snapshots and audit details.
- `AuditLogService` — records workflow actions to audit history.
- `HtmlContentSanitizer` — sanitizes HTML input to prevent script injection.
- `HandbookImportService` — content import helper.
- `ContentImageService` — image upload and metadata helpers.

### 4.5 Identity and startup configuration
- `Startup.cs` — OWIN startup class registers identity managers and cookie authentication.
- `App_Start/IdentityConfig.cs` — custom `ApplicationUserManager` and `ApplicationSignInManager` creation.
- `App_Start/RouteConfig.cs`, `FilterConfig.cs`, `BundleConfig.cs` — MVC setup.
- `App_Start/DatabaseBootstrapper.cs` — database initialization logic.

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

1. Recreate the legacy `.NET Framework 4.8` web application structure in `src/ContentPublishing.Web`.
2. Implement the current controllers and service classes listed above.
3. Use ASP.NET Identity 2.2.4 with OWIN cookie authentication and the same registration/login profile flow.
4. Use Entity Framework 6 for the data context and the same DbSet mappings and table names.
5. Preserve the current role names and content workflow statuses.
6. Implement the email notification patterns in `WorkflowNotificationService`.
7. Implement content editing, review, and publish flows exactly as the existing project does.
8. Ensure the database schema includes tables for `Content`, `Chapter`, `Review`, `ContentReviewerAssignment`, `AuditLog`, `ContentImage`, `ContentVersion`, and standard AspNet Identity tables.

## 7. Build and run instructions for the rebuilt app

- Open `ContentPublishingSystem.sln` in Visual Studio 2022 or newer.
- Restore NuGet packages.
- Build the solution.
- Run the web project using IIS Express or local IIS.
- Ensure `ContentPublishingDb` is accessible using the configured connection string from `Web.config`.

## 8. Notes for the new agent

- Do not migrate to .NET 8 in this blueprint; this file is specifically for the current legacy system.
- Ignore any existing `ContentPublishing.Web.Net8` plans unless the system later requests a separate migration branch.
- Focus on rebuilding the existing behavior and workflow, not on modernizing the platform.
- Preserve the current database naming and legacy Identity configuration.

## 9. Key files to rebuild

- `src/ContentPublishing.Web/ContentPublishing.Web.csproj`
- `src/ContentPublishing.Web/Startup.cs`
- `src/ContentPublishing.Web/App_Start/IdentityConfig.cs`
- `src/ContentPublishing.Web/Models/ApplicationDbContext.cs`
- `src/ContentPublishing.Web/Models/ApplicationUser.cs`
- `src/ContentPublishing.Web/Models/ContentEntity.cs`
- `src/ContentPublishing.Web/Models/ReviewEntity.cs`
- `src/ContentPublishing.Web/Controllers/AccountController.cs`
- `src/ContentPublishing.Web/Controllers/ContentController.cs`
- `src/ContentPublishing.Web/Controllers/ReviewController.cs`
- `src/ContentPublishing.Web/Controllers/AdminController.cs`
- `src/ContentPublishing.Web/Services/WorkflowNotificationService.cs`
- `src/ContentPublishing.Web/Services/PublishingService.cs`
- `src/ContentPublishing.Web/Services/ContentVersionService.cs`
- `src/ContentPublishing.Web/Services/AuditLogService.cs`

## 10. Validation checklist

After rebuilding, verify:
- Login, registration, and email confirmation flows work.
- Authors can edit and save content metadata.
- Reviewers can see pending reviews and approve/reject content.
- Administrators can assign reviewers, publish content, and schedule publishing.
- Audit logs and version snapshots are recorded.
- The site authenticates with the legacy `AspNetUsers` identity tables.

---

This blueprint is intentionally focused on the current legacy application in `src/ContentPublishing.Web` and the exact implementation constructs used by that system.