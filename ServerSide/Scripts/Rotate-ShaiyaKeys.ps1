param(
    [string]$IniPath    = 'C:\ShaiyaServer\PSM_Client\Bin\Data\GMSecurity.ini',
    [string]$LogDir     = 'C:\ShaiyaServer\Logs\KeyRotation',
    [string]$PatchDir   = 'C:\xampp\htdocs\Updater\Patches',
    [string]$CryptTool  = 'C:\ShaiyaServer\Scripts\sahcrypt.exe',
    [switch]$SkipReboot
)

New-Item -ItemType Directory -Force $LogDir | Out-Null
$logFile = Join-Path $LogDir ('rotation-' + (Get-Date -Format 'yyyy-MM-dd_HH-mm') + '.log')

# Pulizia log vecchi: mantieni solo log del giorno corrente
$todayMidnight = (Get-Date).Date
Get-ChildItem $LogDir -Filter 'rotation-*.log' -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -lt $todayMidnight } |
    Remove-Item -Force -ErrorAction SilentlyContinue

function Log($msg) {
    $line = (Get-Date -Format 'HH:mm:ss') + ' ' + $msg
    Add-Content -Path $logFile -Value $line -Encoding ASCII
    Write-Output $line
}

function NewKey {
    $b = New-Object byte[] 32
    $r = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $r.GetBytes($b)
    $r.Dispose()
    return [System.BitConverter]::ToString($b).Replace('-', '').ToLower()
}

try {
    Log '=== Rotazione chiavi Shaiya - avvio ==='

    # 1. Leggi GMSecurity.ini e salva old PatchKeyHex per re-cifratura
    $content = [System.IO.File]::ReadAllText($IniPath, [System.Text.Encoding]::ASCII)
    $oldPatchKey = $null
    if ($content -match '(?m)^PatchKeyHex=([0-9a-fA-F]{64})') {
        $oldPatchKey = $Matches[1].Trim()
        Log 'PatchKeyHex corrente letta'
    } else {
        Log 'PatchKeyHex non trovata in INI'
    }

    # 2. Genera nuove chiavi (ClientSecret, TokenSecret, PatchKeyHex)
    # SahKeyHex NON viene toccata: richiede re-distribuzione manuale di data.sah
    $newClient = NewKey
    $newToken  = NewKey
    $newPatch  = NewKey
    Log 'Nuove chiavi generate'

    # 3. Aggiorna GMSecurity.ini
    $lines = $content -split "`r?`n"
    $lines = @($lines | ForEach-Object { $_.TrimEnd() })
    $content = $lines -join "`r`n"
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '(?m)^ClientSecret=.*', ('ClientSecret=' + $newClient))
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '(?m)^TokenSecret=.*',  ('TokenSecret='  + $newToken))
    if ($content -match '(?m)^PatchKeyHex=') {
        $content = [System.Text.RegularExpressions.Regex]::Replace($content, '(?m)^PatchKeyHex=.*', ('PatchKeyHex=' + $newPatch))
    } else {
        $content = [System.Text.RegularExpressions.Regex]::Replace($content, '(?m)^(SahKeyHex=.+)', ('$1' + "`r`n" + 'PatchKeyHex=' + $newPatch))
    }
    [System.IO.File]::WriteAllText($IniPath, $content, [System.Text.Encoding]::ASCII)
    Log 'GMSecurity.ini aggiornato con nuove chiavi'

    # 4. Re-cifra update.sah (ESAH) dentro ogni file .patch (ZIP) con la nuova PatchKeyHex.
    # Struttura: .patch e' un ZIP (Ionic.Zip/DotNetZip) con update.sah (ESAH) + update.saf.
    # Usa Expand-Archive + Compress-Archive (PS 5.1 built-in, no System.IO.Compression.ZipArchiveMode).
    if (($null -ne $oldPatchKey) -and (Test-Path $CryptTool) -and (Test-Path $PatchDir)) {
        $patchFiles = @(Get-ChildItem $PatchDir -Filter '*.patch' -ErrorAction SilentlyContinue)
        Log ('Re-cifratura update.sah in ' + $patchFiles.Count + ' file patch...')
        foreach ($f in $patchFiles) {
            $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
            $tmpDec = Join-Path $tmpDir 'update.sah.dec'
            $tmpZip = $tmpDir + '.zip'
            try {
                # Espandi-Archive richiede estensione .zip in PS 5.1 — copia .patch → .zip temp
                New-Item -ItemType Directory -Path $tmpDir | Out-Null
                Copy-Item $f.FullName $tmpZip -Force
                Expand-Archive -Path $tmpZip -DestinationPath $tmpDir -Force
                Remove-Item $tmpZip -Force -ErrorAction SilentlyContinue

                $sahPath = Join-Path $tmpDir 'update.sah'
                if (-not (Test-Path $sahPath)) {
                    Log ('WARN: update.sah non trovato in ' + $f.Name + ' - skip')
                    continue
                }

                # Decifra update.sah con vecchia chiave
                & $CryptTool decrypt $sahPath $tmpDec $oldPatchKey 2>&1 | Out-Null
                if ($LASTEXITCODE -ne 0) {
                    Log ('WARN: decrypt update.sah fallito in ' + $f.Name + ' (chiave sbagliata?) - skip')
                    continue
                }

                # Ri-cifra update.sah con nuova chiave (sovrascrive in tmpDir)
                & $CryptTool encrypt $tmpDec $sahPath $newPatch 2>&1 | Out-Null
                if ($LASTEXITCODE -ne 0) {
                    Log ('WARN: encrypt update.sah fallito in ' + $f.Name + ' - skip')
                    continue
                }

                # Ricostruisci .patch: Compress-Archive vuole output .zip in PS 5.1
                # → crea .zip in temp, poi copia sul .patch originale
                $bakPath    = $f.FullName + '.bak'
                $newZipPath = $tmpDir + '_new.zip'
                Copy-Item $f.FullName $bakPath -Force
                $filesToZip = @(Get-ChildItem $tmpDir -File | Where-Object { $_.Name -ne 'update.sah.dec' } | Select-Object -ExpandProperty FullName)
                Compress-Archive -Path $filesToZip -DestinationPath $newZipPath
                Copy-Item $newZipPath $f.FullName -Force
                Remove-Item $newZipPath -Force -ErrorAction SilentlyContinue
                Remove-Item $bakPath -Force -ErrorAction SilentlyContinue

                Log ('OK: ' + $f.Name)
            } catch {
                # Ripristina backup se esiste
                $bakPath = $f.FullName + '.bak'
                if ((Test-Path $bakPath) -and (-not (Test-Path $f.FullName))) {
                    Copy-Item $bakPath $f.FullName -Force
                    Log ('Backup ripristinato per ' + $f.Name)
                }
                Log ('ERRORE su ' + $f.Name + ': ' + $_)
            } finally {
                if (Test-Path $tmpZip)              { Remove-Item $tmpZip              -Force -ErrorAction SilentlyContinue }
                if (Test-Path ($tmpDir + '_new.zip')){ Remove-Item ($tmpDir + '_new.zip') -Force -ErrorAction SilentlyContinue }
                if (Test-Path $tmpDir)              { Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue }
            }
        }
    } else {
        $ct = (Test-Path $CryptTool).ToString()
        $pd = (Test-Path $PatchDir).ToString()
        $ok = ($null -ne $oldPatchKey).ToString()
        Log ('Re-cifratura saltata (sahcrypt=' + $ct + ' PatchDir=' + $pd + ' OldKey=' + $ok + ')')
    }

    # 4b. Invalida GameSecuritySessions — secToken client legati alle vecchie chiavi.
    # launchToken diventa invalido automaticamente (HMAC con ClientSecret cambiato).
    # secToken sono random+DB: non dipendono dalle chiavi, vanno eliminati manualmente.
    # Al prossimo login dall'updater, un nuovo secToken viene generato automaticamente.
    Log 'Invalidazione GameSecuritySessions...'
    $delResult = & sqlcmd -S 127.0.0.1 -E -d PS_UserData -Q "DELETE FROM dbo.GameSecuritySessions; PRINT CAST(@@ROWCOUNT AS VARCHAR) + ' sessioni invalidate';" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Log ('GameSecuritySessions: ' + ($delResult -join ' ').Trim())
    } else {
        Log ('WARN: DELETE GameSecuritySessions fallito: ' + ($delResult -join ' '))
    }

    # 5. Riavvio del server Windows
    # I servizi ps_* si riavviano automaticamente all'avvio (AUTO_START DELAYED).
    # Il riavvio garantisce il reload di GMSecurity.ini da parte di tutti i servizi
    # (workaround per static bool loaded in sdev-login.dll).
    if ($SkipReboot) {
        Log 'Rotazione chiavi completata. SkipReboot attivo - nessun riavvio.'
    } else {
        Log 'Rotazione chiavi completata. Riavvio server Windows in corso...'
        & shutdown.exe /r /f /t 15
        Log 'Comando riavvio inviato (shutdown /r /f /t 15)'
    }
    exit 0
}
catch {
    Log ('ERRORE CRITICO: ' + $_ + ' - riavvio annullato')
    exit 1
}
