using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace NINA.Plugin.ExoPlanets.View {

    /// <summary>
    /// Attached property that renders a simple HTML string into a TextBlock's Inlines,
    /// supporting &lt;a href="…"&gt;, &lt;br/&gt;, &lt;b&gt;, &lt;i&gt; and HTML entities.
    /// Usage:  alt:HtmlTextBlock.Html="{Binding SomeHtmlString}"
    /// </summary>
    public static class HtmlTextBlock {

        public static readonly DependencyProperty HtmlProperty =
            DependencyProperty.RegisterAttached(
                "Html",
                typeof(string),
                typeof(HtmlTextBlock),
                new PropertyMetadata(null, OnHtmlChanged));

        public static string GetHtml(DependencyObject obj) => (string)obj.GetValue(HtmlProperty);
        public static void SetHtml(DependencyObject obj, string value) => obj.SetValue(HtmlProperty, value);

        private static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is not TextBlock tb) return;
            tb.Inlines.Clear();
            var html = e.NewValue as string;
            if (string.IsNullOrEmpty(html)) return;
            AddInlines(tb.Inlines, html);
        }

        private static void AddInlines(InlineCollection inlines, string html) {
            // Tokenise on tags we care about; everything else is plain text
            var pattern = @"(<a\s[^>]*href\s*=\s*[""'][^""']*[""'][^>]*>.*?</a>|<br\s*/?>|<b>.*?</b>|<i>.*?</i>)";
            var parts = Regex.Split(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (var part in parts) {
                if (string.IsNullOrEmpty(part)) continue;

                // <a href="…">text</a>
                var aMatch = Regex.Match(part, @"<a\s[^>]*href\s*=\s*[""']([^""']*)[""'][^>]*>(.*?)</a>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (aMatch.Success) {
                    var url = aMatch.Groups[1].Value.Trim();
                    var text = StripTags(aMatch.Groups[2].Value);
                    var link = new Hyperlink(new Run(string.IsNullOrEmpty(text) ? url : text));
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
                        link.NavigateUri = uri;
                    }
                    link.RequestNavigate += OnNavigate;
                    inlines.Add(link);
                    continue;
                }

                // <br> / <br/>
                if (Regex.IsMatch(part, @"<br\s*/?>", RegexOptions.IgnoreCase)) {
                    inlines.Add(new LineBreak());
                    continue;
                }

                // <b>…</b>
                var bMatch = Regex.Match(part, @"<b>(.*?)</b>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (bMatch.Success) {
                    inlines.Add(new Bold(new Run(DecodeEntities(StripTags(bMatch.Groups[1].Value)))));
                    continue;
                }

                // <i>…</i>
                var iMatch = Regex.Match(part, @"<i>(.*?)</i>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (iMatch.Success) {
                    inlines.Add(new Italic(new Run(DecodeEntities(StripTags(iMatch.Groups[1].Value)))));
                    continue;
                }

                // Plain text (may still contain unknown tags — strip them)
                var text2 = DecodeEntities(StripTags(part));
                if (!string.IsNullOrEmpty(text2))
                    inlines.Add(new Run(text2));
            }
        }

        private static void OnNavigate(object sender, RequestNavigateEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            } catch { }
            e.Handled = true;
        }

        private static string StripTags(string html) =>
            Regex.Replace(html, "<[^>]*>", string.Empty);

        private static string DecodeEntities(string text) =>
            text.Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&apos;", "'")
                .Replace("&nbsp;", "\u00a0")
                .Replace("&#160;", "\u00a0");
    }
}
