param(
    [string]$MainCppPath = "SahCryptTool/main.cpp",
    [string]$LauncherIniPath = "ServerSide/Updater/launcher.ini",
    [string]$LauncherSampleIniPath = "ServerSide/Updater/launcher.sample.ini",
    [switch]$UpdateSampleIni,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    return (Join-Path $repoRoot $Path)
}

function New-HexKey {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)

    $hex = ($bytes | ForEach-Object { $_.ToString("x2") }) -join ""
    return [PSCustomObject]@{
        Bytes = $bytes
        Hex   = $hex
    }
}

function Build-CppKeyBlock {
    param([byte[]]$Bytes)

    $chunks = @()
    for ($i = 0; $i -lt $Bytes.Length; $i += 8) {
        $lineBytes = $Bytes[$i..([Math]::Min($i + 7, $Bytes.Length - 1))]
        $literals = $lineBytes | ForEach-Object { "0x{0}" -f $_.ToString("x2") }
        $line = "    " + ($literals -join ", ")
        if ($i + 8 -lt $Bytes.Length) {
            $line += ","
        }
        $chunks += $line
    }

    $lines = @("static constexpr std::array<uint8_t, kKeySize> kFixedKey = {")
    $lines += $chunks
    $lines += "};"

    return ($lines -join [Environment]::NewLine)
}

function Update-MainCpp {
    param(
        [string]$Path,
        [byte[]]$Bytes,
        [switch]$DryRun
    )

    if (-not (Test-Path $Path)) {
        throw "main.cpp not found: $Path"
    }

    $content = Get-Content -Path $Path -Raw -Encoding UTF8
    $pattern = 'static constexpr std::array<uint8_t,\s*kKeySize>\s*kFixedKey\s*=\s*\{[\s\S]*?\};'

    if ($content -notmatch $pattern) {
        throw "Could not find kFixedKey block in: $Path"
    }

    $replacement = Build-CppKeyBlock -Bytes $Bytes
    $updated = [System.Text.RegularExpressions.Regex]::Replace($content, $pattern, $replacement)

    if (-not $DryRun) {
        Set-Content -Path $Path -Value $updated -Encoding UTF8
    }
}

function Update-LauncherIni {
    param(
        [string]$Path,
        [string]$Hex,
        [switch]$DryRun
    )

    if (-not (Test-Path $Path)) {
        throw "launcher.ini not found: $Path"
    }

    $content = Get-Content -Path $Path -Raw -Encoding UTF8

    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '(?m)^KeyHex=.*$', "KeyHex=$Hex")
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '(?m)^KeyText=.*$', 'KeyText=')
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '(?m)^KeyBase64=.*$', 'KeyBase64=')
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '(?m)^Key=.*$', 'Key=')

    if (-not $DryRun) {
        Set-Content -Path $Path -Value $content -Encoding UTF8
    }
}

$key = New-HexKey
$mainCpp = Resolve-RepoPath $MainCppPath
$launcherIni = Resolve-RepoPath $LauncherIniPath

Update-MainCpp -Path $mainCpp -Bytes $key.Bytes -DryRun:$DryRun
Update-LauncherIni -Path $launcherIni -Hex $key.Hex -DryRun:$DryRun

if ($UpdateSampleIni) {
    $launcherSampleIni = Resolve-RepoPath $LauncherSampleIniPath
    Update-LauncherIni -Path $launcherSampleIni -Hex $key.Hex -DryRun:$DryRun
}

Write-Host "SahCrypt key generated and applied." -ForegroundColor Green
Write-Host "KeyHex: $($key.Hex)"
Write-Host "Updated: $mainCpp"
Write-Host "Updated: $launcherIni"
if ($UpdateSampleIni) {
    Write-Host "Updated: $(Resolve-RepoPath $LauncherSampleIniPath)"
}
if ($DryRun) {
    Write-Host "DryRun enabled: no files were written." -ForegroundColor Yellow
}
