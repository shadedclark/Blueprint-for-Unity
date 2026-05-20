using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlueprintSystem
{
    public sealed class BlueprintJsonException : Exception
    {
        public BlueprintJsonException(string message)
            : base(message)
        {
        }
    }

    public static class BlueprintJson
    {
        public static Dictionary<string, object> DeserializeObject(string json)
        {
            object value = Deserialize(json);
            Dictionary<string, object> obj = value as Dictionary<string, object>;
            if (obj == null)
            {
                throw new BlueprintJsonException("JSON root must be an object.");
            }

            return obj;
        }

        public static object Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException("json");
            }

            Parser parser = new Parser(json);
            object value = parser.ParseValue();
            parser.SkipWhitespace();
            if (!parser.IsEnd)
            {
                throw parser.Error("Unexpected trailing content.");
            }

            return value;
        }

        public static string Serialize(object value, bool pretty)
        {
            StringBuilder builder = new StringBuilder();
            WriteValue(builder, value, pretty, 0);
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, object value, bool pretty, int depth)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            string text = value as string;
            if (text != null)
            {
                WriteString(builder, text);
                return;
            }

            if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            if (IsNumber(value))
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            IDictionary<string, object> dictionary = value as IDictionary<string, object>;
            if (dictionary != null)
            {
                WriteObject(builder, dictionary, pretty, depth);
                return;
            }

            IDictionary genericDictionary = value as IDictionary;
            if (genericDictionary != null)
            {
                Dictionary<string, object> normalized = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in genericDictionary)
                {
                    normalized[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = entry.Value;
                }

                WriteObject(builder, normalized, pretty, depth);
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                WriteArray(builder, enumerable, pretty, depth);
                return;
            }

            WriteString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void WriteObject(StringBuilder builder, IDictionary<string, object> value, bool pretty, int depth)
        {
            builder.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, object> pair in value)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                if (pretty)
                {
                    builder.AppendLine();
                    Indent(builder, depth + 1);
                }

                WriteString(builder, pair.Key);
                builder.Append(pretty ? ": " : ":");
                WriteValue(builder, pair.Value, pretty, depth + 1);
                first = false;
            }

            if (pretty && value.Count > 0)
            {
                builder.AppendLine();
                Indent(builder, depth);
            }

            builder.Append('}');
        }

        private static void WriteArray(StringBuilder builder, IEnumerable value, bool pretty, int depth)
        {
            builder.Append('[');
            bool first = true;
            foreach (object item in value)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                if (pretty)
                {
                    builder.AppendLine();
                    Indent(builder, depth + 1);
                }

                WriteValue(builder, item, pretty, depth + 1);
                first = false;
            }

            if (pretty && !first)
            {
                builder.AppendLine();
                Indent(builder, depth);
            }

            builder.Append(']');
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        private static void Indent(StringBuilder builder, int depth)
        {
            for (int i = 0; i < depth; i++)
            {
                builder.Append("  ");
            }
        }

        private static bool IsNumber(object value)
        {
            return value is byte ||
                   value is sbyte ||
                   value is short ||
                   value is ushort ||
                   value is int ||
                   value is uint ||
                   value is long ||
                   value is ulong ||
                   value is float ||
                   value is double ||
                   value is decimal;
        }

        private sealed class Parser
        {
            private readonly string _json;
            private int _index;

            public Parser(string json)
            {
                _json = json;
            }

            public bool IsEnd
            {
                get { return _index >= _json.Length; }
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (IsEnd)
                {
                    throw Error("Unexpected end of JSON.");
                }

                char c = _json[_index];
                if (c == '{')
                {
                    return ParseObject();
                }

                if (c == '[')
                {
                    return ParseArray();
                }

                if (c == '"')
                {
                    return ParseString();
                }

                if (c == 't')
                {
                    Expect("true");
                    return true;
                }

                if (c == 'f')
                {
                    Expect("false");
                    return false;
                }

                if (c == 'n')
                {
                    Expect("null");
                    return null;
                }

                if (c == '-' || char.IsDigit(c))
                {
                    return ParseNumber();
                }

                throw Error("Unexpected token '" + c + "'.");
            }

            public void SkipWhitespace()
            {
                while (!IsEnd && char.IsWhiteSpace(_json[_index]))
                {
                    _index++;
                }
            }

            public BlueprintJsonException Error(string message)
            {
                return new BlueprintJsonException(message + " At character " + _index + ".");
            }

            private Dictionary<string, object> ParseObject()
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                _index++;
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (IsEnd || _json[_index] != '"')
                    {
                        throw Error("Expected object key.");
                    }

                    string key = ParseString();
                    SkipWhitespace();
                    Consume(':');
                    object value = ParseValue();
                    result[key] = value;
                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    Consume(',');
                }
            }

            private List<object> ParseArray()
            {
                List<object> result = new List<object>();
                _index++;
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    Consume(',');
                }
            }

            private string ParseString()
            {
                Consume('"');
                StringBuilder builder = new StringBuilder();
                while (!IsEnd)
                {
                    char c = _json[_index++];
                    if (c == '"')
                    {
                        return builder.ToString();
                    }

                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (IsEnd)
                    {
                        throw Error("Unexpected end of escape sequence.");
                    }

                    char escaped = _json[_index++];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            builder.Append(ParseUnicodeEscape());
                            break;
                        default:
                            throw Error("Unsupported escape sequence '\\" + escaped + "'.");
                    }
                }

                throw Error("Unterminated string.");
            }

            private char ParseUnicodeEscape()
            {
                if (_index + 4 > _json.Length)
                {
                    throw Error("Incomplete unicode escape.");
                }

                string hex = _json.Substring(_index, 4);
                _index += 4;
                int code = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return (char)code;
            }

            private object ParseNumber()
            {
                int start = _index;
                if (_json[_index] == '-')
                {
                    _index++;
                }

                while (!IsEnd && char.IsDigit(_json[_index]))
                {
                    _index++;
                }

                bool isFloat = false;
                if (!IsEnd && _json[_index] == '.')
                {
                    isFloat = true;
                    _index++;
                    while (!IsEnd && char.IsDigit(_json[_index]))
                    {
                        _index++;
                    }
                }

                if (!IsEnd && (_json[_index] == 'e' || _json[_index] == 'E'))
                {
                    isFloat = true;
                    _index++;
                    if (!IsEnd && (_json[_index] == '+' || _json[_index] == '-'))
                    {
                        _index++;
                    }

                    while (!IsEnd && char.IsDigit(_json[_index]))
                    {
                        _index++;
                    }
                }

                string text = _json.Substring(start, _index - start);
                if (isFloat)
                {
                    double doubleValue;
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue))
                    {
                        return doubleValue;
                    }
                }
                else
                {
                    long longValue;
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out longValue))
                    {
                        return longValue;
                    }
                }

                throw Error("Invalid number '" + text + "'.");
            }

            private void Expect(string token)
            {
                for (int i = 0; i < token.Length; i++)
                {
                    if (IsEnd || _json[_index] != token[i])
                    {
                        throw Error("Expected '" + token + "'.");
                    }

                    _index++;
                }
            }

            private void Consume(char expected)
            {
                SkipWhitespace();
                if (IsEnd || _json[_index] != expected)
                {
                    throw Error("Expected '" + expected + "'.");
                }

                _index++;
            }

            private bool TryConsume(char expected)
            {
                SkipWhitespace();
                if (!IsEnd && _json[_index] == expected)
                {
                    _index++;
                    return true;
                }

                return false;
            }
        }
    }
}
