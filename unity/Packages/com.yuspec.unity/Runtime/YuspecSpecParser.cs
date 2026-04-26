using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Yuspec.Unity
{
    public sealed class YuspecSpecParser
    {
        private static readonly Regex EntityStart = new Regex(@"^entity\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{\s*$", RegexOptions.Compiled);
        private static readonly Regex PropertyAssignment = new Regex(@"^([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+)$", RegexOptions.Compiled);
        private static readonly Regex HandlerStart = new Regex(
            @"^on\s+([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)(?:\s+with\s+([A-Za-z_][A-Za-z0-9_]*))?(?:\s+when\s+(.+))?:\s*$",
            RegexOptions.Compiled);
        private static readonly Regex SetAction = new Regex(@"^set\s+([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+)$", RegexOptions.Compiled);
        private static readonly Regex HasCondition = new Regex(
            @"^([A-Za-z_][A-Za-z0-9_]*)\.has\(([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\)$",
            RegexOptions.Compiled);
        private static readonly Regex EqualsCondition = new Regex(
            @"^([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\s*==\s*(.+)$",
            RegexOptions.Compiled);

        private readonly List<YuspecDiagnostic> diagnostics = new List<YuspecDiagnostic>();

        public IReadOnlyList<YuspecDiagnostic> Diagnostics => diagnostics;

        public YuspecCompiledSpec Parse(string sourceName, string sourceText)
        {
            diagnostics.Clear();

            var spec = new YuspecCompiledSpec(sourceName);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Warning, "YSP1000", "Spec is empty.", sourceName));
                return spec;
            }

            var lines = sourceText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                var lineNumber = index + 1;
                var line = StripComment(lines[index]).Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var entityMatch = EntityStart.Match(line);
                if (entityMatch.Success)
                {
                    index = ParseEntity(spec, entityMatch.Groups[1].Value, lines, index + 1, sourceName);
                    continue;
                }

                var handlerMatch = HandlerStart.Match(line);
                if (handlerMatch.Success)
                {
                    index = ParseHandler(spec, handlerMatch, lines, index + 1, sourceName, lineNumber);
                    continue;
                }

                if (line.StartsWith("scenario ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("behavior ", StringComparison.OrdinalIgnoreCase))
                {
                    index = SkipBlock(lines, index + 1);
                    continue;
                }

                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Warning, "YSP1001", $"Unsupported top-level syntax '{line}'.", sourceName, lineNumber, 1));
            }

            return spec;
        }

        private int ParseEntity(YuspecCompiledSpec spec, string entityType, string[] lines, int startIndex, string sourceName)
        {
            var declaration = new YuspecEntityDeclaration(entityType);
            for (var index = startIndex; index < lines.Length; index++)
            {
                var lineNumber = index + 1;
                var line = StripComment(lines[index]).Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line == "}")
                {
                    spec.Entities.Add(declaration);
                    return index;
                }

                var propertyMatch = PropertyAssignment.Match(line);
                if (!propertyMatch.Success)
                {
                    diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP1002", $"Invalid entity property syntax '{line}'.", sourceName, lineNumber, 1));
                    continue;
                }

                declaration.Properties[propertyMatch.Groups[1].Value] = ParseLiteral(propertyMatch.Groups[2].Value.Trim());
            }

            diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP1003", $"Entity '{entityType}' is missing closing brace.", sourceName, startIndex, 1));
            spec.Entities.Add(declaration);
            return lines.Length - 1;
        }

        private int ParseHandler(YuspecCompiledSpec spec, Match handlerMatch, string[] lines, int startIndex, string sourceName, int lineNumber)
        {
            var actorType = handlerMatch.Groups[1].Value;
            var eventName = $"{actorType}.{handlerMatch.Groups[2].Value}";
            var targetType = handlerMatch.Groups[3].Success ? handlerMatch.Groups[3].Value : string.Empty;
            var condition = handlerMatch.Groups[4].Success ? ParseCondition(handlerMatch.Groups[4].Value.Trim(), sourceName, lineNumber) : YuspecCondition.None;
            var handler = new YuspecEventHandler(actorType, eventName, targetType, condition, sourceName, lineNumber);

            var lastActionIndex = startIndex - 1;
            for (var index = startIndex; index < lines.Length; index++)
            {
                var rawLine = lines[index];
                var trimmed = StripComment(rawLine).Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (!char.IsWhiteSpace(rawLine, 0))
                {
                    break;
                }

                var action = ParseAction(trimmed, sourceName, index + 1);
                if (action != null)
                {
                    handler.Actions.Add(action);
                    lastActionIndex = index;
                }
            }

            if (handler.Actions.Count == 0)
            {
                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Warning, "YSP1004", $"Handler '{eventName}' has no actions.", sourceName, lineNumber, 1));
            }

            spec.EventHandlers.Add(handler);
            return lastActionIndex;
        }

        private YuspecCondition ParseCondition(string conditionText, string sourceName, int lineNumber)
        {
            var hasMatch = HasCondition.Match(conditionText);
            if (hasMatch.Success)
            {
                return new YuspecCondition(
                    YuspecConditionKind.HasValue,
                    hasMatch.Groups[1].Value,
                    string.Empty,
                    hasMatch.Groups[2].Value,
                    hasMatch.Groups[3].Value);
            }

            var equalsMatch = EqualsCondition.Match(conditionText);
            if (equalsMatch.Success)
            {
                return new YuspecCondition(
                    YuspecConditionKind.Equals,
                    equalsMatch.Groups[1].Value,
                    equalsMatch.Groups[2].Value,
                    string.Empty,
                    equalsMatch.Groups[3].Value.Trim());
            }

            diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Warning, "YSP1005", $"Unsupported condition '{conditionText}'. It will fail closed.", sourceName, lineNumber, 1));
            return new YuspecCondition(YuspecConditionKind.Equals, "__unsupported__", "__unsupported__", string.Empty, "__unsupported__");
        }

        private YuspecActionCall ParseAction(string actionText, string sourceName, int lineNumber)
        {
            var setMatch = SetAction.Match(actionText);
            if (setMatch.Success)
            {
                return new YuspecActionCall(setMatch.Groups[1].Value, setMatch.Groups[2].Value, setMatch.Groups[3].Value.Trim(), lineNumber);
            }

            var tokens = Tokenize(actionText);
            if (tokens.Count == 0)
            {
                return null;
            }

            var action = new YuspecActionCall(tokens[0], lineNumber);
            for (var i = 1; i < tokens.Count; i++)
            {
                action.Arguments.Add(tokens[i]);
            }

            return action;
        }

        public static object ParseLiteral(string text)
        {
            text = text.Trim();
            if (text == "[]")
            {
                return new List<string>();
            }

            if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
            {
                return text.Substring(1, text.Length - 2);
            }

            if (bool.TryParse(text, out var boolean))
            {
                return boolean;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                return integer;
            }

            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return number;
            }

            return text;
        }

        public static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var builder = new StringBuilder();
            var inString = false;

            for (var i = 0; i < text.Length; i++)
            {
                var character = text[i];
                if (character == '"')
                {
                    inString = !inString;
                    builder.Append(character);
                    continue;
                }

                if (char.IsWhiteSpace(character) && !inString)
                {
                    AddToken(tokens, builder);
                    continue;
                }

                builder.Append(character);
            }

            AddToken(tokens, builder);
            return tokens;
        }

        private static void AddToken(List<string> tokens, StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                return;
            }

            tokens.Add(builder.ToString());
            builder.Clear();
        }

        private static int SkipBlock(string[] lines, int startIndex)
        {
            var depth = 0;
            for (var index = startIndex - 1; index < lines.Length; index++)
            {
                var line = StripComment(lines[index]);
                foreach (var character in line)
                {
                    if (character == '{')
                    {
                        depth++;
                    }
                    else if (character == '}')
                    {
                        depth--;
                        if (depth <= 0)
                        {
                            return index;
                        }
                    }
                }
            }

            return lines.Length - 1;
        }

        private static string StripComment(string line)
        {
            var index = line.IndexOf("//", StringComparison.Ordinal);
            return index >= 0 ? line.Substring(0, index) : line;
        }
    }
}
