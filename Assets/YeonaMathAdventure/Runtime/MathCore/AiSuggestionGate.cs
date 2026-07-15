using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YeonaMathAdventure.MathCore
{
    [Serializable]
    public sealed class AiActivitySuggestion
    {
        public int schemaVersion;
        public string requestId;
        public string problemKind;
        public string conceptKey;
        public int difficulty;
        public string sourceProblemId;
        public string promptKo;
        public string[] hintsKo = new string[0];
    }

    [Serializable]
    public sealed class AiResolvedActivity
    {
        public bool usedAiSuggestion;
        public string rejectionCode;
        public LocalActivityDescriptor localProblem;
        public string promptKo;
        public string[] hintsKo = new string[0];
    }

    /// <summary>
    /// Parses only the documented AI-selection JSON. Any mathematical answer field,
    /// unknown field, nested object, duplicate key, or non-integer number is rejected.
    /// </summary>
    public static class StrictAiSuggestionJson
    {
        public const int SchemaVersion = 1;
        public const int MaximumJsonLength = 4096;

        private static readonly HashSet<string> AllowedKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "schemaVersion",
            "requestId",
            "problemKind",
            "conceptKey",
            "difficulty",
            "sourceProblemId",
            "promptKo",
            "hintsKo"
        };

        public static bool TryParse(string json, out AiActivitySuggestion suggestion, out string errorCode)
        {
            suggestion = null;
            errorCode = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                errorCode = "ai_json_empty";
                return false;
            }

            if (json.Length > MaximumJsonLength)
            {
                errorCode = "ai_json_too_large";
                return false;
            }

            Dictionary<string, object> values;
            try
            {
                values = new StrictObjectParser(json).Parse();
            }
            catch (StrictJsonException exception)
            {
                errorCode = exception.Code;
                return false;
            }

            foreach (string key in values.Keys)
            {
                if (!AllowedKeys.Contains(key))
                {
                    errorCode = "ai_unknown_field";
                    return false;
                }
            }

            if (values.Count != AllowedKeys.Count)
            {
                errorCode = "ai_required_field_missing";
                return false;
            }

            long schemaVersion;
            long difficulty;
            string requestId;
            string problemKind;
            string conceptKey;
            string sourceProblemId;
            string promptKo;
            string[] hintsKo;
            if (!TryGetInteger(values, "schemaVersion", out schemaVersion) ||
                !TryGetString(values, "requestId", out requestId) ||
                !TryGetString(values, "problemKind", out problemKind) ||
                !TryGetString(values, "conceptKey", out conceptKey) ||
                !TryGetInteger(values, "difficulty", out difficulty) ||
                !TryGetString(values, "sourceProblemId", out sourceProblemId) ||
                !TryGetString(values, "promptKo", out promptKo) ||
                !TryGetStringArray(values, "hintsKo", out hintsKo))
            {
                errorCode = "ai_field_type_invalid";
                return false;
            }

            if (schemaVersion < int.MinValue || schemaVersion > int.MaxValue ||
                difficulty < int.MinValue || difficulty > int.MaxValue)
            {
                errorCode = "ai_integer_out_of_range";
                return false;
            }

            suggestion = new AiActivitySuggestion
            {
                schemaVersion = (int)schemaVersion,
                requestId = requestId,
                problemKind = problemKind,
                conceptKey = conceptKey,
                difficulty = (int)difficulty,
                sourceProblemId = sourceProblemId,
                promptKo = promptKo,
                hintsKo = hintsKo
            };
            return true;
        }

        private static bool TryGetInteger(Dictionary<string, object> values, string key, out long value)
        {
            object raw;
            if (values.TryGetValue(key, out raw) && raw is long)
            {
                value = (long)raw;
                return true;
            }

            value = 0L;
            return false;
        }

        private static bool TryGetString(Dictionary<string, object> values, string key, out string value)
        {
            object raw;
            if (values.TryGetValue(key, out raw) && raw is string)
            {
                value = (string)raw;
                return true;
            }

            value = null;
            return false;
        }

        private static bool TryGetStringArray(Dictionary<string, object> values, string key, out string[] value)
        {
            object raw;
            if (values.TryGetValue(key, out raw) && raw is string[])
            {
                value = (string[])raw;
                return true;
            }

            value = null;
            return false;
        }

        private sealed class StrictObjectParser
        {
            private readonly string text;
            private int position;

            public StrictObjectParser(string text)
            {
                this.text = text;
            }

            public Dictionary<string, object> Parse()
            {
                SkipWhitespace();
                Expect('{', "ai_json_object_expected");
                Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (Match('}'))
                {
                    EnsureComplete();
                    return values;
                }

                while (true)
                {
                    SkipWhitespace();
                    string key = ParseString();
                    if (values.ContainsKey(key))
                    {
                        throw new StrictJsonException("ai_duplicate_field");
                    }

                    SkipWhitespace();
                    Expect(':', "ai_colon_expected");
                    SkipWhitespace();
                    values.Add(key, ParseValue());
                    SkipWhitespace();
                    if (Match('}'))
                    {
                        EnsureComplete();
                        return values;
                    }

                    Expect(',', "ai_comma_expected");
                }
            }

            private object ParseValue()
            {
                if (position >= text.Length)
                {
                    throw new StrictJsonException("ai_value_expected");
                }

                char value = text[position];
                if (value == '"')
                {
                    return ParseString();
                }

                if (value == '[')
                {
                    return ParseStringArray();
                }

                if (value == '-' || IsAsciiDigit(value))
                {
                    return ParseInteger();
                }

                throw new StrictJsonException("ai_value_type_not_allowed");
            }

            private string[] ParseStringArray()
            {
                Expect('[', "ai_array_expected");
                List<string> values = new List<string>();
                SkipWhitespace();
                if (Match(']'))
                {
                    return values.ToArray();
                }

                while (true)
                {
                    SkipWhitespace();
                    values.Add(ParseString());
                    SkipWhitespace();
                    if (Match(']'))
                    {
                        return values.ToArray();
                    }

                    Expect(',', "ai_array_comma_expected");
                }
            }

            private long ParseInteger()
            {
                int start = position;
                if (Match('-') && (position >= text.Length || !IsAsciiDigit(text[position])))
                {
                    throw new StrictJsonException("ai_integer_invalid");
                }

                if (position < text.Length && text[position] == '0')
                {
                    position++;
                    if (position < text.Length && IsAsciiDigit(text[position]))
                    {
                        throw new StrictJsonException("ai_integer_invalid");
                    }
                }
                else
                {
                    while (position < text.Length && IsAsciiDigit(text[position]))
                    {
                        position++;
                    }
                }

                string token = text.Substring(start, position - start);
                long result;
                if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result))
                {
                    throw new StrictJsonException("ai_integer_invalid");
                }

                return result;
            }

            private string ParseString()
            {
                Expect('"', "ai_string_expected");
                StringBuilder builder = new StringBuilder();
                while (position < text.Length)
                {
                    char value = text[position++];
                    if (value == '"')
                    {
                        return builder.ToString();
                    }

                    if (value == '\\')
                    {
                        if (position >= text.Length)
                        {
                            throw new StrictJsonException("ai_escape_invalid");
                        }

                        char escaped = text[position++];
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
                                throw new StrictJsonException("ai_escape_invalid");
                        }
                    }
                    else
                    {
                        if (value < 0x20)
                        {
                            throw new StrictJsonException("ai_control_character_invalid");
                        }

                        builder.Append(value);
                    }
                }

                throw new StrictJsonException("ai_string_unterminated");
            }

            private char ParseUnicodeEscape()
            {
                if (position + 4 > text.Length)
                {
                    throw new StrictJsonException("ai_unicode_escape_invalid");
                }

                int value = 0;
                for (int index = 0; index < 4; index++)
                {
                    int digit = HexValue(text[position++]);
                    if (digit < 0)
                    {
                        throw new StrictJsonException("ai_unicode_escape_invalid");
                    }

                    value = value * 16 + digit;
                }

                return (char)value;
            }

            private void EnsureComplete()
            {
                SkipWhitespace();
                if (position != text.Length)
                {
                    throw new StrictJsonException("ai_trailing_data");
                }
            }

            private void SkipWhitespace()
            {
                while (position < text.Length &&
                       (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n'))
                {
                    position++;
                }
            }

            private bool Match(char value)
            {
                if (position < text.Length && text[position] == value)
                {
                    position++;
                    return true;
                }

                return false;
            }

            private void Expect(char value, string errorCode)
            {
                if (!Match(value))
                {
                    throw new StrictJsonException(errorCode);
                }
            }

            private static int HexValue(char value)
            {
                if (value >= '0' && value <= '9')
                {
                    return value - '0';
                }

                if (value >= 'a' && value <= 'f')
                {
                    return value - 'a' + 10;
                }

                if (value >= 'A' && value <= 'F')
                {
                    return value - 'A' + 10;
                }

                return -1;
            }

            private static bool IsAsciiDigit(char value)
            {
                return value >= '0' && value <= '9';
            }
        }

        private sealed class StrictJsonException : Exception
        {
            public readonly string Code;

            public StrictJsonException(string code)
            {
                Code = code;
            }
        }
    }

    public static class AiSuggestionGate
    {
        public static AiResolvedActivity ResolveOrFallback(
            string json,
            string expectedRequestId,
            AdaptiveRecommendation recommendation)
        {
            if (recommendation == null)
            {
                throw new ArgumentNullException("recommendation");
            }

            AiActivitySuggestion suggestion;
            string rejectionCode;
            LocalActivityDescriptor descriptor;
            if (StrictAiSuggestionJson.TryParse(json, out suggestion, out rejectionCode) &&
                ValidateSuggestion(suggestion, expectedRequestId, recommendation, out descriptor, out rejectionCode))
            {
                return new AiResolvedActivity
                {
                    usedAiSuggestion = true,
                    rejectionCode = string.Empty,
                    localProblem = descriptor,
                    promptKo = suggestion.promptKo,
                    hintsKo = (string[])suggestion.hintsKo.Clone()
                };
            }

            LocalActivityDescriptor fallback = LocalProblemBank.SelectFallback(
                recommendation.concept,
                recommendation.numberDifficulty);
            return new AiResolvedActivity
            {
                usedAiSuggestion = false,
                rejectionCode = rejectionCode,
                localProblem = fallback,
                promptKo = fallback.defaultPromptKo,
                hintsKo = (string[])fallback.defaultHintsKo.Clone()
            };
        }

        private static bool ValidateSuggestion(
            AiActivitySuggestion suggestion,
            string expectedRequestId,
            AdaptiveRecommendation recommendation,
            out LocalActivityDescriptor descriptor,
            out string errorCode)
        {
            descriptor = null;
            errorCode = string.Empty;
            if (suggestion == null || suggestion.schemaVersion != StrictAiSuggestionJson.SchemaVersion)
            {
                errorCode = "ai_schema_version_rejected";
                return false;
            }

            if (!IsSafeIdentifier(suggestion.requestId, 64) ||
                !string.Equals(suggestion.requestId, expectedRequestId, StringComparison.Ordinal))
            {
                errorCode = "ai_request_id_rejected";
                return false;
            }

            if (!IsSafeIdentifier(suggestion.sourceProblemId, 80) ||
                !LocalProblemBank.TryGetDescriptor(suggestion.sourceProblemId, out descriptor))
            {
                errorCode = "ai_local_problem_not_found";
                return false;
            }

            if (!string.Equals(suggestion.problemKind, descriptor.problemKind, StringComparison.Ordinal) ||
                !string.Equals(suggestion.conceptKey, descriptor.conceptKey, StringComparison.Ordinal) ||
                suggestion.difficulty != descriptor.difficulty)
            {
                errorCode = "ai_problem_metadata_mismatch";
                descriptor = null;
                return false;
            }

            if (descriptor.concept != recommendation.concept ||
                Math.Abs(descriptor.difficulty - DifficultyRules.Clamp(recommendation.numberDifficulty)) > 1)
            {
                errorCode = "ai_teacher_boundary_rejected";
                descriptor = null;
                return false;
            }

            if (!IsSafeKoreanText(suggestion.promptKo, 180) ||
                suggestion.hintsKo == null || suggestion.hintsKo.Length > 3)
            {
                errorCode = "ai_text_rejected";
                descriptor = null;
                return false;
            }

            for (int index = 0; index < suggestion.hintsKo.Length; index++)
            {
                if (!IsSafeKoreanText(suggestion.hintsKo[index], 100))
                {
                    errorCode = "ai_hint_rejected";
                    descriptor = null;
                    return false;
                }
            }

            return true;
        }

        private static bool IsSafeIdentifier(string value, int maximumLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length > maximumLength)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool valid = (character >= 'a' && character <= 'z') ||
                             (character >= 'A' && character <= 'Z') ||
                             (character >= '0' && character <= '9') ||
                             character == '-' || character == '_';
                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSafeKoreanText(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
            {
                return false;
            }

            bool hasHangul = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsControl(character) || character == '<' || character == '>' ||
                    character == '{' || character == '}')
                {
                    return false;
                }

                if (character >= '\uAC00' && character <= '\uD7A3')
                {
                    hasHangul = true;
                }
            }

            string[] rejectedFragments =
            {
                "http://",
                "https://",
                "www.",
                "API 키",
                "비밀번호",
                "이름을 입력",
                "전화번호",
                "이메일"
            };
            for (int index = 0; index < rejectedFragments.Length; index++)
            {
                if (value.IndexOf(rejectedFragments[index], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            return hasHangul;
        }
    }
}
