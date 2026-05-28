using System.Text.RegularExpressions;

namespace ContentPublishing.Web.Services
{
    public static class HtmlContentSanitizer
    {
        private static readonly Regex ScriptTagRegex = new Regex("<script\\b[^<]*(?:(?!<\\/script>)<[^<]*)*<\\/script>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex JsHrefRegex = new Regex("(href|src)\\s*=\\s*([\"'])\\s*javascript:[^\"']*\\2", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string StripScripts(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            var withoutScripts = ScriptTagRegex.Replace(input, string.Empty);
            return JsHrefRegex.Replace(withoutScripts, string.Empty);
        }
    }
}