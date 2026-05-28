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
            sb.Append("<article style=\"max-width:760px;margin:0 auto;font-family:Georgia,'Times New Roman',serif;line-height:1.6;color:#1f2937;\">");
            sb.Append("<h1 style=\"font-size:2rem;margin:0 0 1rem 0;font-weight:700;\">")
                .Append(HttpUtility.HtmlEncode(entry.Title))
                .Append("</h1>");

            var headingDescriptions = ExtractHeadingDescriptions(rawSection);

            if (entry.ContentItems.Any())
            {
                sb.Append("<div style=\"margin-top:0.5rem;\">");
                foreach (var item in entry.ContentItems)
                {
                    sb.Append("<section style=\"margin:0 0 1.25rem 0;\">");
                    sb.Append("<h2 style=\"font-size:1.25rem;margin:0 0 0.4rem 0;font-weight:600;\">")
                        .Append(HttpUtility.HtmlEncode(item))
                        .Append("</h2>");

                    if (headingDescriptions.TryGetValue(item, out var description) && !string.IsNullOrWhiteSpace(description))
                    {
                        sb.Append("<p style=\"margin:0;\">")
                            .Append(HttpUtility.HtmlEncode(description))
                            .Append("</p>");
                    }
                    else
                    {
                        sb.Append("<p style=\"margin:0;color:#6b7280;\">Content section available for editorial updates.</p>");
                    }

                    sb.Append("</section>");
                }

                sb.Append("</div>");
            }

            if (!entry.ContentItems.Any() && !string.IsNullOrWhiteSpace(rawSection))
            {
                sb.Append("<p style=\"margin:0;\">Content imported and ready for review.</p>");
            }

            sb.Append("</article>");
            return sb.ToString();
        }

        private static Dictionary<string, string> ExtractHeadingDescriptions(string rawSection)
        {
            var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rawSection))
            {
                return result;
            }

            var lines = rawSection.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string currentHeading = null;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                var headingMatch = Regex.Match(line, "^heading:\\s*\"([^\"]+)\"");
                if (headingMatch.Success)
                {
                    currentHeading = headingMatch.Groups[1].Value;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(currentHeading) || !line.StartsWith("description:"))
                {
                    continue;
                }

                var description = ReadQuotedLiteral(lines, ref i, line.IndexOf("description:", StringComparison.Ordinal));
                if (!string.IsNullOrWhiteSpace(description) && !result.ContainsKey(currentHeading))
                {
                    result[currentHeading] = description;
                }
            }

            return result;
        }

        private static string ReadQuotedLiteral(string[] lines, ref int index, int descriptionTokenIndex)
        {
            var builder = new StringBuilder();
            var started = false;
            var escaped = false;

            for (var i = index; i < lines.Length; i++)
            {
                var text = lines[i];
                var startAt = i == index ? descriptionTokenIndex : 0;

                for (var p = startAt; p < text.Length; p++)
                {
                    var ch = text[p];

                    if (!started)
                    {
                        if (ch == '"')
                        {
                            started = true;
                        }
                        continue;
                    }

                    if (escaped)
                    {
                        builder.Append('\\');
                        builder.Append(ch);
                        escaped = false;
                        continue;
                    }

                    if (ch == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (ch == '"')
                    {
                        index = i;
                        return DecodeEscapedContent(builder.ToString());
                    }

                    builder.Append(ch);
                }

                if (started)
                {
                    builder.Append('\n');
                }
            }

            index = lines.Length - 1;
            return DecodeEscapedContent(builder.ToString());
        }

        private static string DecodeEscapedContent(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var normalized = value.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\"", "\"");
            return normalized.Trim();
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