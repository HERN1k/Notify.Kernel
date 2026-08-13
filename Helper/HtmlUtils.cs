using System.Net;
using System.Text.RegularExpressions;

namespace Notify.Helper
{
    public static partial class HtmlUtils
    {
        [GeneratedRegex(@"<(script|style)[^>]*?>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex ScriptStyleRegex();

        [GeneratedRegex(@"<br\s*/?>|</p>|</div>|</li>", RegexOptions.IgnoreCase)]
        private static partial Regex NewLineTagsRegex();

        [GeneratedRegex(@"<[^>]+>")]
        private static partial Regex HtmlTagsRegex();

        [GeneratedRegex(@"(\r?\n\s*){3,}")]
        private static partial Regex MultiNewLinesRegex();

        public static string HTMLToPlainText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            string text = ScriptStyleRegex().Replace(html, string.Empty);

            text = NewLineTagsRegex().Replace(text, "\n");

            text = HtmlTagsRegex().Replace(text, string.Empty);

            text = WebUtility.HtmlDecode(text);

            text = MultiNewLinesRegex().Replace(text, "\n\n");

            return text.Trim();
        }
    }
}