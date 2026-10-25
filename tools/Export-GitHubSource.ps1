param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskRoot=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $OutputDirectory){$OutputDirectory=Join-Path $taskRoot 'artifacts/github-source'}
$taskOutput=[System.IO.Path]::GetFullPath($OutputDirectory)
if(-not $taskOutput.StartsWith($taskRoot+[System.IO.Path]::DirectorySeparatorChar,[System.StringComparison]::OrdinalIgnoreCase)){
    throw 'Export must be inside this workspace.'
}
if(Test-Path -LiteralPath $taskOutput){throw 'Use a new, empty export directory.'}
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskRoots=@('MyriadMelodies','tests','tools','docs','.github','MyriadMelodies.sln','README.md','README.en.md','LICENSE','THIRD-PARTY-NOTICES.txt','.gitignore')
$taskExcludedDirectories=@('bin','obj','artifacts','packages','.git','.vs','.agents','.codex','.idea','.vscode','__pycache__')
$taskExcludedFiles=@('MyriadMelodies.portable','overlay-settings.json','custom-color-themes.json','last-color-theme.txt','packages.config','App.config')
$taskInternalFiles=@('docs/CODE-REVIEW.md','docs/GITHUB.md','tools/promote-debug-themes.py','tools/extract-ui-strings.py','tools/localize-ui-literals.py')
$taskAllowedExtensions=@('.cs','.csproj','.sln','.resx','.ico','.pubxml','.md','.txt','.json','.settings','.ps1','.py','.yml','.yaml','.png')
$taskApprovedImages=@('compact-ru.png','compact-en.png','settings-zones-ru.png','settings-zones-en.png','result-ru.png','result-en.png')
foreach($taskEntry in $taskRoots){
    $taskSource=Join-Path $taskRoot $taskEntry
    foreach($taskFile in Get-ChildItem -LiteralPath $taskSource -File -Recurse -Force){
        $taskRelative=[System.IO.Path]::GetRelativePath($taskRoot,$taskFile.FullName)
        $taskSegments=$taskRelative -split '[\\/]'
        if($taskRelative.Replace('\','/') -in $taskInternalFiles){continue}
        if(@($taskSegments | Where-Object {$_ -in $taskExcludedDirectories}).Count -gt 0){continue}
        if($taskFile.Name -in $taskExcludedFiles -or $taskFile.Extension -in @('.user','.suo','.tmp','.mp4','.pdb','.sqlite','.zip')){continue}
        if($taskFile.Extension -notin $taskAllowedExtensions -and $taskFile.Name -notin @('LICENSE','.gitignore')){throw "Unexpected source file: $taskRelative"}
        if($taskFile.Extension -eq '.png' -and ($taskRelative -notlike 'docs*images*' -or $taskFile.Name -notin $taskApprovedImages)){throw "Image is not approved for publishing: $taskRelative"}
        $taskTarget=Join-Path $taskOutput $taskRelative
        New-Item -ItemType Directory -Path (Split-Path $taskTarget -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $taskFile.FullName -Destination $taskTarget
    }
}
Write-Output $taskOutput
