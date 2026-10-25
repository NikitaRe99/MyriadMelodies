param([switch]$Gpu)
$ErrorActionPreference='Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $taskProjects=@('CaptureLayout.Checks','Overlay.Checks','Regression.Checks')
    if($Gpu){$taskProjects+=@('WindowCapture.Checks','CaptureSession.Smoke')}
    foreach($taskProject in $taskProjects){
        dotnet build "tests/$taskProject/$taskProject.csproj" -c Release -v:minimal
        if($LASTEXITCODE -ne 0){throw "Build failed: $taskProject"}
        $taskFramework=if($taskProject -eq 'CaptureLayout.Checks'){'net10.0'}else{'net10.0-windows10.0.19041.0'}
        $taskArguments=@("tests/$taskProject/bin/Release/$taskFramework/$taskProject.dll")
        if($Gpu -and $taskProject -in @('Overlay.Checks','Regression.Checks')){$taskArguments+='--'+$(if($taskProject -eq 'Overlay.Checks'){'capture'}else{'gpu'})}
        dotnet @taskArguments
        if($LASTEXITCODE -ne 0){throw "Checks failed: $taskProject"}
    }
}
finally{Pop-Location}
