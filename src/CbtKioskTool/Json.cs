using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CbtKioskTool
{
    /// <summary>
    /// Minimal JSON reader/writer. Written by hand so the tool needs no JSON library at all, which
    /// keeps setting-CBT.exe at a few dozen kilobytes.
    /// </summary>
    internal static class Json
    {
        // ================================================================= read

        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("JSON kosong.");
            var i = 0;
            var value = ParseValue(text, ref i);
            SkipWhitespace(text, ref i);
            if (i < text.Length) throw new FormatException("JSON tidak valid (ada sisa karakter).");
            return value;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON terpotong.");

            switch (s[i])
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            i++; // {
            SkipWhitespace(s, ref i);

            if (i < s.Length && s[i] == '}') { i++; return result; }

            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("JSON: nama properti tidak valid.");
                var name = ParseString(s, ref i);

                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("JSON: ':' tidak ditemukan.");
                i++;

                result[name] = ParseValue(s, ref i);

                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON terpotong.");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return result; }
                throw new FormatException("JSON: ',' atau '}' tidak ditemukan.");
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var result = new List<object>();
            i++; // [
            SkipWhitespace(s, ref i);

            if (i < s.Length && s[i] == ']') { i++; return result; }

            while (true)
            {
                result.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON terpotong.");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return result; }
                throw new FormatException("JSON: ',' atau ']' tidak ditemukan.");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // opening quote
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new FormatException("JSON: string tidak ditutup.");
                var c = s[i++];
                if (c == '"') return sb.ToString();

                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) throw new FormatException("JSON: escape terpotong.");
                var e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("JSON: \\u terpotong.");
                        sb.Append((char)ushort.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw new FormatException("JSON: escape tidak dikenal \\" + e);
                }
            }
        }

        private static object ParseNumber(string s, ref int i)
        {
            var start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' ||
                                    s[i] == 'e' || s[i] == 'E'))
                i++;

            if (start == i) throw new FormatException("JSON: nilai tidak dikenali pada posisi " + i + ".");

            var raw = s.Substring(start, i - start);
            if (raw.IndexOf('.') < 0 && raw.IndexOf('e') < 0 && raw.IndexOf('E') < 0)
            {
                if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
            }

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;

            throw new FormatException("JSON: angka tidak valid '" + raw + "'.");
        }

        private static void Expect(string s, ref int i, string word)
        {
            if (i + word.Length > s.Length || string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                throw new FormatException("JSON: '" + word + "' tidak ditemukan.");
            i += word.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        // ============================================================== helpers

        public static string AsString(object v) => v as string;

        public static bool AsBool(object v, bool fallback)
        {
            if (v is bool b) return b;
            if (v is string s && bool.TryParse(s, out var parsed)) return parsed;
            return fallback;
        }

        public static int AsInt(object v, int fallback)
        {
            if (v is long l) return (int)l;
            if (v is double d) return (int)d;
            if (v is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
            return fallback;
        }

        public static Dictionary<string, object> AsObject(object v) => v as Dictionary<string, object>;

        // =============================================================== write

        public static string SerializeSettings(AppSettings s)
        {
            var sb = new StringBuilder();
            sb.Append("{\r\n");

            Field(sb, "BaseUrl", s.BaseUrl, false);
            Field(sb, "StartUrl", s.StartUrl, false);
            Field(sb, "KioskUrl", s.KioskUrl, false);
            Field(sb, "FallbackUrl", s.FallbackUrl, false);
            Field(sb, "KioskTimeoutMs", s.KioskTimeoutMs, false);
            Field(sb, "KioskAttempts", s.KioskAttempts, false);
            Field(sb, "KioskAttemptIntervalMs", s.KioskAttemptIntervalMs, false);
            Field(sb, "OfflineFallbackPasswordHash", s.OfflineFallbackPasswordHash, false);
            Field(sb, "AllowDefaultEmergencyPassword", s.AllowDefaultEmergencyPassword, false);
            Field(sb, "BlockNavigationKeys", s.BlockNavigationKeys, false);
            Field(sb, "AllowZoom", s.AllowZoom, false);
            Field(sb, "AllowBackNavigation", s.AllowBackNavigation, false);
            Field(sb, "SettingsPasswordHash", s.SettingsPasswordHash, false);
            Field(sb, "ClearSessionOnQuit", s.ClearSessionOnQuit, false);
            Field(sb, "AutoStart", s.AutoStart, false);
            Field(sb, "AutoStartAllUsers", s.AutoStartAllUsers, false);
            Field(sb, "StartupMethod", s.StartupMethod, true);

            sb.Append("}\r\n");
            return sb.ToString();
        }

        private static void Field(StringBuilder sb, string name, object value, bool isLast)
        {
            sb.Append("  \"");
            sb.Append(name);
            sb.Append("\": ");
            AppendValue(sb, value);
            sb.Append(isLast ? "\r\n" : ",\r\n");
        }

        private static void AppendValue(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (v is int i) { sb.Append(i.ToString(CultureInfo.InvariantCulture)); return; }
            if (v is long l) { sb.Append(l.ToString(CultureInfo.InvariantCulture)); return; }
            if (v is double d) { sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return; }
            AppendString(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        private static void AppendString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
