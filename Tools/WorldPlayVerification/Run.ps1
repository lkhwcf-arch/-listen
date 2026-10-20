param(
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.2.8f1/Editor/Unity.exe',
    [switch]$IncludeServer,
    [string]$ServerRoot,
    [string]$NodePath = 'node'
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$verificationRoot = Join-Path $projectRoot 'Temp/WorldPlayVerification'
New-Item -ItemType Directory -Force $verificationRoot | Out-Null
function Copy-VerificationFiles([string]$relativePath, [string]$excludePattern = '^$') {
    $sourceRoot = Join-Path $projectRoot $relativePath
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File) {
        if ($file.FullName -match $excludePattern) { continue }
        $relativeFile = $file.FullName.Substring($projectRoot.Length + 1)
        $target = Join-Path $verificationRoot $relativeFile
        New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($target)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}
Copy-VerificationFiles 'Assets/3.Script/Common'
Copy-VerificationFiles 'Assets/3.Script/Data'
Copy-VerificationFiles 'Assets/3.Script/Player'
Copy-VerificationFiles 'Assets/3.Script/Session' '\\Presentation\\|SessionJoinTester'
Copy-VerificationFiles 'Assets/3.Script/Networking'
Copy-VerificationFiles 'Assets/3.Script/Generated/Protocol' '\.asmdef(\.meta)?$|protocol\.manifest\.json$'
Copy-VerificationFiles 'Assets/Plugins/Protobuf'
Copy-VerificationFiles 'Assets/3.Script/World' 'GameEntryPresenter'
Copy-VerificationFiles 'Assets/2.Model/Prefabs'
Copy-VerificationFiles 'Assets/11.CSV'
Copy-VerificationFiles 'Assets/10.Input'
Copy-VerificationFiles 'Assets/Resources'
foreach ($directory in @('Assets/Editor','Assets/1.Scene','ProjectSettings','Packages')) {
    New-Item -ItemType Directory -Force (Join-Path $verificationRoot $directory) | Out-Null
}
Copy-Item (Join-Path $PSScriptRoot 'WorldVerification.cs') (Join-Path $verificationRoot 'Assets/Editor/WorldVerification.cs')
Copy-Item (Join-Path $projectRoot 'Assets/3.Script/Editor/GameContentBuilder.cs') (Join-Path $verificationRoot 'Assets/Editor/GameContentBuilder.cs')
foreach ($file in @('Ingame.unity','Ingame.unity.meta')) {
    Copy-Item (Join-Path $projectRoot "Assets/1.Scene/$file") (Join-Path $verificationRoot "Assets/1.Scene/$file")
}
Copy-Item (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') (Join-Path $verificationRoot 'ProjectSettings/ProjectVersion.txt')
foreach ($file in @('ProjectSettings.asset','DynamicsManager.asset')) {
    Copy-Item (Join-Path $projectRoot ('ProjectSettings/' + $file)) (Join-Path $verificationRoot ('ProjectSettings/' + $file))
}
$manifest = Get-Content (Join-Path $projectRoot 'Packages/manifest.json') -Raw | ConvertFrom-Json
$dependencies = @{}
foreach ($property in $manifest.dependencies.PSObject.Properties) {
    if ($property.Name.StartsWith('com.unity.modules.') -or
        $property.Name -in @('com.unity.inputsystem','com.unity.render-pipelines.universal')) {
        $dependencies[$property.Name] = $property.Value
    }
}
@{ dependencies = $dependencies } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $verificationRoot 'Packages/manifest.json')
$serverProcess = $null
$previousFlag = $env:WORLD_VERIFICATION_SERVER
try {
    $env:WORLD_VERIFICATION_SERVER = if ($IncludeServer) { '1' } else { '0' }
    if ($IncludeServer) {
        if (!$ServerRoot) { $ServerRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '../TSC_REST')) }
        $serverProcess = Start-Process -FilePath $NodePath -ArgumentList @(
            ('"' + (Join-Path $PSScriptRoot 'Server.cjs') + '"'), ('"' + $ServerRoot + '"')
        ) -PassThru -WindowStyle Hidden -RedirectStandardError (Join-Path $verificationRoot 'server-error.log')
        if ($serverProcess.WaitForExit(300)) { throw "Verification server exited: $verificationRoot/server-error.log" }
    }
    $unity = Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode','-nographics','-projectPath',
        ('"' + $verificationRoot + '"'),'-executeMethod','WorldVerification.Run','-logFile',
        ('"' + (Join-Path $verificationRoot 'verification.log') + '"')) -PassThru -Wait -WindowStyle Hidden
    if ($unity.ExitCode -eq 2) {
        Get-Content (Join-Path $verificationRoot 'verification.txt')
        throw 'Unity 검사가 콘텐츠 미완성으로 중단되었습니다. B 정상 플레이·온라인 전체 시나리오 통과가 아닙니다.'
    }
    if ($unity.ExitCode -ne 0) { throw "Unity verification failed. See $verificationRoot/verification.log" }
    Get-Content (Join-Path $verificationRoot 'verification.txt')
}
finally {
    if ($serverProcess -and !$serverProcess.HasExited) { $serverProcess.Kill(); $serverProcess.WaitForExit() }
    $env:WORLD_VERIFICATION_SERVER = $previousFlag
}
