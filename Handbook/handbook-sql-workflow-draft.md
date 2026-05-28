# Handbook SQL + Workflow Draft (Updated Requirement)

## Goal

Store the provided handbook in SQL Server while preserving its original structure, then allow:

1. Author selects a chapter and edits it.
2. Reviewer reviews that chapter.
3. Approved chapter is published.
4. Structure and hierarchy remain intact.

## Recommended Data Strategy

Use a hybrid model:

1. Relational tables for workflow, ownership, chapter status, and querying.
2. JSON columns for exact chapter payload to preserve nested structure.

This avoids losing fidelity from the source while still enabling chapter-level workflow.

## Proposed Tables

### 1) HandbookDocument

- HandbookDocumentId (uniqueidentifier, PK)
- Title (nvarchar(250))
- SourceVersion (nvarchar(50))
- Status (nvarchar(50)) -- Draft, Active, Archived
- CreatedBy (uniqueidentifier)
- CreatedDateUtc (datetime2)
- LastModifiedDateUtc (datetime2)

### 2) HandbookChapter

- HandbookChapterId (uniqueidentifier, PK)
- HandbookDocumentId (uniqueidentifier, FK)
- ChapterNumber (int) -- id from source (1,7,8,...)
- ChapterTitle (nvarchar(500))
- ChapterOrder (int)
- TocContentJson (nvarchar(max)) -- original TOC content array for chapter
- CurrentVersionNumber (int)
- CurrentStatus (nvarchar(50)) -- Draft, InReview, ChangesRequested, Approved, Published
- IsLockedForReview (bit)
- CreatedDateUtc (datetime2)
- LastModifiedDateUtc (datetime2)

Unique index suggestion:
- UX_HandbookChapter_Doc_ChapterNumber on (HandbookDocumentId, ChapterNumber)

### 3) HandbookChapterVersion

- HandbookChapterVersionId (uniqueidentifier, PK)
- HandbookChapterId (uniqueidentifier, FK)
- VersionNumber (int)
- ChapterPayloadJson (nvarchar(max)) -- exact chapter object from contents[]
- NormalizedText (nvarchar(max), nullable) -- optional search text
- ChangeSummary (nvarchar(1000), nullable)
- EditedByUserId (uniqueidentifier)
- EditedDateUtc (datetime2)
- WorkflowStatus (nvarchar(50)) -- Draft, SubmittedForReview, Reviewed, Published

Unique index suggestion:
- UX_ChapterVersion_Chapter_Version on (HandbookChapterId, VersionNumber)

### 4) HandbookChapterReview

- HandbookChapterReviewId (uniqueidentifier, PK)
- HandbookChapterVersionId (uniqueidentifier, FK)
- ReviewerId (uniqueidentifier)
- Decision (nvarchar(50)) -- Pending, Approved, Rejected
- Comments (nvarchar(max), nullable)
- ReviewedDateUtc (datetime2, nullable)
- CreatedDateUtc (datetime2)

### 5) HandbookChapterPublication

- HandbookChapterPublicationId (uniqueidentifier, PK)
- HandbookChapterId (uniqueidentifier, FK)
- PublishedVersionNumber (int)
- PublishedByUserId (uniqueidentifier)
- PublishedDateUtc (datetime2)
- PublicationNotes (nvarchar(1000), nullable)

## Chapter Editing Workflow

1. Import handbook:
- Create one HandbookDocument row.
- Create HandbookChapter rows from TABLE_OF_CONTENTS ids/titles.
- For each chapter in CONTENTS, create HandbookChapterVersion v1 with ChapterPayloadJson.
- For TOC-only chapter like 31 (price list), create a chapter whose payload references PRICE_LIST.

2. Author selects chapter:
- Load HandbookChapter + latest draft/published HandbookChapterVersion.
- Render JSON payload into editor.
- Save updates as a new HandbookChapterVersion (increment VersionNumber).

3. Submit for review:
- Set chapter status to InReview.
- Create HandbookChapterReview row(s) for assigned reviewer(s).
- Lock chapter for edits or allow branch edits based on policy.

4. Reviewer decision:
- Approve: status -> Approved.
- Reject: status -> ChangesRequested with comments.

5. Publish chapter:
- Copy approved version number to HandbookChapter.CurrentVersionNumber.
- Set chapter status -> Published.
- Insert HandbookChapterPublication audit row.

## Preserve Original Structure

Rules to guarantee structure fidelity:

1. Never flatten ChapterPayloadJson at save time.
2. Persist unknown keys untouched.
3. Preserve array ordering exactly as source.
4. Preserve string values verbatim (including line breaks and spacing in values).
5. Use chapter version snapshots for rollback.

## Mapping from Existing Entities

Current entities are content-centric:

- ContentItem
- Chapter
- Review

Recommended transition:

1. Keep existing tables for compatibility.
2. Add new handbook-specific tables above.
3. Introduce adapters/services that map chapter workflow to new tables.
4. Gradually migrate UI endpoints from Chapter.ChapterBody to HandbookChapterVersion.ChapterPayloadJson.

## SQL Sketch

```sql
CREATE TABLE HandbookDocument (
  HandbookDocumentId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
  Title NVARCHAR(250) NOT NULL,
  SourceVersion NVARCHAR(50) NULL,
  Status NVARCHAR(50) NOT NULL,
  CreatedBy UNIQUEIDENTIFIER NOT NULL,
  CreatedDateUtc DATETIME2 NOT NULL,
  LastModifiedDateUtc DATETIME2 NOT NULL
);

CREATE TABLE HandbookChapter (
  HandbookChapterId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
  HandbookDocumentId UNIQUEIDENTIFIER NOT NULL,
  ChapterNumber INT NOT NULL,
  ChapterTitle NVARCHAR(500) NOT NULL,
  ChapterOrder INT NOT NULL,
  TocContentJson NVARCHAR(MAX) NULL,
  CurrentVersionNumber INT NULL,
  CurrentStatus NVARCHAR(50) NOT NULL,
  IsLockedForReview BIT NOT NULL DEFAULT 0,
  CreatedDateUtc DATETIME2 NOT NULL,
  LastModifiedDateUtc DATETIME2 NOT NULL,
  CONSTRAINT FK_HandbookChapter_Document FOREIGN KEY (HandbookDocumentId)
    REFERENCES HandbookDocument(HandbookDocumentId)
);

CREATE UNIQUE INDEX UX_HandbookChapter_Doc_ChapterNumber
ON HandbookChapter(HandbookDocumentId, ChapterNumber);

CREATE TABLE HandbookChapterVersion (
  HandbookChapterVersionId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
  HandbookChapterId UNIQUEIDENTIFIER NOT NULL,
  VersionNumber INT NOT NULL,
  ChapterPayloadJson NVARCHAR(MAX) NOT NULL,
  NormalizedText NVARCHAR(MAX) NULL,
  ChangeSummary NVARCHAR(1000) NULL,
  EditedByUserId UNIQUEIDENTIFIER NOT NULL,
  EditedDateUtc DATETIME2 NOT NULL,
  WorkflowStatus NVARCHAR(50) NOT NULL,
  CONSTRAINT FK_HandbookChapterVersion_Chapter FOREIGN KEY (HandbookChapterId)
    REFERENCES HandbookChapter(HandbookChapterId)
);

CREATE UNIQUE INDEX UX_ChapterVersion_Chapter_Version
ON HandbookChapterVersion(HandbookChapterId, VersionNumber);
```

## Notes for Your Current File

The source file is TypeScript-like data, not strict JSON. For import:

1. Convert to strict JSON payload with this envelope:
- tableOfContents
- contents
- priceList
2. Validate against handbook.schema.json before inserting.
3. Insert by chapter id to keep chapter boundaries and order.
