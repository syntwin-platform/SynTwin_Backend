param(
    [string]$ApiBaseUrl = "http://localhost:5200",
    [Parameter(Mandatory = $true)]
    [string]$DevicesCsv,
    [int]$RobotCount = 10,
    [int]$DurationSeconds = 120,
    [int]$HeartbeatIntervalSeconds = 10,
    [int]$TelemetryIntervalMilliseconds = 1000,
    [int]$CommandPollWaitSeconds = 25
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $DevicesCsv)) {
    throw "Devices CSV not found: $DevicesCsv"
}

$devices = Import-Csv -LiteralPath $DevicesCsv | Select-Object -First $RobotCount

if ($devices.Count -eq 0) {
    throw "Devices CSV is empty. Required columns: RobotId,DeviceSecret"
}

foreach ($device in $devices) {
    if ([string]::IsNullOrWhiteSpace($device.RobotId) -or [string]::IsNullOrWhiteSpace($device.DeviceSecret)) {
        throw "Each CSV row must include RobotId and DeviceSecret."
    }
}

Write-Host "Starting robot runtime load test"
Write-Host "API: $ApiBaseUrl"
Write-Host "Robots: $($devices.Count)"
Write-Host "DurationSeconds: $DurationSeconds"
Write-Host "HeartbeatIntervalSeconds: $HeartbeatIntervalSeconds"
Write-Host "TelemetryIntervalMilliseconds: $TelemetryIntervalMilliseconds"
Write-Host "CommandPollWaitSeconds: $CommandPollWaitSeconds"

$jobs = foreach ($device in $devices) {
    Start-Job -ArgumentList @(
        $ApiBaseUrl,
        $device.RobotId,
        $device.DeviceSecret,
        $DurationSeconds,
        $HeartbeatIntervalSeconds,
        $TelemetryIntervalMilliseconds,
        $CommandPollWaitSeconds
    ) -ScriptBlock {
        param(
            [string]$ApiBaseUrl,
            [string]$RobotId,
            [string]$DeviceSecret,
            [int]$DurationSeconds,
            [int]$HeartbeatIntervalSeconds,
            [int]$TelemetryIntervalMilliseconds,
            [int]$CommandPollWaitSeconds
        )

        $stats = [ordered]@{
            RobotId = $RobotId
            SessionOk = $false
            HeartbeatOk = 0
            HeartbeatFail = 0
            TelemetryOk = 0
            TelemetryFail = 0
            PendingOk = 0
            PendingNoContent = 0
            PendingFail = 0
            LastError = ""
        }

        try {
            $sessionHeaders = @{
                "X-Robot-Id" = $RobotId
                "X-Device-Secret" = $DeviceSecret
            }

            $session = Invoke-RestMethod `
                -Method Post `
                -Uri "$ApiBaseUrl/api/device/session" `
                -Headers $sessionHeaders `
                -TimeoutSec 15

            $accessToken = $session.accessToken

            if ([string]::IsNullOrWhiteSpace($accessToken)) {
                throw "Session response did not include accessToken."
            }

            $stats.SessionOk = $true
            $authHeaders = @{
                "Authorization" = "Bearer $accessToken"
            }

            $deadline = [DateTimeOffset]::UtcNow.AddSeconds($DurationSeconds)
            $nextHeartbeatAt = [DateTimeOffset]::UtcNow
            $nextTelemetryAt = [DateTimeOffset]::UtcNow
            $nextPendingAt = [DateTimeOffset]::UtcNow

            while ([DateTimeOffset]::UtcNow -lt $deadline) {
                $now = [DateTimeOffset]::UtcNow

                if ($now -ge $nextHeartbeatAt) {
                    try {
                        Invoke-RestMethod `
                            -Method Post `
                            -Uri "$ApiBaseUrl/api/device/heartbeat" `
                            -Headers $authHeaders `
                            -TimeoutSec 10 | Out-Null

                        $stats.HeartbeatOk++
                    }
                    catch {
                        $stats.HeartbeatFail++
                        $stats.LastError = $_.Exception.Message
                    }

                    $nextHeartbeatAt = $now.AddSeconds($HeartbeatIntervalSeconds)
                }

                if ($now -ge $nextTelemetryAt) {
                    $telemetry = @{
                        robotId = $RobotId
                        jointAngles = @(0, 0, 0, 0, 0, 0)
                        tcpPose = @{
                            x = 0
                            y = 0
                            z = 0
                            rx = 0
                            ry = 0
                            rz = 0
                        }
                        statusCode = "Running"
                        timestamp = [DateTimeOffset]::UtcNow.ToString("O")
                    } | ConvertTo-Json -Depth 6

                    try {
                        Invoke-RestMethod `
                            -Method Post `
                            -Uri "$ApiBaseUrl/api/device/telemetry" `
                            -Headers $authHeaders `
                            -ContentType "application/json" `
                            -Body $telemetry `
                            -TimeoutSec 10 | Out-Null

                        $stats.TelemetryOk++
                    }
                    catch {
                        $stats.TelemetryFail++
                        $stats.LastError = $_.Exception.Message
                    }

                    $nextTelemetryAt = $now.AddMilliseconds($TelemetryIntervalMilliseconds)
                }

                if ($now -ge $nextPendingAt) {
                    try {
                        $pendingUri = "$ApiBaseUrl/api/device/commands/pending?waitSeconds=$CommandPollWaitSeconds&isBusy=false"

                        $response = Invoke-WebRequest `
                            -Method Get `
                            -Uri $pendingUri `
                            -Headers $authHeaders `
                            -TimeoutSec ($CommandPollWaitSeconds + 10) `
                            -SkipHttpErrorCheck

                        if ($response.StatusCode -eq 204) {
                            $stats.PendingNoContent++
                        }
                        elseif ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                            $stats.PendingOk++
                        }
                        else {
                            $stats.PendingFail++
                            $stats.LastError = "Pending command HTTP $($response.StatusCode)"
                        }
                    }
                    catch {
                        $stats.PendingFail++
                        $stats.LastError = $_.Exception.Message
                    }

                    $nextPendingAt = [DateTimeOffset]::UtcNow.AddSeconds($CommandPollWaitSeconds)
                }

                Start-Sleep -Milliseconds 50
            }
        }
        catch {
            $stats.LastError = $_.Exception.Message
        }

        [pscustomobject]$stats
    }
}

$results = $jobs | Wait-Job | Receive-Job
$jobs | Remove-Job

$results | Format-Table -AutoSize

$summary = [pscustomobject]@{
    Robots = $results.Count
    SessionsOk = ($results | Where-Object { $_.SessionOk }).Count
    HeartbeatOk = ($results | Measure-Object -Property HeartbeatOk -Sum).Sum
    HeartbeatFail = ($results | Measure-Object -Property HeartbeatFail -Sum).Sum
    TelemetryOk = ($results | Measure-Object -Property TelemetryOk -Sum).Sum
    TelemetryFail = ($results | Measure-Object -Property TelemetryFail -Sum).Sum
    PendingOk = ($results | Measure-Object -Property PendingOk -Sum).Sum
    PendingNoContent = ($results | Measure-Object -Property PendingNoContent -Sum).Sum
    PendingFail = ($results | Measure-Object -Property PendingFail -Sum).Sum
}

Write-Host ""
Write-Host "Summary"
$summary | Format-List

if ($summary.SessionsOk -ne $summary.Robots -or
    $summary.HeartbeatFail -gt 0 -or
    $summary.TelemetryFail -gt 0 -or
    $summary.PendingFail -gt 0) {
    exit 1
}

exit 0