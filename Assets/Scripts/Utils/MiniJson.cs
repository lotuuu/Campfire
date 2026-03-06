// Minimal JSON parser returning Dictionary<string,object> / List<object> / string / double / long / bool / null.
// Based on the public domain MiniJSON by Calvin Rien.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Garden
{
    public static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (json == null) return null;
            return Parser.Parse(json);
        }

        private sealed class Parser : IDisposable
        {
            private StringReader _reader;

            private Parser(string json) => _reader = new StringReader(json);

            public static object Parse(string json)
            {
                using var p = new Parser(json);
                return p.ParseValue();
            }

            public void Dispose() => _reader?.Dispose();

            private object ParseValue()
            {
                SkipWhitespace();
                int peek = Peek();
                switch (peek)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case -1: return null;
                    default:
                        // number, true, false, null
                        return ParseOther();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>();
                Read(); // '{'
                while (true)
                {
                    SkipWhitespace();
                    int peek = Peek();
                    if (peek == '}') { Read(); return dict; }
                    if (peek == ',') { Read(); continue; }
                    string key = ParseString();
                    SkipWhitespace();
                    Read(); // ':'
                    dict[key] = ParseValue();
                }
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                Read(); // '['
                while (true)
                {
                    SkipWhitespace();
                    int peek = Peek();
                    if (peek == ']') { Read(); return list; }
                    if (peek == ',') { Read(); continue; }
                    list.Add(ParseValue());
                }
            }

            private string ParseString()
            {
                Read(); // opening '"'
                var sb = new StringBuilder();
                while (true)
                {
                    int c = Read();
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        int next = Read();
                        switch (next)
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
                                var hex = new char[4];
                                for (int i = 0; i < 4; i++) hex[i] = (char)Read();
                                sb.Append((char)Convert.ToInt32(new string(hex), 16));
                                break;
                        }
                    }
                    else
                    {
                        sb.Append((char)c);
                    }
                }
            }

            private object ParseOther()
            {
                var sb = new StringBuilder();
                while (true)
                {
                    int peek = Peek();
                    if (peek == -1 || peek == ',' || peek == '}' || peek == ']' ||
                        peek == ' ' || peek == '\t' || peek == '\n' || peek == '\r')
                        break;
                    sb.Append((char)Read());
                }
                string s = sb.ToString().Trim();
                if (s == "null") return null;
                if (s == "true") return true;
                if (s == "false") return false;
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                    return l;
                if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double d))
                    return d;
                return s;
            }

            private void SkipWhitespace()
            {
                while (true)
                {
                    int peek = Peek();
                    if (peek == ' ' || peek == '\t' || peek == '\n' || peek == '\r')
                        Read();
                    else
                        break;
                }
            }

            private int Peek() => _reader.Peek();
            private int Read() => _reader.Read();
        }
    }
}
