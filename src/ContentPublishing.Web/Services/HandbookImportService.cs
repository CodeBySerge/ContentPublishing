using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using ContentPublishing.Web.Models;

namespace ContentPublishing.Web.Services
{
    public static class HandbookImportService
    {
        private const string ImportedAuthorId = "SYSTEM_IMPORT";
        private const string ImportedTitlePrefix = "Chapter ";

        public static void EnsureImported(ApplicationDbContext db)
        {
            var handbookPath = ResolveHandbookPath();
            if (string.IsNullOrWhiteSpace(handbookPath) || !File.Exists(handbookPath))
            {
                return;
            }

            var source = File.ReadAllText(handbookPath);
            var tocEntries = ParseTableOfContents(source);
            if (!tocEntries.Any())
            {
                return;
            }

            var contentsBlock = ExtractBlock(source, "const CONTENTS", "const PRICE_LIST");
            var priceListBlock = ExtractBlock(source, "const PRICE_LIST", "export {");
            var now = DateTime.UtcNow;

            var hasImportedRows = db.Contents.Any(c => c.AuthorId == ImportedAuthorId);
            if (hasImportedRows)
            {
                NormalizeAndRefreshImportedRows(db, tocEntries, contentsBlock, priceListBlock, now);
                return;
            }

            foreach (var entry in tocEntries.OrderBy(e => e.Id))
            {
                var contentId = Guid.NewGuid();
                var chapterRawBlock = entry.Id == 31
                    ? priceListBlock
                    : ExtractTopLevelContentChapter(contentsBlock, entry.Id);

                var contentTitle = Truncate(ImportedTitlePrefix + entry.Id + ": " + entry.Title, 250);

                db.Contents.Add(new ContentEntity
                {
                    ContentId = contentId,
                    Title = contentTitle,
                    Description = "Imported from handbook source. Edit chapter and submit for approval.",
                    Status = ContentStatuses.Draft,
                    AuthorId = ImportedAuthorId,
                    CreatedDate = now,
                    LastModifiedDate = now
                });

                db.Chapters.Add(new ChapterEntity
                {
                    ChapterId = Guid.NewGuid(),
                    ContentId = contentId,
                    ChapterTitle = Truncate(entry.Title, 250),
                    ChapterBody = BuildChapterBody(entry, chapterRawBlock),
                    ChapterOrder = 1,
                    CreatedDate = now,
                    LastModifiedDate = now,
                    IsDeleted = false
                });
            }

            db.SaveChanges();
        }

        private static void NormalizeAndRefreshImportedRows(ApplicationDbContext db, List<TocEntry> tocEntries, string contentsBlock, string priceListBlock, DateTime now)
        {
            var updated = false;
            var importedContents = db.Contents.Where(c => c.AuthorId == ImportedAuthorId).ToList();

            foreach (var content in importedContents)
            {
                var normalizedTitle = NormalizeTitle(content.Title);
                if (!string.Equals(normalizedTitle, content.Title, StringComparison.Ordinal))
                {
                    content.Title = Truncate(normalizedTitle, 250);
                    content.LastModifiedDate = DateTime.UtcNow;
                    updated = true;
                }
            }

            var chapterLookup = db.Chapters
                .Where(ch => !ch.IsDeleted)
                .GroupBy(ch => ch.ContentId)
                .ToDictionary(g => g.Key, g => g.OrderBy(ch => ch.ChapterOrder).FirstOrDefault());

            foreach (var entry in tocEntries)
            {
                var matchingContent = importedContents
                    .FirstOrDefault(c => ExtractChapterNumberFromTitle(c.Title) == entry.Id);

                if (matchingContent == null)
                {
                    continue;
                }

                var chapterRawBlock = entry.Id == 31
                    ? priceListBlock
                    : ExtractTopLevelContentChapter(contentsBlock, entry.Id);
                var refreshedBody = BuildChapterBody(entry, chapterRawBlock);

                if (!chapterLookup.TryGetValue(matchingContent.ContentId, out var chapter) || chapter == null)
                {
                    db.Chapters.Add(new ChapterEntity
                    {
                        ChapterId = Guid.NewGuid(),
                        ContentId = matchingContent.ContentId,
                        ChapterTitle = Truncate(entry.Title, 250),
                        ChapterBody = refreshedBody,
                        ChapterOrder = 1,
                        CreatedDate = now,
                        LastModifiedDate = now,
                        IsDeleted = false
                    });
                    updated = true;
                    continue;
                }

                if (!string.Equals(chapter.ChapterBody, refreshedBody, StringComparison.Ordinal))
                {
                    chapter.ChapterBody = refreshedBody;
                    chapter.ChapterTitle = Truncate(entry.Title, 250);
                    chapter.LastModifiedDate = now;
                    updated = true;
                }
            }

            if (updated)
            {
                db.SaveChanges();
            }
        }

        private static string NormalizeTitle(string currentTitle)
        {
            if (string.IsNullOrWhiteSpace(currentTitle))
            {
                return currentTitle;
            }

            var title = currentTitle.Trim();
            if (title.StartsWith("Handbook Chapter ", StringComparison.OrdinalIgnoreCase))
            {
                title = "Chapter " + title.Substring("Handbook Chapter ".Length);
            }

            if (title.StartsWith("Handbook ", StringComparison.OrdinalIgnoreCase))
            {
                title = title.Substring("Handbook ".Length);
            }

            return title;
        }

        private static string ResolveHandbookPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Handbook", "Handbook.txt")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "Handbook", "Handbook.txt")),
                Path.GetFullPath(Path.Combine(baseDir, "Handbook", "Handbook.txt"))
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static List<TocEntry> ParseTableOfContents(string source)
        {
            var tocBlock = ExtractBlock(source, "const TABLE_OF_CONTENTS", "const CONTENTS");
            var result = new List<TocEntry>();

            if (string.IsNullOrWhiteSpace(tocBlock))
            {
                return result;
            }

            TocEntry current = null;
            var lines = tocBlock.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var insideContentArray = false;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("//"))
                {
                    continue;
                }

                var idMatch = Regex.Match(line, "^id:\\s*(\\d+),");
                if (idMatch.Success)
                {
                    current = new TocEntry
                    {
                        Id = int.Parse(idMatch.Groups[1].Value)
                    };
                    result.Add(current);
                    insideContentArray = false;
                    continue;
                }

                if (current == null)
                {
                    continue;
                }

                var titleMatch = Regex.Match(line, "^title:\\s*\"([^\"]+)\",");
                if (titleMatch.Success)
                {
                    current.Title = titleMatch.Groups[1].Value;
                    continue;
                }

                if (line.StartsWith("content:"))
                {
                    insideContentArray = true;
                    continue;
                }

                if (insideContentArray && line.StartsWith("],"))
                {
                    insideContentArray = false;
                    continue;
                }

                if (!insideContentArray)
                {
                    continue;
                }

                var contentItemMatch = Regex.Match(line, "\"([^\"]+)\"");
                if (contentItemMatch.Success)
                {
                    current.ContentItems.Add(contentItemMatch.Groups[1].Value);
                }
            }

            result = result.Where(r => r.Id > 0 && !string.IsNullOrWhiteSpace(r.Title)).ToList();

            return result;
        }

        private static string ExtractBlock(string source, string startToken, string endToken)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            var startIndex = source.IndexOf(startToken, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return string.Empty;
            }

            var endIndex = source.IndexOf(endToken, startIndex, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                return source.Substring(startIndex);
            }

            return source.Substring(startIndex, endIndex - startIndex);
        }

        private static string ExtractTopLevelContentChapter(string contentsBlock, int chapterId)
        {
            if (string.IsNullOrWhiteSpace(contentsBlock))
            {
                return string.Empty;
            }

            var pattern = "(?ms)^\\s{6}id:\\s*" + chapterId + ",.*?(?=^\\s{6}id:\\s*\\d+,|^\\s{2}\\];)";
            var match = Regex.Match(contentsBlock, pattern, RegexOptions.Multiline | RegexOptions.Singleline);
            return match.Success ? match.Value.Trim() : string.Empty;
        }

        private static string BuildChapterBody(TocEntry entry, string rawSection)
        {
            var sb = new StringBuilder();
            sb.Append("<h3>").Append(HttpUtility.HtmlEncode(entry.Title)).Append("</h3>");

            if (entry.ContentItems.Any())
            {
                sb.Append("<p>Original chapter structure:</p><ul>");
                foreach (var item in entry.ContentItems)
                {
                    sb.Append("<li>").Append(HttpUtility.HtmlEncode(item)).Append("</li>");
                }

                sb.Append("</ul>");
            }

            if (!string.IsNullOrWhiteSpace(rawSection))
            {
                sb.Append("<p>Original source block:</p><pre>")
                    .Append(HttpUtility.HtmlEncode(rawSection))
                    .Append("</pre>");
            }

            return sb.ToString();
        }

        private static int ExtractChapterNumberFromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return -1;
            }

            var match = Regex.Match(title, "\\b(\\d+)\\b");
            if (!match.Success)
            {
                return -1;
            }

            return int.TryParse(match.Groups[1].Value, out var value) ? value : -1;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        private sealed class TocEntry
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public List<string> ContentItems { get; set; } = new List<string>();
        }
    }
}