[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AccessToken,

    [Parameter(Mandatory = $true)]
    [Guid]$RobotId,

    [string]$BaseUrl = "http://localhost:5200",

    [switch]$Execute,

    [switch]$WaitForCompletion,

    [ValidateRange(10, 600)]
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd("/")
$programFile = Join-Path $PSScriptRoot "..\docs\swagger\lay-coc-program.json"

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

if (-not (Test-Path -LiteralPath $programFile)) {
    throw "Program JSON was not found: $programFile"
}

$programBody = Get-Content -Raw -LiteralPath $programFile | ConvertFrom-Json
$programBody.name = "$($programBody.name) $(Get-Date -Format 'yyyyMMdd-HHmmss')"

Write-Host "Checking robot $RobotId..."
$robot = Invoke-SyntwinApi -Method GET -Path "/api/robots/$RobotId"
Write-Host "Robot: $($robot.robotName) | model: $($robot.model) | status: $($robot.status)"

Write-Warning "The imported Lua ends at J1 = -175 degrees. Verify this target on the FAIRINO teach pendant."
Write-Host "Creating program imported from lay_coc.lua..."
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
    Write-Warning "No RunProgram command was sent."
    Write-Host "Review all joint targets, then rerun with -Execute."
    return
}

Write-Warning "Sending RunProgram. The connected FAIRINO device or simulator may move immediately."
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

    $current = $commands |
        Where-Object { $_.id -eq $command.id } |
        Select-Object -First 1

    if ($null -ne $current) {
        Write-Host "Command status: $($current.status)"

        if ($terminalStatuses -contains $current.status) {
            if ($current.status -ne "Completed") {
                throw "RunProgram ended with status $($current.status)."
            }

            Write-Host "lay_coc program completed successfully."
            return
        }
    }
} while ((Get-Date) -lt $deadline)

throw "Timed out after $TimeoutSeconds seconds while waiting for command $($command.id)."
