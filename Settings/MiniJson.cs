using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Peeker.Settings
{
    /// <summary>
    /// Just enough JSON to round-trip Peeker's config file. Hand-rolled on purpose:
    /// the only serializer in the Lethal Company install is Newtonsoft, and pulling
    /// it in would mean another assembly reference for a two-level-deep object.
    ///
    /// Parsed values are <c>null</c>, <see cref="bool"/>, <see cref="double"/>,
    /// <see cref="string"/>, <c>Dictionary&lt;string, object&gt;</c> or
    /// <c>List&lt;object&gt;</c>.
    /// </summary>
    internal static class MiniJson
    {
        // ------------------------------------------------------------ writing

        public static string Serialize(object value)
        {
            var sb = new StringBuilder(1024);
            Write(sb, value, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value, int depth)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    return;

                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;

                case string s:
                    WriteString(sb, s);
                    return;

                case IDictionary map:
                    WriteObject(sb, map, depth);
                    return;

                case IEnumerable list when !(value is string):
                    WriteArray(sb, list, depth);
                    return;

                default:
                    WriteNumber(sb, value);
                    return;
            }
        }

        private static void WriteObject(StringBuilder sb, IDictionary map, int depth)
        {
            if (map.Count == 0) { sb.Append("{}"); return; }

            sb.Append("{\n");
            int i = 0;
            foreach (DictionaryEntry entry in map)
            {
                Indent(sb, depth + 1);
                WriteString(sb, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                sb.Append(": ");
                Write(sb, entry.Value, depth + 1);
                if (++i < map.Count) sb.Append(',');
                sb.Append('\n');
            }
            Indent(sb, depth);
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable list, int depth)
        {
            var items = new List<object>();
            foreach (object item in list) items.Add(item);

            if (items.Count == 0) { sb.Append("[]"); return; }

            sb.Append("[\n");
            for (int i = 0; i < items.Count; i++)
            {
                Indent(sb, depth + 1);
                Write(sb, items[i], depth + 1);
                if (i < items.Count - 1) sb.Append(',');
                sb.Append('\n');
            }
            Indent(sb, depth);
            sb.Append(']');
        }

        private static void WriteNumber(StringBuilder sb, object value)
        {
            // "R" keeps float/double round-trippable; everything else is integral.
            switch (value)
            {
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); return;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return;
                case decimal m: sb.Append(m.ToString(CultureInfo.InvariantCulture)); return;
                default:
                    IConvertible convertible = value as IConvertible;
                    if (convertible != null)
                    {
                        sb.Append(convertible.ToString(CultureInfo.InvariantCulture));
                        return;
                    }
                    WriteString(sb, value.ToString());
                    return;
            }
        }

        private static void Indent(StringBuilder sb, int depth) => sb.Append(' ', depth * 2);

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s ?? string.Empty)
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
                        if (c < ' ' || c > '~') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------ reading

        public static object Parse(string text)
        {
            var parser = new Parser(text);
            object value = parser.ParseValue();
            parser.SkipWhitespace();
            if (!parser.AtEnd) throw new FormatException("Trailing content after the top-level JSON value.");
            return value;
        }

        private class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s) { _s = s ?? string.Empty; }

            public bool AtEnd => _i >= _s.Length;

            public void SkipWhitespace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (AtEnd) throw new FormatException("Unexpected end of JSON.");

                switch (_s[_i])
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                _i++;   // '{'
                SkipWhitespace();

                if (!AtEnd && _s[_i] == '}') { _i++; return map; }

                while (true)
                {
                    SkipWhitespace();
                    string key = ParseString();
                    SkipWhitespace();
                    Consume(':');
                    map[key] = ParseValue();
                    SkipWhitespace();

                    if (AtEnd) throw new FormatException("Unterminated object.");
                    if (_s[_i] == ',') { _i++; continue; }
                    Consume('}');
                    return map;
                }
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                _i++;   // '['
                SkipWhitespace();

                if (!AtEnd && _s[_i] == ']') { _i++; return list; }

                while (true)
                {
                    list.Add(ParseValue());
                    SkipWhitespace();

                    if (AtEnd) throw new FormatException("Unterminated array.");
                    if (_s[_i] == ',') { _i++; continue; }
                    Consume(']');
                    return list;
                }
            }

            private string ParseString()
            {
                Consume('"');
                var sb = new StringBuilder();

                while (true)
                {
                    if (AtEnd) throw new FormatException("Unterminated string.");
                    char c = _s[_i++];

                    if (c == '"') return sb.ToString();

                    if (c != '\\') { sb.Append(c); continue; }

                    if (AtEnd) throw new FormatException("Unterminated escape sequence.");
                    char e = _s[_i++];
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
                            if (_i + 4 > _s.Length) throw new FormatException("Truncated \\u escape.");
                            sb.Append((char)Convert.ToInt32(_s.Substring(_i, 4), 16));
                            _i += 4;
                            break;
                        default: throw new FormatException("Unknown escape sequence: \\" + e);
                    }
                }
            }

            private double ParseNumber()
            {
                int start = _i;
                while (_i < _s.Length && "+-.eE0123456789".IndexOf(_s[_i]) >= 0) _i++;

                string slice = _s.Substring(start, _i - start);
                if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw new FormatException("Not a number: '" + slice + "'");

                return value;
            }

            private void Consume(char expected)
            {
                SkipWhitespace();
                if (AtEnd || _s[_i] != expected)
                    throw new FormatException("Expected '" + expected + "' at index " + _i + ".");
                _i++;
            }

            private void Expect(string literal)
            {
                if (_i + literal.Length > _s.Length || string.CompareOrdinal(_s, _i, literal, 0, literal.Length) != 0)
                    throw new FormatException("Expected '" + literal + "' at index " + _i + ".");
                _i += literal.Length;
            }
        }
    }
}
