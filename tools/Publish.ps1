$ErrorActionPreference='Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $taskUserData=@('overlay-settings.json','custom-color-themes.json','last-color-theme.txt','MyriadMelodies.portable')
    New-Item -ItemType Directory -Path 'artifacts/release' -Force | Out-Null
    $taskVariants=@(
        @{Profile='SingleFile';Folder='win-x64';Name='MyriadMelodies-standalone-win-x64.exe'},
        @{Profile='RuntimeRequired';Folder='runtime-required-win-x64';Name='MyriadMelodies-runtime-required-win-x64.exe'}
    )
    $taskChecksums=@()
    foreach($taskVariant in $taskVariants){
        dotnet publish MyriadMelodies/MyriadMelodies.csproj "-p:PublishProfile=$($taskVariant.Profile)" -v:minimal
        if($LASTEXITCODE -ne 0){throw "Publish failed: $($taskVariant.Profile)"}
        $taskExe=Get-Item "artifacts/publish/$($taskVariant.Folder)/MyriadMelodies.exe"
        $taskPublished=@(Get-ChildItem $taskExe.Directory.FullName -File -Recurse | Where-Object {$_.Name -notin $taskUserData})
        if($taskPublished.Count -ne 1){throw 'Publish directory contains files other than the EXE.'}
        $taskDestination=Join-Path 'artifacts/release' $taskVariant.Name
        Copy-Item -LiteralPath $taskExe.FullName -Destination $taskDestination
        $taskHash=(Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash.ToLowerInvariant()
        $taskChecksums+="$taskHash *$($taskVariant.Name)"
        Write-Output ([System.IO.Path]::GetFullPath($taskDestination))
    }
    [System.IO.File]::WriteAllText((Join-Path (Get-Location) 'artifacts/release/SHA256SUMS.txt'),($taskChecksums -join "`n")+"`n")
}
finally{Pop-Location}
