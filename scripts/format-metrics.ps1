<#
.SYNOPSIS
  Formats raw OpenTelemetry / .NET metrics into a readable summary.

.DESCRIPTION
  Paste metrics output (or pipe from file) into this script to get a clean summary
  with plain-English explanations.

.EXAMPLE
  Get-Content metrics-dump.txt | .\scripts\format-metrics.ps1
  # Or paste into terminal, then run: .\scripts\format-metrics.ps1
#>

param(
    [Parameter(ValueFromPipeline = $true)]
    [string[]] $InputText
)

$raw = if ($InputText) { $InputText -join "`n" } else { [System.Console]::In.ReadToEnd() }
if (-not $raw.Trim()) { Write-Host "Paste metrics text and pipe to this script, or pass a file."; exit 1 }

# Plain-English descriptions for StudyPilot and common .NET metrics
$descriptions = @{
    'http.client.request.time_in_queue'   = 'Time outbound requests waited for a free connection (e.g. to AI).'
    'http.client.request.duration'        = 'Duration of outbound HTTP requests (e.g. to AI at :8000).'
    'http.client.active_requests'         = 'Outbound HTTP requests in flight.'
    'http.client.open_connections'        = 'Outbound connections (idle or active).'
    'ai_request_duration_ms'              = 'Time spent in AI calls (health, extract-concepts, quiz).'
    'background_queue_length'            = 'Document jobs waiting in the queue.'
    'background_job_failures_total'       = 'Total document processing jobs that failed.'
    'background_jobs_total'               = 'Total background jobs run.'
    'http_requests_total'                = 'Total HTTP requests to the API.'
    'http_request_duration_ms'            = 'Request duration to the API.'
    'documents_processed_total'           = 'Documents successfully processed.'
    'kestrel.active_connections'         = 'Active TCP connections to the API.'
    'kestrel.queued_connections'         = 'Connections waiting to be accepted.'
    'kestrel.connection.duration'        = 'Duration of each client connection.'
    'http.server.active_requests'        = 'Requests currently being handled.'
    'http.server.request.duration'       = 'Request duration per route.'
    'aspnetcore.rate_limiting.active_request_leases' = 'Requests holding a rate-limit lease.'
    'aspnetcore.rate_limiting.request_lease.duration' = 'How long rate-limit leases were held.'
    'dns.lookup.duration'                 = 'DNS lookup time (e.g. localhost).'
}

function Get-Description($name) {
    $key = ($name -split ',')[0].Trim()
    if ($descriptions.ContainsKey($key)) { $descriptions[$key] } else { '' }
}

# Parse: "Metric Name: X" ... "Value: ..." (and optional buckets)
$blocks = [regex]::Matches($raw, '(?ms)Metric Name:\s*([^\r\n]+?)(?:\r?\n[^\r\n]*)?\r?\nValue:\s*([^\r\n]+)')
$seen = @{}

Write-Host ""
Write-Host "==================== METRICS SUMMARY ====================" -ForegroundColor Cyan
Write-Host ""

foreach ($m in $blocks) {
    $name = $m.Groups[1].Value.Trim()
    $value = $m.Groups[2].Value.Trim()
    $key = ($name -split ',')[0].Trim()
    if ($seen[$key]) { continue }
    $seen[$key] = $true

    # Normalize value line for readability
    $value = $value -replace 'Sum:\s*([\d.]+)\s+Count:\s*(\d+)\s+Min:\s*([\d.]+)\s+Max:\s*([\d.]+)', 'Sum: $1  Count: $2  Min: $3  Max: $4'
    $value = $value -replace 'LongSumNonMonotonic\s*', ''
    $value = $value -replace 'Histogram\s*', ''

    Write-Host $name -ForegroundColor Yellow
    Write-Host "  $value"
    $desc = Get-Description $name
    if ($desc) { Write-Host "  -> $desc" -ForegroundColor DarkGray }
    Write-Host ""
}

# If no blocks found, show a short reference
if ($seen.Count -eq 0) {
    Write-Host "No 'Metric Name:' / 'Value:' blocks found in input." -ForegroundColor Yellow
    Write-Host "Ensure the pasted text includes lines like:"
    Write-Host "  Metric Name: ai_request_duration_ms, ..."
    Write-Host "  Value: Sum: 4183 Count: 2 Min: 2028 Max: 2155"
    Write-Host ""
    Write-Host "Quick reference (StudyPilot):" -ForegroundColor Cyan
    Write-Host "  ai_request_duration_ms     - Time in AI calls (ms)"
    Write-Host "  background_queue_length   - Jobs waiting"
    Write-Host "  background_job_failures_total - Failed document jobs"
    Write-Host "  http.client.request.time_in_queue - Wait for connection to AI (s)"
}

Write-Host "=========================================================" -ForegroundColor Cyan
