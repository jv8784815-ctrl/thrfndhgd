using System.Text;
using System.Text.RegularExpressions;

namespace JarvisTeto.Utils
{
    /// <summary>
    /// Gemini devuelve el texto en Markdown (**negrita**, # títulos, `código`, listas con "- ", etc.).
    /// Como Jarvis no renderiza Markdown, esos símbolos quedaban visibles tal cual (de ahí los
    /// "***********" que se veían en el chat y que la voz llegaba a leer literalmente como "asterisco").
    /// Esta clase limpia el texto para mostrarlo y, por separado, para hablarlo.
    /// </summary>
    public static class TextUtils
    {
        private static readonly Regex CodeBlock = new(@"```[a-zA-Z]*\r?\n?([\s\S]*?)```", RegexOptions.Compiled);
        private static readonly Regex InlineCode = new(@"`([^`]+)`", RegexOptions.Compiled);
        private static readonly Regex BoldItalicStar = new(@"\*{1,3}([^*\n]+?)\*{1,3}", RegexOptions.Compiled);
        private static readonly Regex BoldItalicUnderscore = new(@"_{1,3}([^_\n]+?)_{1,3}", RegexOptions.Compiled);
        private static readonly Regex HeaderMark = new(@"^\s{0,3}#{1,6}\s*", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex BulletMark = new(@"^\s*[-*•]\s+", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex NumberedMark = new(@"^\s*\d+[.)]\s+", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex MarkdownLink = new(@"\[([^\]]+)\]\((?:[^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex HorizontalRule = new(@"^\s*([-*_])\s*(\1\s*){2,}$", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex StrayAsterisks = new(@"\*{2,}", RegexOptions.Compiled);
        private static readonly Regex StrayUnderscores = new(@"_{2,}", RegexOptions.Compiled);
        private static readonly Regex MultiBlankLines = new(@"\n{3,}", RegexOptions.Compiled);
        private static readonly Regex MultiSpaces = new(@"[ \t]{2,}", RegexOptions.Compiled);

        /// <summary>
        /// Versión para mostrar en el chat: saca el marcado pero conserva saltos de línea y viñetas
        /// legibles (las convierte en "• ").
        /// </summary>
        public static string ForDisplay(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            string text = raw.Replace("\r\n", "\n");

            text = CodeBlock.Replace(text, m => m.Groups[1].Value.Trim());
            text = InlineCode.Replace(text, "$1");
            text = HorizontalRule.Replace(text, string.Empty);
            text = HeaderMark.Replace(text, string.Empty);
            text = MarkdownLink.Replace(text, "$1");
            text = BulletMark.Replace(text, "• ");
            text = NumberedMark.Replace(text, m => m.Value.TrimStart());
            text = BoldItalicStar.Replace(text, "$1");
            text = BoldItalicUnderscore.Replace(text, "$1");

            // Cualquier resto de asteriscos/guiones bajos sueltos que no formaban un par válido
            // (la causa más común de los "***********" que aparecían en el chat).
            text = StrayAsterisks.Replace(text, string.Empty);
            text = StrayUnderscores.Replace(text, string.Empty);
            text = text.Replace("*", string.Empty);

            text = MultiSpaces.Replace(text, " ");
            text = MultiBlankLines.Replace(text, "\n\n");

            return text.Trim();
        }

        /// <summary>
        /// Versión para el sintetizador de voz: además de limpiar el marcado, aplana todo a texto
        /// corrido (sin viñetas ni saltos de línea) para que no diga "guión" ni pausas raras.
        /// </summary>
        public static string ForSpeech(string? raw)
        {
            string display = ForDisplay(raw);
            if (string.IsNullOrEmpty(display)) return string.Empty;

            var sb = new StringBuilder(display.Length);
            foreach (char c in display)
                sb.Append(c == '\n' ? ' ' : c);

            string flat = sb.ToString();
            flat = flat.Replace("•", string.Empty);
            flat = MultiSpaces.Replace(flat, " ");
            return flat.Trim();
        }
    }
}
