# Windows PowerShell 5.1; no Excel/Python installation is required.
[CmdletBinding()]
param(
    [string]$InputDirectory,
    [string]$OutputDirectory,
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($InputDirectory)) {
    $InputDirectory = Join-Path $projectRoot 'EXCEL'
} elseif (-not [System.IO.Path]::IsPathRooted($InputDirectory)) {
    $InputDirectory = Join-Path $projectRoot $InputDirectory
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot 'Assets\11.CSV'
} elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot $OutputDirectory
}

try {
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [Console]::OutputEncoding = $encoding
    $OutputEncoding = $encoding
    $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Exporter.cs') -Raw -Encoding UTF8
    Add-Type -TypeDefinition $source -Language CSharp -ReferencedAssemblies @(
        'System.Core.dll', 'System.Xml.dll', 'System.Xml.Linq.dll',
        'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll'
    )
    [void][SilentCsv.Exporter]::Export($InputDirectory, $OutputDirectory, $ValidateOnly.IsPresent)
    exit 0
} catch {
    $failure = $_.Exception
    while ($failure.InnerException) { $failure = $failure.InnerException }
    [Console]::Error.WriteLine('CSV export failed: ' + $failure.Message)
    exit 1
}
