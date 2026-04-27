using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private static readonly Regex BehaviorStart = new Regex(
            @"^behavior\s+([A-Za-z_][A-Za-z0-9_]*)\s+for\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{\s*$",
            RegexOptions.Compiled);
        private static readonly Regex StateStart = new Regex(@"^state\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{\s*$", RegexOptions.Compiled);
        private static readonly Regex ScenarioStart = new Regex(@"^scenario\s+\""([^\""]+)\""\s*\{\s*$", RegexOptions.Compiled);
        private static readonly Regex TransitionLine = new Regex(@"^on\s+(.+?)\s*->\s*([A-Za-z_][A-Za-z0-9_]*)\s*$", RegexOptions.Compiled);
        private static readonly Regex EveryStart = new Regex(@"^every\s+(.+):\s*$", RegexOptions.Compiled);
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
                    index = ParseEntity(spec, entityMatch.Groups[1].Value, lines, index + 1, sourceName, lineNumber);
                    continue;
                }

                var handlerMatch = HandlerStart.Match(line);
                if (handlerMatch.Success)
                {
                    index = ParseHandler(spec, handlerMatch, lines, index + 1, sourceName, lineNumber);
                    continue;
                }

                var behaviorMatch = BehaviorStart.Match(line);
                if (behaviorMatch.Success)
                {
                    index = ParseBehavior(spec, behaviorMatch, lines, index + 1, sourceName, lineNumber);
                    continue;
                }

                var scenarioMatch = ScenarioStart.Match(line);
                if (scenarioMatch.Success)
                {
                    index = ParseScenario(spec, scenarioMatch, lines, index + 1, sourceName, lineNumber);
                    continue;
                }

                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Warning, "YSP1001", $"Unsupported top-level syntax '{line}'.", sourceName, lineNumber, 1));
            }

            return spec;
        }

        private int ParseEntity(YuspecCompiledSpec spec, string entityType, string[] lines, int startIndex, string sourceName, int startLine)
        {
            var declaration = new YuspecEntityDeclaration(entityType);
            var entitySyntax = new YuspecEntitySyntax(entityType, new YuspecSourceLocation(sourceName, startLine, 1));

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
                    spec.SyntaxTree.Entities.Add(entitySyntax);
                    return index;
                }

                var propertyMatch = PropertyAssignment.Match(line);
                if (!propertyMatch.Success)
                {
                    diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP1002", $"Invalid entity property syntax '{line}'.", sourceName, lineNumber, 1));
                    continue;
                }

                var propertyName = propertyMatch.Groups[1].Value;
                var propertyValue = ParseLiteral(propertyMatch.Groups[2].Value.Trim());
                declaration.Properties[propertyName] = propertyValue;
                entitySyntax.Properties.Add(new YuspecPropertySyntax(propertyName, propertyValue, new YuspecSourceLocation(sourceName, lineNumber, 1)));
            }

            diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP1003", $"Entity '{entityType}' is missing closing brace.", sourceName, startIndex, 1));
            spec.Entities.Add(declaration);
            spec.SyntaxTree.Entities.Add(entitySyntax);
            return lines.Length - 1;
        }

        private int ParseHandler(YuspecCompiledSpec spec, Match handlerMatch, string[] lines, int startIndex, string sourceName, int lineNumber)
        {
            var actorType = handlerMatch.Groups[1].Value;
            var eventName = $"{actorType}.{handlerMatch.Groups[2].Value}";
            var targetType = handlerMatch.Groups[3].Success ? handlerMatch.Groups[3].Value : string.Empty;
            var condition = handlerMatch.Groups[4].Success ? ParseCondition(handlerMatch.Groups[4].Value.Trim(), sourceName, lineNumber) : YuspecCondition.None;
            var handler = new YuspecEventHandler(actorType, eventName, targetType, condition, sourceName, lineNumber);
            var eventRuleSyntax = new YuspecEventRuleSyntax(
                actorType,
                eventName,
                targetType,
                handlerMatch.Groups[4].Success ? handlerMatch.Groups[4].Value.Trim() : string.Empty,
                new YuspecSourceLocation(sourceName, lineNumber, 1));

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
                    eventRuleSyntax.Actions.Add(CreateActionSyntax(action, trimmed, sourceName, index + 1));
                    lastActionIndex = index;
                }
            }

            if (handler.Actions.Count == 0)
            {
                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Warning, "YSP1004", $"Handler '{eventName}' has no actions.", sourceName, lineNumber, 1));
            }

            spec.EventHandlers.Add(handler);
            spec.SyntaxTree.EventRules.Add(eventRuleSyntax);
            return lastActionIndex;
        }

        private int ParseBehavior(YuspecCompiledSpec spec, Match behaviorMatch, string[] lines, int startIndex, string sourceName, int lineNumber)
        {
            var behaviorName = behaviorMatch.Groups[1].Value;
            var entityType = behaviorMatch.Groups[2].Value;
            var behaviorSyntax = new YuspecBehaviorSyntax(behaviorName, entityType, new YuspecSourceLocation(sourceName, lineNumber, 1));
            var behaviorDefinition = new YuspecBehaviorDefinition(behaviorName, entityType, lineNumber);

            for (var index = startIndex; index < lines.Length; index++)
            {
                var currentLine = StripComment(lines[index]).Trim();
                if (string.IsNullOrWhiteSpace(currentLine))
                {
                    continue;
                }

                if (currentLine == "}")
                {
                    spec.SyntaxTree.Behaviors.Add(behaviorSyntax);
                    spec.Behaviors.Add(behaviorDefinition);
                    return index;
                }

                var stateMatch = StateStart.Match(currentLine);
                if (stateMatch.Success)
                {
                    index = ParseState(behaviorSyntax, behaviorDefinition, stateMatch.Groups[1].Value, lines, index + 1, sourceName, index + 1);
                    continue;
                }

                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Warning, "YSP1006", $"Unsupported behavior syntax '{currentLine}'.", sourceName, index + 1, 1));
            }

            diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP1007", $"Behavior '{behaviorName}' is missing closing brace.", sourceName, lineNumber, 1));
            spec.SyntaxTree.Behaviors.Add(behaviorSyntax);
            spec.Behaviors.Add(behaviorDefinition);
            return lines.Length - 1;
        }

        private int ParseState(
            YuspecBehaviorSyntax behaviorSyntax,
            YuspecBehaviorDefinition behaviorDefinition,
            string stateName,
            string[] lines,
            int startIndex,
            string sourceName,
            int lineNumber)
        {
            var stateSyntax = new YuspecStateSyntax(stateName, new YuspecSourceLocation(sourceName, lineNumber, 1));
            var stateDefinition = new YuspecStateDefinition(stateName, lineNumber);

            for (var index = startIndex; index < lines.Length; index++)
            {
                var rawLine = lines[index];
                var currentLine = StripComment(rawLine).Trim();
                if (string.IsNullOrWhiteSpace(currentLine))
                {
                    continue;
                }

                if (currentLine == "}")
                {
                    behaviorSyntax.States.Add(stateSyntax);
                    behaviorDefinition.States.Add(stateDefinition);
                    return index;
                }

                if (string.Equals(currentLine, "on enter:", StringComparison.OrdinalIgnoreCase))
                {
                    index = ParseStateActionBlock(lines, index + 1, sourceName, stateSyntax.EnterActions, stateDefinition.EnterActions, out var lastIndex);
                    index = lastIndex;
                    continue;
                }

                if (string.Equals(currentLine, "on exit:", StringComparison.OrdinalIgnoreCase))
                {
                    index = ParseStateActionBlock(lines, index + 1, sourceName, stateSyntax.ExitActions, stateDefinition.ExitActions, out var lastIndex);
                    index = lastIndex;
                    continue;
                }

                if (string.Equals(currentLine, "do:", StringComparison.OrdinalIgnoreCase))
                {
                    index = ParseStateActionBlock(lines, index + 1, sourceName, stateSyntax.DoActions, stateDefinition.DoActions, out var lastIndex);
                    index = lastIndex;
                    continue;
                }

                var everyMatch = EveryStart.Match(currentLine);
                if (everyMatch.Success)
                {
                    var everySyntax = new YuspecEverySyntax(everyMatch.Groups[1].Value.Trim(), new YuspecSourceLocation(sourceName, index + 1, 1));
                    var timedBlock = new YuspecTimedActionBlock(everyMatch.Groups[1].Value.Trim(), index + 1);
                    index = ParseStateActionBlock(lines, index + 1, sourceName, everySyntax.Actions, timedBlock.Actions, out var lastIndex);
                    stateSyntax.EveryBlocks.Add(everySyntax);
                    stateDefinition.EveryBlocks.Add(timedBlock);
                    index = lastIndex;
                    continue;
                }

                var transitionMatch = TransitionLine.Match(currentLine);
                if (transitionMatch.Success)
                {
                    stateSyntax.Transitions.Add(new YuspecTransitionSyntax(
                        transitionMatch.Groups[1].Value.Trim(),
                        transitionMatch.Groups[2].Value.Trim(),
                        new YuspecSourceLocation(sourceName, index + 1, 1)));

                    stateDefinition.Transitions.Add(new YuspecTransitionDefinition(
                        transitionMatch.Groups[1].Value.Trim(),
                        transitionMatch.Groups[2].Value.Trim(),
                        index + 1));
                    continue;
                }

                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Warning, "YSP1008", $"Unsupported state syntax '{currentLine}'.", sourceName, index + 1, 1));
            }

            diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP1009", $"State '{stateName}' is missing closing brace.", sourceName, lineNumber, 1));
            behaviorSyntax.States.Add(stateSyntax);
            behaviorDefinition.States.Add(stateDefinition);
            return lines.Length - 1;
        }

        private int ParseScenario(YuspecCompiledSpec spec, Match scenarioMatch, string[] lines, int startIndex, string sourceName, int lineNumber)
        {
            var scenarioSyntax = new YuspecScenarioSyntax(
                scenarioMatch.Groups[1].Value,
                new YuspecSourceLocation(sourceName, lineNumber, 1));
            var scenarioDefinition = new YuspecScenarioDefinition(scenarioMatch.Groups[1].Value, lineNumber);

            for (var index = startIndex; index < lines.Length; index++)
            {
                var line = StripComment(lines[index]).Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line == "}")
                {
                    spec.SyntaxTree.Scenarios.Add(scenarioSyntax);
                    spec.Scenarios.Add(scenarioDefinition);
                    return index;
                }

                if (TryParseScenarioStep(line, "given", scenarioSyntax.GivenSteps, scenarioDefinition.GivenSteps, sourceName, index + 1) ||
                    TryParseScenarioStep(line, "when", scenarioSyntax.WhenSteps, scenarioDefinition.WhenSteps, sourceName, index + 1) ||
                    TryParseScenarioStep(line, "expect", scenarioSyntax.ExpectSteps, scenarioDefinition.ExpectSteps, sourceName, index + 1))
                {
                    continue;
                }

                diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Warning, "YSP1010", $"Unsupported scenario syntax '{line}'.", sourceName, index + 1, 1));
            }

            diagnostics.Add(new YuspecDiagnostic(YuspecDiagnosticSeverity.Error, "YSP1011", $"Scenario '{scenarioSyntax.Name}' is missing closing brace.", sourceName, lineNumber, 1));
            spec.SyntaxTree.Scenarios.Add(scenarioSyntax);
            spec.Scenarios.Add(scenarioDefinition);
            return lines.Length - 1;
        }

        private bool TryParseScenarioStep(
            string line,
            string keyword,
            List<YuspecScenarioStepSyntax> syntaxTarget,
            List<YuspecScenarioStepDefinition> compiledTarget,
            string sourceName,
            int lineNumber)
        {
            if (!line.StartsWith(keyword + " ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var stepText = line.Substring(keyword.Length).Trim();
            syntaxTarget.Add(new YuspecScenarioStepSyntax(stepText, new YuspecSourceLocation(sourceName, lineNumber, 1)));
            compiledTarget.Add(new YuspecScenarioStepDefinition(stepText, lineNumber));
            return true;
        }

        private int ParseStateActionBlock(
            string[] lines,
            int startIndex,
            string sourceName,
            List<YuspecActionSyntax> syntaxTarget,
            List<YuspecActionCall> compiledTarget,
            out int lastConsumedIndex)
        {
            lastConsumedIndex = startIndex - 1;

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
                    syntaxTarget.Add(CreateActionSyntax(action, trimmed, sourceName, index + 1));
                    compiledTarget.Add(action);
                    lastConsumedIndex = index;
                }
            }

            return lastConsumedIndex;
        }

        private static YuspecActionSyntax CreateActionSyntax(YuspecActionCall action, string rawActionText, string sourceName, int lineNumber)
        {
            if (action.IsSetAction)
            {
                var setArguments = new[]
                {
                    action.TargetEntity,
                    action.TargetProperty,
                    action.AssignedValue
                };

                return new YuspecActionSyntax(action.Name, rawActionText, setArguments, new YuspecSourceLocation(sourceName, lineNumber, 1));
            }

            return new YuspecActionSyntax(action.Name, rawActionText, action.Arguments.ToList(), new YuspecSourceLocation(sourceName, lineNumber, 1));
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
            if (text.StartsWith("[", StringComparison.Ordinal) && text.EndsWith("]", StringComparison.Ordinal))
            {
                var inner = text.Substring(1, text.Length - 2).Trim();
                var items = new List<string>();
                if (string.IsNullOrWhiteSpace(inner))
                {
                    return items;
                }

                foreach (var item in SplitListItems(inner))
                {
                    var parsedItem = ParseLiteral(item);
                    items.Add(parsedItem?.ToString() ?? string.Empty);
                }

                return items;
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

        private static List<string> SplitListItems(string text)
        {
            var items = new List<string>();
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

                if (character == ',' && !inString)
                {
                    AddToken(items, builder);
                    continue;
                }

                builder.Append(character);
            }

            AddToken(items, builder);
            return items;
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

        private static string StripComment(string line)
        {
            var inString = false;

            for (var index = 0; index < line.Length; index++)
            {
                var character = line[index];
                if (character == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (character == '#')
                {
                    return line.Substring(0, index);
                }

                if (character == '/' && index + 1 < line.Length && line[index + 1] == '/')
                {
                    return line.Substring(0, index);
                }
            }

            return line;
        }
    }
}
