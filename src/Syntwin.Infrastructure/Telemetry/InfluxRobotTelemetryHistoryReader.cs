using System.Globalization;
using System.Text;
using InfluxDB.Client;
using Microsoft.Extensions.Options;
using Syntwin.Application.Robots.Dtos;
using Syntwin.Application.Telemetry.Dtos;
using Syntwin.Application.Telemetry.Interfaces;

namespace Syntwin.Infrastructure.Telemetry;

public sealed class InfluxRobotTelemetryHistoryReader : IRobotTelemetryHistoryReader, IDisposable
{
    private const string MeasurementName = "robot_telemetry";

    private readonly InfluxDbOptions _options;
    private readonly InfluxDBClient? _client;

    public InfluxRobotTelemetryHistoryReader(IOptions<InfluxDbOptions> options)
    {
        _options = options.Value;

        if (!_options.Enabled ||
            string.IsNullOrWhiteSpace(_options.Url) ||
            string.IsNullOrWhiteSpace(_options.Token) ||
            string.IsNullOrWhiteSpace(_options.Org) ||
            string.IsNullOrWhiteSpace(_options.Bucket))
        {
            return;
        }

        _client = new InfluxDBClient(
            _options.Url,
            _options.Token);
    }

    public async Task<IReadOnlyList<RobotTelemetryHistoryPoint>> QueryAsync(
        RobotTelemetryHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            return Array.Empty<RobotTelemetryHistoryPoint>();
        }

        if (query.RobotId == Guid.Empty)
        {
            return Array.Empty<RobotTelemetryHistoryPoint>();
        }

        if (query.To <= query.From)
        {
            return Array.Empty<RobotTelemetryHistoryPoint>();
        }

        var flux = BuildFluxQuery(query);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeoutCts.CancelAfter(TimeSpan.FromSeconds(
            Math.Max(1, _options.QueryTimeoutSeconds)));

        var tables = await _client.GetQueryApi().QueryAsync(
            flux,
            _options.Org,
            timeoutCts.Token);

        return MapTables(tables, query.Limit);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    private string BuildFluxQuery(RobotTelemetryHistoryQuery query)
    {
        var from = query.From.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var to = query.To.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var limit = Math.Clamp(query.Limit, 1, 10000);

        var builder = new StringBuilder();

        builder.AppendLine($"from(bucket: \"{EscapeFluxString(_options.Bucket)}\")");
        builder.AppendLine($"  |> range(start: time(v: \"{from}\"), stop: time(v: \"{to}\"))");
        builder.AppendLine($"  |> filter(fn: (r) => r._measurement == \"{MeasurementName}\")");
        builder.AppendLine($"  |> filter(fn: (r) => r.robotId == \"{query.RobotId}\")");

        var fields = GetSelectedFields(query.Fields);

        builder.AppendLine(
            $"  |> filter(fn: (r) => {BuildFieldFilter(fields)})");

        if (query.Interval.HasValue && query.Interval.Value > TimeSpan.Zero)
        {
            builder.AppendLine(
                $"  |> aggregateWindow(every: {ToFluxDuration(query.Interval.Value)}, fn: mean, createEmpty: false)");
        }

        builder.AppendLine("  |> pivot(rowKey:[\"_time\"], columnKey:[\"_field\"], valueColumn:\"_value\")");
        builder.AppendLine("  |> sort(columns: [\"_time\"], desc: false)");
        builder.AppendLine($"  |> limit(n: {limit})");

        return builder.ToString();
    }

    private static IReadOnlyList<RobotTelemetryHistoryPoint> MapTables(
        IReadOnlyList<InfluxDB.Client.Core.Flux.Domain.FluxTable> tables,
        int requestedLimit)
    {
        var limit = Math.Clamp(requestedLimit, 1, 10000);
        var points = new List<RobotTelemetryHistoryPoint>();

        foreach (var record in tables.SelectMany(table => table.Records))
        {
            if (points.Count >= limit)
            {
                break;
            }

            var timestamp = record.GetTimeInDateTime();

            if (!timestamp.HasValue)
            {
                continue;
            }

            points.Add(new RobotTelemetryHistoryPoint
            {
                Timestamp = new DateTimeOffset(timestamp.Value, TimeSpan.Zero),
                JointAngles = ReadJointAngles(record),
                TcpPose = ReadTcpPose(record),
                Temperature = ReadNullableDouble(record, "temperature"),
                CollisionWarning = ReadNullableBool(record, "collision_warning"),
                Status = ReadNullableString(record, "status_code"),
                Source = ReadNullableString(record, "source")
            });
        }

        return points;
    }

    private static IReadOnlyList<double> ReadJointAngles(
        InfluxDB.Client.Core.Flux.Domain.FluxRecord record)
    {
        var values = new[]
        {
            ReadNullableDouble(record, "joint1"),
            ReadNullableDouble(record, "joint2"),
            ReadNullableDouble(record, "joint3"),
            ReadNullableDouble(record, "joint4"),
            ReadNullableDouble(record, "joint5"),
            ReadNullableDouble(record, "joint6")
        };

        return values.All(value => value.HasValue)
            ? values.Select(value => value!.Value).ToArray()
            : Array.Empty<double>();
    }

    private static TcpPoseDto? ReadTcpPose(
        InfluxDB.Client.Core.Flux.Domain.FluxRecord record)
    {
        var x = ReadNullableDouble(record, "tcp_x");
        var y = ReadNullableDouble(record, "tcp_y");
        var z = ReadNullableDouble(record, "tcp_z");
        var rx = ReadNullableDouble(record, "tcp_rx");
        var ry = ReadNullableDouble(record, "tcp_ry");
        var rz = ReadNullableDouble(record, "tcp_rz");

        if (!x.HasValue ||
            !y.HasValue ||
            !z.HasValue ||
            !rx.HasValue ||
            !ry.HasValue ||
            !rz.HasValue)
        {
            return null;
        }

        return new TcpPoseDto
        {
            X = x.Value,
            Y = y.Value,
            Z = z.Value,
            Rx = rx.Value,
            Ry = ry.Value,
            Rz = rz.Value
        };
    }

    private static double? ReadNullableDouble(
        InfluxDB.Client.Core.Flux.Domain.FluxRecord record,
        string key)
    {
        if (!record.Values.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            long longValue => longValue,
            decimal decimalValue => (double)decimalValue,
            _ when double.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null
        };
    }

    private static bool? ReadNullableBool(
        InfluxDB.Client.Core.Flux.Domain.FluxRecord record,
        string key)
    {
        if (!record.Values.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            bool boolValue => boolValue,
            _ when bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) => parsed,
            _ => null
        };
    }

    private static string? ReadNullableString(
        InfluxDB.Client.Core.Flux.Domain.FluxRecord record,
        string key)
    {
        return record.Values.TryGetValue(key, out var value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;
    }

    private static IReadOnlyList<string> GetSelectedFields(IReadOnlyList<string> requestedFields)
    {
        var allowedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "joint1",
            "joint2",
            "joint3",
            "joint4",
            "joint5",
            "joint6",
            "tcp_x",
            "tcp_y",
            "tcp_z",
            "tcp_rx",
            "tcp_ry",
            "tcp_rz",
            "temperature",
            "collision_warning",
            "status_code"
        };

        var fields = requestedFields
            .Where(field => allowedFields.Contains(field))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return fields.Length > 0
            ? fields
            : allowedFields.ToArray();
    }

    private static string BuildFieldFilter(IReadOnlyList<string> fields)
    {
        return string.Join(
            " or ",
            fields.Select(field => $"r._field == \"{field}\""));
    }

    private static string ToFluxDuration(TimeSpan interval)
    {
        if (interval.TotalDays >= 1 && interval.TotalDays % 1 == 0)
        {
            return $"{(int)interval.TotalDays}d";
        }

        if (interval.TotalHours >= 1 && interval.TotalHours % 1 == 0)
        {
            return $"{(int)interval.TotalHours}h";
        }

        if (interval.TotalMinutes >= 1 && interval.TotalMinutes % 1 == 0)
        {
            return $"{(int)interval.TotalMinutes}m";
        }

        return $"{Math.Max(1, (int)interval.TotalSeconds)}s";
    }

    private static string EscapeFluxString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}