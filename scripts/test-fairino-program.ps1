[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AccessToken,

    [Parameter(Mandatory = $true)]
    [Guid]$RobotId,

    [string]$BaseUrl = "http://localhost:5200",

    [string]$ProgramName = "FAIRINO Swagger smoke test",

    [switch]$Execute,

    [switch]$WaitForCompletion,

    [ValidateRange(10, 600)]
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd("/")

$headers = @{
    Authorization = "Bearer $AccessToken"
    Accept = "application/json"
}

function Invoke-SyntwinApi {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("GET", "POST")]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [object]$Body
    )

    $request = @{
        Method = $Method
        Uri = "$BaseUrl$Path"
        Headers = $headers
    }

    if ($PSBoundParameters.ContainsKey("Body")) {
        $request.ContentType = "application/json"
        $request.Body = $Body | ConvertTo-Json -Depth 12
    }

    try {
        return Invoke-RestMethod @request
    }
    catch {
        $detail = $_.ErrorDetails.Message
        if ([string]::IsNullOrWhiteSpace($detail)) {
            $detail = $_.Exception.Message
        }

        throw "API $Method $Path failed: $detail"
    }
}

Write-Host "Checking robot $RobotId..."
$robot = Invoke-SyntwinApi -Method GET -Path "/api/robots/$RobotId"
Write-Host "Robot: $($robot.robotName) | model: $($robot.model) | status: $($robot.status)"

# These conservative targets are close to Fairino-Studio's initial FR5 pose.
# Verify collision clearance and teach approved poses before using a physical arm.
$programBody = @{
    name = "$ProgramName $(Get-Date -Format 'yyyyMMdd-HHmmss')"
    status = "Draft"
    source = "Studio"
    steps = @(
        @{
            orderIndex = 1
            stepType = "MoveJ"
            label = "Move to test pose A"
            payload = @{
                jointAngles = @(0, -30, 90, 0, 60, 0)
                speed = 10
                acc = 10
            }
        },
        @{
            orderIndex = 2
            stepType = "GripperOpen"
            label = "Open gripper"
            payload = @{}
        },
        @{
            orderIndex = 3
            stepType = "WaitMs"
            label = "Wait before moving"
            payload = @{
                delayMs = 1000
            }
        },
        @{
            orderIndex = 4
            stepType = "MoveJ"
            label = "Move to test pose B"
            payload = @{
                jointAngles = @(10, -35, 85, 0, 60, 10)
                speed = 10
                acc = 10
            }
        },
        @{
            orderIndex = 5
            stepType = "GripperClose"
            label = "Close gripper"
            payload = @{}
        },
        @{
            orderIndex = 6
            stepType = "WaitMs"
            label = "Hold position"
            payload = @{
                delayMs = 1000
            }
        },
        @{
            orderIndex = 7
            stepType = "SetDO"
            label = "Turn cabinet DO2 on"
            payload = @{
                doType = "cabinet"
                doIndex = 2
                doValue = 1
            }
        },
        @{
            orderIndex = 8
            stepType = "MoveJ"
            label = "Return to test pose A"
            payload = @{
                jointAngles = @(0, -30, 90, 0, 60, 0)
                speed = 10
                acc = 10
            }
        },
        @{
            orderIndex = 9
            stepType = "GripperOpen"
            label = "Release gripper"
            payload = @{}
        },
        @{
            orderIndex = 10
            stepType = "SetDO"
            label = "Turn cabinet DO2 off"
            payload = @{
                doType = "cabinet"
                doIndex = 2
                doValue = 0
            }
        }
    )
}

Write-Host "Creating Draft program..."
$program = Invoke-SyntwinApi `
    -Method POST `
    -Path "/api/robots/$RobotId/programs" `
    -Body $programBody

Write-Host "Publishing program $($program.id)..."
$publishedProgram = Invoke-SyntwinApi `
    -Method POST `
    -Path "/api/robots/$RobotId/programs/$($program.id)/publish"

Write-Host "Program ready: $($publishedProgram.id) | status: $($publishedProgram.status)"

if (-not $Execute) {
    Write-Warning "Program was created and published, but no motion command was sent."
    Write-Host "Review the poses, clear the robot workspace, then rerun with -Execute."
    return
}

Write-Warning "Sending RunProgram. The connected device may start moving immediately."
$command = Invoke-SyntwinApi `
    -Method POST `
    -Path "/api/robots/$RobotId/commands" `
    -Body @{
        commandType = "RunProgram"
        payload = @{
            programId = $publishedProgram.id
        }
    }

Write-Host "Command queued: $($command.id) | status: $($command.status)"

if (-not $WaitForCompletion) {
    return
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$terminalStatuses = @("Completed", "Failed", "Timeout", "Cancelled")

do {
    Start-Sleep -Seconds 2

    $commands = Invoke-SyntwinApi `
        -Method GET `
        -Path "/api/robots/$RobotId/commands"

    $current = $commands | Where-Object { $_.id -eq $command.id } | Select-Object -First 1

    if ($null -ne $current) {
        Write-Host "Command status: $($current.status)"

        if ($terminalStatuses -contains $current.status) {
            if ($current.status -ne "Completed") {
                throw "RunProgram ended with status $($current.status)."
            }

            Write-Host "FAIRINO program completed successfully."
            return
        }
    }
} while ((Get-Date) -lt $deadline)

throw "Timed out after $TimeoutSeconds seconds while waiting for command $($command.id)."
