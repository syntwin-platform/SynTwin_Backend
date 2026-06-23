using System.Globalization;
using System.Text.RegularExpressions;
using Syntwin.Application.LuaParsing.Interfaces;
using Syntwin.Application.LuaParsing.Models;

namespace Syntwin.Application.LuaParsing.Services;

public sealed class LuaProgramParser : ILuaProgramParser
{
    public LuaParseResult Parse(string luaContent, string? fileName = null)
    {
        var result = new LuaParseResult();

        if (string.IsNullOrWhiteSpace(luaContent))
        {
            result.Diagnostics.Add(CreateDiagnostic(
                1,
                "error",
                "The LUA file is empty.",
                string.Empty));

            return result;
        }

        var lines = NormalizeText(luaContent).Split('\n');

        var currentLabel = string.Empty;
        var currentComment = string.Empty;
        var insideToDoubleHelper = false;
        var insideIgnoredFunction = false;
        var gripperHint = false;
        var hasSeenWorkflowCommand = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var source = lines[index];
            var line = source.Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var projectMatch = ProjectNameRegex.Match(line);
            if (projectMatch.Success)
            {
                result.Metadata.ProjectName = projectMatch.Groups[1].Value.Trim();
                continue;
            }

            var metadataMatch = MetadataRegex.Match(line);
            if (metadataMatch.Success && !hasSeenWorkflowCommand && string.IsNullOrWhiteSpace(currentLabel))
            {
                ApplyMetadata(result.Metadata, metadataMatch.Groups[1].Value, metadataMatch.Groups[2].Value);
                continue;
            }

            var stepLabelMatch = StepLabelRegex.Match(line);
            if (stepLabelMatch.Success)
            {
                currentLabel = stepLabelMatch.Groups[1].Value.Trim();
                currentComment = string.Empty;
                continue;
            }

            var noteMatch = NoteRegex.Match(line);
            if (noteMatch.Success)
            {
                currentComment = noteMatch.Groups[1].Value.Trim();
                continue;
            }

            if (ToDoubleFunctionStartRegex.IsMatch(line))
            {
                insideToDoubleHelper = true;
                continue;
            }

            if (insideToDoubleHelper)
            {
                if (EndStatementRegex.IsMatch(line))
                {
                    insideToDoubleHelper = false;
                }

                continue;
            }

            if (FunctionStartRegex.IsMatch(line))
            {
                insideIgnoredFunction = true;
                continue;
            }

            if (insideIgnoredFunction)
            {
                if (EndStatementRegex.IsMatch(line))
                {
                    insideIgnoredFunction = false;
                }

                continue;
            }

            var pointStartMatch = PointDefinitionStartRegex.Match(line);
            if (pointStartMatch.Success)
            {
                var pointName = pointStartMatch.Groups[1].Value.Trim();
                var tableBlock = CollectTableBlock(lines, ref index);

                if (TryParsePointDefinition(pointName, tableBlock, result.Variables, out var point))
                {
                    result.Points[pointName] = point;
                    continue;
                }

                AddError(result, index, $"Point definition '{pointName}' is malformed.", tableBlock);
                continue;
            }

            var variableLine = NormalizeNumbers(line);
            var variableMatch = ScalarVariableRegex.Match(variableLine);
            if (variableMatch.Success)
            {
                var name = variableMatch.Groups[1].Value.Trim();
                var rawValue = variableMatch.Groups[2].Value.Trim();

                if (TryParseScalarValue(rawValue, out var scalarValue))
                {
                    result.Variables[name] = scalarValue;
                    continue;
                }
            }

            if (line.StartsWith("--", StringComparison.Ordinal))
            {
                if (GripperHintRegex.IsMatch(line))
                {
                    gripperHint = true;
                }

                continue;
            }

            var normalizedLine = NormalizeNumbers(line);

            if (gripperHint)
            {
                var gripperMatch = GripperSetDoRegex.Match(normalizedLine);
                var nextLine = index + 1 < lines.Length
                    ? NormalizeNumbers(lines[index + 1].Trim())
                    : string.Empty;

                if (gripperMatch.Success && Wait500Regex.IsMatch(nextLine))
                {
                    var isClosed = gripperMatch.Groups[1].Value == "1";

                    hasSeenWorkflowCommand = true;
                    AddStep(
                        result,
                        isClosed ? "GripperClose" : "GripperOpen",
                        currentLabel.Length > 0
                            ? currentLabel
                            : isClosed ? "Close gripper" : "Open gripper",
                        currentComment,
                        new { },
                        normalizedLine);

                    currentLabel = string.Empty;
                    currentComment = string.Empty;
                    gripperHint = false;
                    index += 1;
                    continue;
                }
            }
            var moveJPointMatch = MoveJPointRefRegex.Match(normalizedLine);
            if (moveJPointMatch.Success)
            {
                var pointRef = moveJPointMatch.Groups[1].Value.Trim();
                var speed = ParseNumber(moveJPointMatch.Groups[2].Value, result.Variables);
                var acc = ParseNumber(moveJPointMatch.Groups[3].Value, result.Variables);

                if (!result.Points.TryGetValue(pointRef, out var point) ||
                    point.Kind != "joint" ||
                    point.JointAngles is null)
                {
                    AddError(result, index, $"MoveJ pointRef '{pointRef}' could not be resolved to a joint point.", source);
                    continue;
                }

                if (!IsValidPercent(speed) || !IsValidPercent(acc))
                {
                    AddError(result, index, "MoveJ speed and acc must be between 1 and 100.", source);
                    continue;
                }

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    "MoveJ",
                    currentLabel.Length > 0 ? currentLabel : $"MoveJ {pointRef}",
                    currentComment,
                    new
                    {
                        pointRef,
                        jointAngles = point.JointAngles,
                        speed,
                        acc
                    },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            var compactMoveJMatch = CompactMoveJRegex.Match(normalizedLine);
            if (compactMoveJMatch.Success)
            {
                var joints = ParseNumberList(compactMoveJMatch.Groups[1].Value, result.Variables);
                var speed = ParseNumber(compactMoveJMatch.Groups[2].Value, result.Variables);

                if (joints is null || joints.Length != 6)
                {
                    AddError(result, index, "MoveJ requires exactly 6 joint values.", source);
                    continue;
                }

                if (!IsValidPercent(speed))
                {
                    AddError(result, index, "MoveJ speed must be between 1 and 100.", source);
                    continue;
                }

                var speedValue = speed.GetValueOrDefault();

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    "MoveJ",
                    currentLabel.Length > 0 ? currentLabel : "MoveJ",
                    currentComment,
                    new
                    {
                        jointAngles = joints,
                        speed = speedValue,
                        acc = speedValue
                    },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            var moveJMatch = MoveJRegex.Match(normalizedLine);
            if (moveJMatch.Success)
            {
                var joints = ParseNumberList(moveJMatch.Groups[1].Value, result.Variables);
                var speed = ParseNumber(moveJMatch.Groups[2].Value, result.Variables);
                var acc = ParseNumber(moveJMatch.Groups[3].Value, result.Variables);

                if (joints is null || joints.Length != 6)
                {
                    AddError(result, index, "MoveJ requires exactly 6 joint values.", source);
                    continue;
                }

                if (!IsValidPercent(speed) || !IsValidPercent(acc))
                {
                    AddError(result, index, "MoveJ speed and acc must be between 1 and 100.", source);
                    continue;
                }

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    "MoveJ",
                    currentLabel.Length > 0 ? currentLabel : "MoveJ",
                    currentComment,
                    new
                    {
                        jointAngles = joints,
                        speed,
                        acc
                    },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            var moveLPointMatch = MoveLPointRefRegex.Match(normalizedLine);
            if (moveLPointMatch.Success)
            {
                var pointRef = moveLPointMatch.Groups[1].Value.Trim();
                var speed = ParseNumber(moveLPointMatch.Groups[2].Value, result.Variables);
                var acc = ParseNumber(moveLPointMatch.Groups[3].Value, result.Variables);

                if (!result.Points.TryGetValue(pointRef, out var point) ||
                    point.Kind != "tcp" ||
                    point.TcpPose is null)
                {
                    AddError(result, index, $"MoveL pointRef '{pointRef}' could not be resolved to a TCP point.", source);
                    continue;
                }

                if (!IsValidPercent(speed) || !IsValidPercent(acc))
                {
                    AddError(result, index, "MoveL speed and acc must be between 1 and 100.", source);
                    continue;
                }

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    "MoveL",
                    currentLabel.Length > 0 ? currentLabel : $"MoveL {pointRef}",
                    currentComment,
                    new
                    {
                        pointRef,
                        tcpPose = new
                        {
                            x = point.TcpPose.X,
                            y = point.TcpPose.Y,
                            z = point.TcpPose.Z,
                            rx = point.TcpPose.Rx,
                            ry = point.TcpPose.Ry,
                            rz = point.TcpPose.Rz
                        },
                        speed,
                        acc
                    },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            var compactMoveLMatch = CompactMoveLRegex.Match(normalizedLine);
            if (compactMoveLMatch.Success)
            {
                var pose = ParseNumberList(compactMoveLMatch.Groups[1].Value, result.Variables);
                var speed = ParseNumber(compactMoveLMatch.Groups[2].Value, result.Variables);

                if (pose is null || pose.Length != 6)
                {
                    AddError(result, index, "MoveL requires exactly 6 TCP values.", source);
                    continue;
                }

                if (!IsValidPercent(speed))
                {
                    AddError(result, index, "MoveL speed must be between 1 and 100.", source);
                    continue;
                }

                var speedValue = speed.GetValueOrDefault();

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    "MoveL",
                    currentLabel.Length > 0 ? currentLabel : "MoveL",
                    currentComment,
                    new
                    {
                        tcpPose = new
                        {
                            x = pose[0],
                            y = pose[1],
                            z = pose[2],
                            rx = pose[3],
                            ry = pose[4],
                            rz = pose[5]
                        },
                        speed = speedValue,
                        acc = speedValue
                    },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            var moveLMatch = MoveLRegex.Match(normalizedLine);
            if (moveLMatch.Success)
            {
                var pose = ParseNumberList(moveLMatch.Groups[1].Value, result.Variables);
                var speed = ParseNumber(moveLMatch.Groups[2].Value, result.Variables);
                var acc = ParseNumber(moveLMatch.Groups[3].Value, result.Variables);

                if (pose is null || pose.Length != 6)
                {
                    AddError(result, index, "MoveL requires exactly 6 TCP values.", source);
                    continue;
                }

                if (!IsValidPercent(speed) || !IsValidPercent(acc))
                {
                    AddError(result, index, "MoveL speed and acc must be between 1 and 100.", source);
                    continue;
                }

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    "MoveL",
                    currentLabel.Length > 0 ? currentLabel : "MoveL",
                    currentComment,
                    new
                    {
                        tcpPose = new
                        {
                            x = pose[0],
                            y = pose[1],
                            z = pose[2],
                            rx = pose[3],
                            ry = pose[4],
                            rz = pose[5]
                        },
                        speed,
                        acc
                    },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            var setToolDoMatch = SetToolDoRegex.Match(normalizedLine);
            if (setToolDoMatch.Success)
            {
                var doIndex = int.Parse(setToolDoMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                var doValue = int.Parse(setToolDoMatch.Groups[2].Value, CultureInfo.InvariantCulture);

                if (doIndex is < 0 or > 1)
                {
                    AddError(result, index, "Tool DO index must be between 0 and 1.", source);
                    continue;
                }

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    "SetDO",
                    currentLabel.Length > 0 ? currentLabel : $"Set tool DO {doIndex}",
                    currentComment,
                    new
                    {
                        doType = "tool",
                        doIndex,
                        doValue
                    },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            var setDoMatch = SetDoRegex.Match(normalizedLine);
            if (setDoMatch.Success)
            {
                var doIndex = int.Parse(setDoMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                var doValue = int.Parse(setDoMatch.Groups[2].Value, CultureInfo.InvariantCulture);

                if (doIndex is < 1 or > 8)
                {
                    AddError(result, index, "Cabinet DO index must be between 1 and 8.", source);
                    continue;
                }

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    "SetDO",
                    currentLabel.Length > 0 ? currentLabel : $"Set cabinet DO {doIndex}",
                    currentComment,
                    new
                    {
                        doType = "cabinet",
                        doIndex,
                        doValue
                    },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            var setAoMatch = SetAoRegex.Match(normalizedLine);
            if (setAoMatch.Success)
            {
                var aoIndex = int.Parse(setAoMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                var aoValue = ParseNumber(setAoMatch.Groups[2].Value, result.Variables);

                if (aoIndex is < 0 or > 1)
                {
                    AddError(result, index, "Analog output index must be between 0 and 1.", source);
                    continue;
                }

                if (!aoValue.HasValue)
                {
                    AddError(result, index, "SetAO value must be a finite number.", source);
                    continue;
                }

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    "SetAO",
                    currentLabel.Length > 0 ? currentLabel : $"Set analog output {aoIndex}",
                    currentComment,
                    new
                    {
                        aoIndex,
                        aoValue = aoValue.Value
                    },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            var waitAliasMatch = WaitAliasRegex.Match(normalizedLine);
            if (waitAliasMatch.Success)
            {
                var delayValue = ParseNumber(waitAliasMatch.Groups[1].Value, result.Variables);

                if (!delayValue.HasValue || delayValue.Value < 0)
                {
                    AddError(result, index, "Wait delayMs must be greater than or equal to 0.", source);
                    continue;
                }

                var delayMs = (int)Math.Round(delayValue.Value);

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    "WaitMs",
                    currentLabel.Length > 0 ? currentLabel : $"Wait {delayMs} ms",
                    currentComment,
                    new
                    {
                        delayMs
                    },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            var waitMatch = WaitMsRegex.Match(normalizedLine);
            if (waitMatch.Success)
            {
                var delayValue = ParseNumber(waitMatch.Groups[1].Value, result.Variables);

                if (!delayValue.HasValue || delayValue.Value < 0)
                {
                    AddError(result, index, "WaitMs delayMs must be greater than or equal to 0.", source);
                    continue;
                }

                var delayMs = (int)Math.Round(delayValue.Value);

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    "WaitMs",
                    currentLabel.Length > 0 ? currentLabel : $"Wait {delayMs} ms",
                    currentComment,
                    new
                    {
                        delayMs
                    },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            var gripperWrapperMatch = GripperWrapperRegex.Match(normalizedLine);
            if (gripperWrapperMatch.Success)
            {
                var action = gripperWrapperMatch.Groups[1].Value.Trim().ToLowerInvariant();
                var stepType = action == "open" ? "GripperOpen" : "GripperClose";

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    stepType,
                    currentLabel.Length > 0 ? currentLabel : stepType,
                    currentComment,
                    new { },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            var customCommandMatch = CustomCommandRegex.Match(normalizedLine);
            if (customCommandMatch.Success)
            {
                var commandName = customCommandMatch.Groups[1].Value.Trim();
                var rawArgs = customCommandMatch.Groups[2].Value.Trim();

                hasSeenWorkflowCommand = true;
                AddStep(
                    result,
                    "CustomCommand",
                    currentLabel.Length > 0 ? currentLabel : commandName,
                    currentComment,
                    new
                    {
                        raw = normalizedLine,
                        commandName,
                        args = SplitArguments(rawArgs)
                    },
                    normalizedLine);

                currentLabel = string.Empty;
                currentComment = string.Empty;
                gripperHint = false;
                continue;
            }

            hasSeenWorkflowCommand = true;
            AddError(result, index, "Unsupported or malformed LUA statement.", source);
        }

        if (insideToDoubleHelper)
        {
            result.Diagnostics.Add(CreateDiagnostic(
                lines.Length,
                "error",
                "The toDouble helper function is missing its closing end.",
                lines.Length > 0 ? lines[^1] : string.Empty));
        }

        return result;
    }

    private static string NormalizeText(string luaContent)
    {
        return luaContent
            .TrimStart('\uFEFF')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    private static string NormalizeNumbers(string line)
    {
        return ToDoubleValueRegex.Replace(line, "$2");
    }

    private static double? ParseNumber(
        string value,
        IReadOnlyDictionary<string, object?>? variables = null)
    {
        var normalized = value.Trim();

        if (double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number))
        {
            return number;
        }

        if (variables is not null &&
            variables.TryGetValue(normalized, out var variableValue) &&
            variableValue is double variableNumber)
        {
            return variableNumber;
        }

        return null;
    }

    private static double[]? ParseNumberList(
        string value,
        IReadOnlyDictionary<string, object?>? variables = null)
    {
        var parts = value.Split(
     ',',
     StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var numbers = new List<double>();

        foreach (var part in parts)
        {
            var number = ParseNumber(part, variables);

            if (!number.HasValue)
            {
                return null;
            }

            numbers.Add(number.Value);
        }

        return numbers.ToArray();
    }

    private static bool IsValidPercent(double? value)
    {
        return value.HasValue && value.Value >= 1 && value.Value <= 100;
    }

    private static void AddStep(
        LuaParseResult result,
        string stepType,
        string label,
        string? comment,
        object payload,
        string raw)
    {
        var finalPayload = string.IsNullOrWhiteSpace(comment)
            ? payload
            : MergeComment(payload, comment);

        result.Steps.Add(new LuaParsedStep
        {
            OrderIndex = result.Steps.Count + 1,
            StepType = stepType,
            Label = label,
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(finalPayload),
            Raw = raw
        });
    }

    private static object MergeComment(object payload, string comment)
    {
        var json = System.Text.Json.JsonSerializer.SerializeToElement(payload);
        var values = new Dictionary<string, object?>();

        foreach (var property in json.EnumerateObject())
        {
            values[property.Name] = property.Value.Clone();
        }

        values["comment"] = comment;
        return values;
    }

    private static void AddError(
        LuaParseResult result,
        int lineIndex,
        string message,
        string source)
    {
        result.Diagnostics.Add(CreateDiagnostic(
            lineIndex + 1,
            "error",
            message,
            source));
    }

    private static LuaParseDiagnostic CreateDiagnostic(
        int line,
        string severity,
        string message,
        string source)
    {
        return new LuaParseDiagnostic
        {
            Line = line,
            Severity = severity,
            Message = message,
            Source = source
        };
    }

    private static void ApplyMetadata(
        LuaProgramMetadata metadata,
        string rawKey,
        string rawValue)
    {
        var key = NormalizeMetadataKey(rawKey);
        var value = rawValue.Trim();

        switch (key)
        {
            case "projectname":
                metadata.ProjectName = value;
                break;

            case "robotmodel":
                metadata.RobotModel = value;
                break;

            case "date":
                metadata.Date = value;
                break;

            case "note":
                metadata.Note = value;
                break;

            case "author":
                metadata.Author = value;
                break;

            case "version":
                metadata.Version = value;
                break;
        }
    }

    private static string NormalizeMetadataKey(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static bool TryParseScalarValue(string rawValue, out object? value)
    {
        var normalized = rawValue.Trim();

        if ((normalized.StartsWith("\"", StringComparison.Ordinal) &&
             normalized.EndsWith("\"", StringComparison.Ordinal)) ||
            (normalized.StartsWith("'", StringComparison.Ordinal) &&
             normalized.EndsWith("'", StringComparison.Ordinal)))
        {
            value = normalized[1..^1];
            return true;
        }

        if (bool.TryParse(normalized, out var boolean))
        {
            value = boolean;
            return true;
        }

        var number = ParseNumber(normalized);
        if (number.HasValue)
        {
            value = number.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static string CollectTableBlock(string[] lines, ref int index)
    {
        var blockLines = new List<string>();
        var braceDepth = 0;

        for (; index < lines.Length; index++)
        {
            var current = lines[index];
            blockLines.Add(current);

            braceDepth += current.Count(character => character == '{');
            braceDepth -= current.Count(character => character == '}');

            if (braceDepth <= 0 && blockLines.Count > 0)
            {
                break;
            }
        }

        return string.Join('\n', blockLines);
    }

    private static bool TryParsePointDefinition(
        string pointName,
        string tableBlock,
        IReadOnlyDictionary<string, object?> variables,
        out LuaRobotPoint point)
    {
        point = new LuaRobotPoint
        {
            Name = pointName
        };

        var normalizedBlock = NormalizeNumbers(tableBlock);
        var openBraceIndex = normalizedBlock.IndexOf('{');
        var closeBraceIndex = normalizedBlock.LastIndexOf('}');

        if (openBraceIndex < 0 || closeBraceIndex <= openBraceIndex)
        {
            return false;
        }

        var body = RemoveLineComments(
            normalizedBlock[(openBraceIndex + 1)..closeBraceIndex]);
        if (TryParseTcpPoint(pointName, body, variables, out point))
        {
            return true;
        }

        var jointValues = ParseNumberList(body, variables);
        if (jointValues is { Length: 6 })
        {
            point = new LuaRobotPoint
            {
                Name = pointName,
                Kind = "joint",
                Unit = "deg",
                JointAngles = jointValues
            };

            return true;
        }

        return false;
    }

    private static bool TryParseTcpPoint(
        string pointName,
        string body,
        IReadOnlyDictionary<string, object?> variables,
        out LuaRobotPoint point)
    {
        point = new LuaRobotPoint
        {
            Name = pointName
        };

        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in TcpFieldRegex.Matches(body))
        {
            var key = match.Groups[1].Value.Trim();
            var rawValue = match.Groups[2].Value.Trim();
            var number = ParseNumber(rawValue, variables);

            if (number.HasValue)
            {
                values[key] = number.Value;
            }
        }

        var requiredKeys = new[] { "x", "y", "z", "rx", "ry", "rz" };

        if (requiredKeys.Any(key => !values.ContainsKey(key)))
        {
            return false;
        }

        point = new LuaRobotPoint
        {
            Name = pointName,
            Kind = "tcp",
            Unit = "mm/deg",
            TcpPose = new LuaTcpPose
            {
                X = values["x"],
                Y = values["y"],
                Z = values["z"],
                Rx = values["rx"],
                Ry = values["ry"],
                Rz = values["rz"]
            }
        };

        return true;
    }

    private static string RemoveLineComments(string value)
    {
        var lines = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n');

        return string.Join(
            '\n',
            lines.Select(line =>
            {
                var commentIndex = line.IndexOf("--", StringComparison.Ordinal);
                return commentIndex >= 0 ? line[..commentIndex] : line;
            }));
    }

    private static string[] SplitArguments(string rawArgs)
    {
        if (string.IsNullOrWhiteSpace(rawArgs))
        {
            return Array.Empty<string>();
        }

        return rawArgs
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
    }

    private static readonly Regex ToDoubleValueRegex = new(
        """toDouble\(\s*(['"])([+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\1\s*\)""",
        RegexOptions.Compiled);

    private static readonly Regex ProjectNameRegex = new(
        """^--\s*Project Name\s*:\s*(.+)$""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StepLabelRegex = new(
        """^--\s*\[[^\]]*\d+\]\s*(.+)$""",
        RegexOptions.Compiled);

    private static readonly Regex NoteRegex = new(
        """^--\s*(?:Ghi\s+ch\S*|Note)\s*:\s*(.+)$""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ToDoubleFunctionStartRegex = new(
        """^local\s+function\s+toDouble\s*\(""",
        RegexOptions.Compiled);

    private static readonly Regex EndStatementRegex = new(
        """^end\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex GripperHintRegex = new(
        """tay\s+g|gripper""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GripperSetDoRegex = new(
        """^SetDO\(\s*1\s*,\s*([01])(?:\s*,.*)?\)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex Wait500Regex = new(
        """^WaitMs\(\s*500\s*\)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex MoveJRegex = new(
        """^MoveJ\(\s*\{([^}]*)\}\s*,\s*[^,]+,\s*[^,]+,\s*([^,]+),\s*([^,]+)(?:,.*)?\)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex CompactMoveJRegex = new(
        """^MoveJ\(\s*\{([^}]*)\}\s*,\s*([^,\)]+)\s*,\s*[^,\)]*\)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex MoveLRegex = new(
        """^MoveL\(\s*\{([^}]*)\}\s*,\s*[^,]+,\s*[^,]+,\s*([^,]+),\s*([^,]+)(?:,.*)?\)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex CompactMoveLRegex = new(
        """^MoveL\(\s*\{([^}]*)\}\s*,\s*([^,\)]+)\s*,\s*[^,\)]*\)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex SetToolDoRegex = new(
        """^SetToolDO\(\s*(\d+)\s*,\s*([01])(?:\s*,.*)?\)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex SetDoRegex = new(
        """^SetDO\(\s*(\d+)\s*,\s*([01])(?:\s*,.*)?\)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex WaitMsRegex = new(
        """^WaitMs\(\s*([A-Za-z_][A-Za-z0-9_]*|\d+)\s*\)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex WaitAliasRegex = new(
        """^Wait\(\s*([A-Za-z_][A-Za-z0-9_]*|\d+)\s*\)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex MetadataRegex = new(
        """^--\s*([A-Za-z ]+)\s*:\s*(.+)$""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ScalarVariableRegex = new(
        """^(?:local\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+?)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex PointDefinitionStartRegex = new(
        """^(?:local\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*\{""",
        RegexOptions.Compiled);

    private static readonly Regex MoveJPointRefRegex = new(
        """^MoveJ\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*,\s*[^,]+,\s*[^,]+,\s*([^,]+),\s*([^,]+)(?:,.*)?\)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex MoveLPointRefRegex = new(
        """^MoveL\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*,\s*[^,]+,\s*[^,]+,\s*([^,]+),\s*([^,]+)(?:,.*)?\)\s*;?$""",
        RegexOptions.Compiled);

    private static readonly Regex TcpFieldRegex = new(
        """([A-Za-z_][A-Za-z0-9_]*)\s*=\s*([^,\r\n}]+)""",
        RegexOptions.Compiled);

    private static readonly Regex CustomCommandRegex = new(
     """^([A-Za-z_][A-Za-z0-9_]*)\((.*)\)\s*;?$""",
     RegexOptions.Compiled);

    private static readonly Regex FunctionStartRegex = new(
    """^(?:local\s+)?function\s+[A-Za-z_][A-Za-z0-9_]*\s*\(""",
    RegexOptions.Compiled);

    private static readonly Regex GripperWrapperRegex = new(
        """^gripper(Open|Close)\(\s*\)\s*;?$""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SetAoRegex = new(
    """^SetAO\(\s*(\d+)\s*,\s*([A-Za-z_][A-Za-z0-9_]*|[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*\)\s*;?$""",
    RegexOptions.Compiled);
}
