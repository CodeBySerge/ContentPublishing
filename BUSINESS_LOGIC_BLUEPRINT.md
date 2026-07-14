# Business Logic Blueprint â€” One Health Handbook

This document describes the implementation blueprint for the complete application business logic in the .NET 8 migration. It focuses on how the login, registration, user roles, content publication workflow, and security/identity flows should be structured. It is a detailed implementation spec, not full source code.

## 1. Architecture Overview

### 1.1 Layered design
- `OneHealthHandbook.Domain`
  - Domain entities, enums, value objects, and repository interfaces.
- `OneHealthHandbook.Application`
  - Application services, commands/queries, DTOs/view models, business rules, validation.
- `OneHealthHandbook.Infrastructure`
  - EF Core implementation, data repositories, email and storage adapters, `SuperClass` bridge.
- `OneHealthHandbook.Web.Net8`
  - MVC controllers, Razor pages/views, UI models, authentication wiring, routing, request validation.

### 1.2 Primary service categories
- Authentication and Identity management
- Role and permission management
- Handbook content lifecycle management
- User profile and account management
- Notification and email services
- Audit and event logging

## 2. Domain Model

### 2.1 Core entities
- `User`
  - IdentityUser-derived fields plus profile metadata (FullName, DisplayName, Department, RoleAssignments).
- `Role`
  - Role name and description (Author, Reviewer, Approver, Administrator).
- `HandbookDocument`
  - Title, Summary, ContentHtml, AuthorId, CreatedAt, UpdatedAt, Status, Version, RevisionNotes.
- `ReviewRequest`
  - DocumentId, ReviewerId, RequestedAt, CompletedAt, ReviewStatus, Comments.
- `ChangeHistory`
  - EntityName, EntityId, Action, ChangedBy, ChangedAt, Details.

### 2.2 Supporting value objects
- `DocumentStatus` enum
  - Draft, PendingReview, Approved, Rejected, Published, Archived.
- `Permission` enum
  - ViewContent, CreateContent, EditContent, SubmitForReview, ApproveContent, ManageUsers, ConfigureSystem.
- `LoginResult`/`RegistrationResult`
  - Result status, errors, redirect path, extra flags.

### 2.3 Relationships
- One `User` may author many `HandbookDocument` entries.
- One document can have multiple `ReviewRequest` records.
- Roles are assigned to users; permissions are derived from role membership.

## 3. Authentication and Registration

### 3.1 Registration flow
- Inputs: Email, Password, ConfirmPassword, FirstName, LastName, Department, Optional role request.
- Validation rules:
  - Email format and domain restrictions if required.
  - Password complexity: digits, upper/lowercase, length, special chars.
  - Unique email check.
- Business logic:
  - Create a new Identity user and set email confirmed status if using email confirmation.
  - Store profile metadata in the associated user claims or extended user table.
  - Assign default role `Author` or `Reviewer` depending on business policy.
  - Send a welcome/verification email via the notification service.
- Output:
  - Registration result object with success flag and error messages.

### 3.2 Login flow
- Inputs: Username/email, Password, RememberMe flag.
- Validation rules:
  - Account exists.
  - Password matches via Identity sign-in manager.
  - Account lockout or email confirmation conditions.
- Business rules:
  - If account is locked or not confirmed, return a clear error state.
  - Support multi-factor or external login later if required.
- Session behavior:
  - Use cookie authentication via ASP.NET Core Identity.
  - Configure cookie expiration and sliding expiration according to policy.

### 3.3 Password management
- Reset request flow
  - Input: Email.
  - Generate identity token and send reset link via email.
- Reset confirmation flow
  - Inputs: Email, Token, NewPassword, ConfirmPassword.
  - Validate token and update password.
  - Record the password reset event in audit logs.

### 3.4 Account management
- View profile details.
- Edit personal metadata.
- Change password.
- Optionally, manage two-factor authentication or external login providers later.

## 4. Role and Authorization Model

### 4.1 Roles
- `Administrator`
  - Full access: user management, role assignments, system settings, all content actions.
- `Author`
  - Create/edit own handbook content, submit for review, view own drafts and review history.
- `Reviewer`
  - View pending review content, comment, approve/reject submissions, request changes.
- `Approver`
  - Final approval and publishing authority.
- Additional custom roles
  - `Publisher`, `Auditor`, `ContentManager` can be added as needed.

### 4.2 Permissions
- Map each role to a set of permissions.
- Example permission groups:
  - Content: Create, Edit, Delete, SubmitForReview, Approve, Publish.
  - User: ManageUsers, AssignRoles.
  - System: ConfigureSettings, ViewAudit.
- Use a permission service to evaluate whether a user can perform an action.

### 4.3 Authorization enforcement
- At controller/action layer:
  - Apply `[Authorize]` attributes and policy checks.
  - Use custom policies for actions like `RequireAuthorRole`, `RequireReviewerRole`, `RequireAdministratorRole`.
- Within application services:
  - Validate permissions before executing commands.
  - Throw authorization exceptions when checks fail.

### 4.4 Role management logic
- Admin can create/edit/delete roles and assign permissions.
- Admin can assign/revoke user roles.
- Business rule: a user may have multiple roles, but permissions must be composed carefully.
- Role changes should be audited.

## 5. Content Lifecycle and View Content Logic

### 5.1 Content creation
- Inputs: Title, Summary, BodyHtml, Tags, IntendedAudience.
- Validation rules:
  - Title required and unique within environment.
  - BodyHtml required; sanitize HTML before storing.
- Business logic:
  - Create a new `HandbookDocument` with `Status = Draft`.
  - Store author identifier and creation metadata.
  - Optionally save an initial version record.

### 5.2 Content editing
- Inputs: DocumentId, updated Title, Summary, BodyHtml, Tags.
- Permissions:
  - Authors can edit their own drafts.
  - Administrators may edit any content.
- Business logic:
  - Apply edits and update `UpdatedAt`.
  - Preserve version history by recording previous content and a revision note.

### 5.3 Submit for review
- Inputs: DocumentId, optional reviewer comments.
- Business logic:
  - Validate user can submit (ownership or admin rights).
  - Change status to `PendingReview`.
  - Create a `ReviewRequest` record.
  - Notify assigned reviewers or reviewer role.

### 5.4 Review and approval
- Reviewer actions:
  - View all pending review documents.
  - Provide comments and choose `Approve`, `Reject`, or `RequestChanges`.
- Business logic:
  - If approved by a reviewer, update review request and optionally notify approver.
  - If rejected or changes requested, set document status back to `Draft` or `Rejected` and record comments.
  - If final approval is required, transition to `Approved` and notify publishing authority.

### 5.5 Publishing content
- Inputs: DocumentId, publish notes.
- Permissions:
  - Approver or Administrator only.
- Business logic:
  - Set status to `Published` and record `PublishedAt`.
  - Optionally create a public/page-ready representation.
  - Notify stakeholders that content is live.

### 5.6 Viewing content
- Public view for published documents.
- Authenticated view for drafts and pending review content.
- Role-based content listings:
  - Authors see own drafts and submissions.
  - Reviewers see pending review documents.
  - Administrators see all documents.
- Search/filter logic:
  - Filter by status, author, date range, tags, department.

### 5.7 Content history and versioning
- Maintain `ChangeHistory` records for create/edit/submit/approve actions.
- Provide a document revision history page with timestamps, actors, and comments.
- Allow rollback to a previous version if required.

## 6. Account and User Management Journeys

### 6.1 User registration and onboarding
- New user registers and receives a confirmation email.
- On first login, the system may show a welcome page and role-specific dashboard.
- If user requests special permissions, route approval to an administrator.

### 6.2 User login and session
- Login page authenticates using Identity.
- After login, redirect to:
  - Author dashboard for authors.
  - Reviewer queue for reviewers.
  - Admin console for administrators.
- Session expiration and sign-out logic should be explicit.

### 6.3 User profile management
- Allow users to update personal details, change password, and view activity history.
- Show assigned roles and permissions.
- Admin view can manage user role assignments and account status.

### 6.4 Role request / approval workflow
- If user requests additional roles, create a `RoleRequest` record.
- Admin can review and approve/reject role requests.
- Notify user of the outcome.

## 7. Notifications, Email, and Audit

### 7.1 Notification service contract
- Define a service interface for email notifications, e.g. `INotificationService`.
- Responsibilities:
  - Send registration confirmation.
  - Send password reset links.
  - Send review assignment and approval/rejection notifications.
  - Send publication announcements.

### 7.2 Email templates and content
- Use template-driven email generation.
- Include secure links for actions such as email confirmation, password reset, and review.
- Localize messages or store strings in resource files if needed.

### 7.3 Audit logging
- Record key events:
  - User login, registration, password reset.
  - Role assignment changes.
  - Document creation, edits, review actions, approvals, publishing.
- Use a simple audit entity or integrate with `SuperClass` logging if available.
- Store actor, timestamp, action, and details.

## 8. SuperClass Integration Strategy

### 8.1 Integration surface
- Identify shared features in SuperClass:
  - Logging and diagnostics
  - Email sending
  - Encryption/crypto helpers
  - Export utilities
  - Common validation or user management helpers
- Decide which features are reused directly and which require a compatibility adapter.

### 8.2 Adapter pattern
- Create adapter services in `OneHealthHandbook.Infrastructure` to wrap `SuperClass` functionality.
- Example services:
  - `IEmailSender` -> `SuperClassEmailSender`
  - `ICryptoProvider` -> `SuperClassCryptoAdapter`
  - `ILogger` -> `SuperClassLoggerAdapter`
- Keep the application and web layers dependent on abstractions, not concrete SuperClass classes.

### 8.3 Compatibility considerations
- If `SuperClass` is net48-only, do not reference it directly from `OneHealthHandbook.Web.Net8`.
- Preferred path:
  - Retarget/port `SuperClass` to `netstandard2.0` or `net8.0`.
  - If not possible, isolate Windows-only features behind a service boundary.
- Document the set of SuperClass APIs that must be implemented or wrapped to preserve current behavior.

## 9. Implementation Breakdown by Module

### 9.1 `OneHealthHandbook.Domain`
- Entities:
  - `UserProfile`, `HandbookDocument`, `ReviewRequest`, `RoleAssignment`, `ChangeHistory`, `RoleRequest`.
- Interfaces:
  - `IUserRepository`, `IDocumentRepository`, `IReviewRepository`, `IAuditRepository`.
- Domain services:
  - `IDocumentApprovalRules`, `IUserAuthorizationRules`.

### 9.2 `OneHealthHandbook.Application`
- Services:
  - `IAuthenticationService`
  - `IRegistrationService`
  - `IRoleManagementService`
  - `IContentService`
  - `IReviewService`
  - `INotificationService`
  - `IUserProfileService`
- DTOs/ViewModels:
  - `RegisterUserModel`, `LoginModel`, `ContentDraftModel`, `ReviewRequestModel`, `DashboardSummaryModel`.
- Commands/Queries:
  - `CreateContentCommand`, `SubmitForReviewCommand`, `ApproveContentCommand`, `AssignRoleCommand`, `GetPendingReviewQuery`.
- Business rules:
  - `ValidateUserCanEditDocument`
  - `ValidateReviewTransition`
  - `ValidateRoleAssignment`
  - `ValidateContentPublish`

### 9.3 `OneHealthHandbook.Infrastructure`
- EF Core DbContext and repository implementations:
  - `ApplicationDbContext`
  - `UserRepository`, `DocumentRepository`, `ReviewRepository`, `AuditRepository`
- `SuperClass` adapters:
  - `EmailSenderAdapter`, `LoggerAdapter`, `CryptoAdapter`.
- Identity and persistence configuration.

### 9.4 `OneHealthHandbook.Web.Net8`
- Controllers:
  - `AccountController`, `HomeController`, `DashboardController`, `ContentController`, `ReviewController`, `AdminController`.
- Models/ViewModels:
  - `LoginViewModel`, `RegisterViewModel`, `ContentEditViewModel`, `ReviewViewModel`, `RoleAssignmentViewModel`.
- Views:
  - Login, Register, ResetPassword, Profile, ContentList, ContentEdit, ContentReview, Dashboard, RoleManagement.
- Middleware and startup:
  - Identity configuration, authorization policies, error handling, static files.

## 10. Journey Scenarios

### 10.1 New user registration and first login
1. User opens registration view.
2. System validates input and creates Identity user.
3. Default role is assigned.
4. Email confirmation is sent.
5. User signs in and lands on the role-specific dashboard.

### 10.2 Author creates and submits content
1. Author opens create content page.
2. Author saves draft.
3. Author edits draft until ready.
4. Author submits for review.
5. System creates review request and notifies reviewers.

### 10.3 Reviewer reviews submitted content
1. Reviewer views pending review queue.
2. Reviewer opens document and adds comments.
3. Reviewer approves, rejects, or requests changes.
4. System updates document status and notifies the author.

### 10.4 Approver publishes content
1. Approver opens approved items queue.
2. Approver publishes the document.
3. System marks the document as published and records audit history.
4. Stakeholders receive publication notification.

### 10.5 Administrator manages users and roles
1. Administrator views user list and role assignments.
2. Administrator edits a user and adds or removes roles.
3. System validates the change and saves an audit record.
4. User receives notification when roles are changed.

## 11. Validation and Error Handling

### 11.1 Input validation
- Use validation rules on view models and DTOs.
- Validate required fields, string lengths, HTML sanitization, and business constraints.
- Return clear error messages to the UI.

### 11.2 Business rule enforcement
- Perform rule checks in application services before persistence.
- Use result objects or exceptions to communicate failures.
- Log rejected operations for diagnostics.

### 11.3 Exception handling
- Use centralized error handling in the web project.
- Map known business exceptions to user-friendly messages.
- Capture unexpected exceptions and log details securely.

## 12. Test Strategy

### 12.1 Unit tests
- Test application services for:
  - Registration and login logic.
  - Role assignment and permission checks.
  - Content lifecycle transitions.
  - Review decision rules.
- Mock repository and notification interfaces.

### 12.2 Integration tests
- Validate identity flows with in-memory or test database.
- Verify content creation, submission, review, and approval sequences.
- Test authorization policies and role-based access.

### 12.3 Regression coverage
- Cover any legacy behavior required by existing users, especially if the current database schema or business rules must be preserved.

## 13. Implementation Notes for an Agent

- Start with the identity and role management service first; it is the foundation for all journeys.
- Keep the domain layer independent of external frameworks.
- Keep the application layer focused on use-case orchestration and validation.
- Keep the infrastructure layer responsible for persistence and external integrations.
- Implement small, runnable slices: first register/login, then author dashboard and content creation, then review/publish.
- Expand the `SuperClass` integration only after the core domain is wired, so adapters can be built cleanly.

## 14. Recommended File Structure for Implementation

- `src/OneHealthHandbook.Domain/Entities`
- `src/OneHealthHandbook.Domain/Enums`
- `src/OneHealthHandbook.Domain/Interfaces`
- `src/OneHealthHandbook.Application/Services`
- `src/OneHealthHandbook.Application/Commands`
- `src/OneHealthHandbook.Application/Queries`
- `src/OneHealthHandbook.Application/Models`
- `src/OneHealthHandbook.Infrastructure/Data`
- `src/OneHealthHandbook.Infrastructure/Services`
- `src/OneHealthHandbook.Web.Net8/Controllers`
- `src/OneHealthHandbook.Web.Net8/Models`
- `src/OneHealthHandbook.Web.Net8/Views`
- `src/OneHealthHandbook.Web.Net8/Services` (if specific web-layer helpers are needed)

## 15. Summary

This blueprint provides the complete implementation logic for the One Health Handbook business flows:
- user registration and authentication,
- role-based authorization,
- content creation, review, approval, and publishing,
- SuperClass integration and compatibility,
- data persistence, audit, and notification patterns.

Use this document as a step-by-step guide when writing the actual code. The focus is on proper separation of concerns, reliable authorization, and a predictable content lifecycle.

