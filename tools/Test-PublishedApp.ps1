param([string]$Executable = (Join-Path $PSScriptRoot '../artifacts/publish/win-x64/MyriadMelodies.exe'),[switch]$RuntimeRequired)
$ErrorActionPreference = 'Stop'
$taskExecutable = [System.IO.Path]::GetFullPath($Executable)
if (-not (Test-Path -LiteralPath $taskExecutable -PathType Leaf)) { throw "EXE not found: $taskExecutable" }
$taskUserData = @('overlay-settings.json','custom-color-themes.json','last-color-theme.txt','MyriadMelodies.portable')
$taskFiles = @(Get-ChildItem -LiteralPath (Split-Path $taskExecutable) -File -Recurse | Where-Object {$_.Name -notin $taskUserData})
if ($taskFiles.Count -ne 1 -or $taskFiles[0].FullName -ne $taskExecutable) { throw 'Distribution must contain exactly one EXE.' }

Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class PublishedWindowProbe {
    private delegate bool Callback(IntPtr handle, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, IntPtr state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    public static IntPtr FindMainWindow(int process) {
        IntPtr found=IntPtr.Zero;
        EnumWindows((handle,state)=>{
            GetWindowThreadProcessId(handle,out uint id);
            if(id!=process) return true;
            var title=new StringBuilder(256);GetWindowText(handle,title,title.Capacity);
            if(title.ToString()=="Мириад мелодий" || title.ToString()=="Repertoire of Myriad Melodies") {found=handle;return false;}
            return true;
        },IntPtr.Zero);
        return found;
    }
    public static void Close(IntPtr handle) {PostMessage(handle,0x10,IntPtr.Zero,IntPtr.Zero);}
}
'@

$taskProcess = $null
$taskOriginalRoot = $env:DOTNET_ROOT
try {
    # A self-contained executable must start without using a DOTNET_ROOT installation.
    if(-not $RuntimeRequired){$env:DOTNET_ROOT = Join-Path (Split-Path $taskExecutable) 'no-installed-runtime'}
    $taskProcess = Start-Process -FilePath $taskExecutable -WorkingDirectory (Split-Path $taskExecutable) -WindowStyle Hidden -PassThru
    if (-not $taskProcess.WaitForInputIdle(15000)) { throw 'Application did not reach its UI loop.' }
    $taskDeadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        if ($taskProcess.HasExited) { throw "Application exited during startup: $($taskProcess.ExitCode)." }
        $taskWindow = [PublishedWindowProbe]::FindMainWindow($taskProcess.Id)
        if ($taskWindow -ne [IntPtr]::Zero) { break }
        [System.Threading.Thread]::Sleep(50)
    } while ([DateTime]::UtcNow -lt $taskDeadline)
    if ($taskWindow -eq [IntPtr]::Zero) { throw 'The published application did not create its main form.' }
    # Do not start capture or change any settings. Close through the normal application path.
    [PublishedWindowProbe]::Close($taskWindow)
    if (-not $taskProcess.WaitForExit(10000)) { throw 'Application did not close cleanly.' }
    if ($taskProcess.ExitCode -ne 0) { throw "Application exited with code $($taskProcess.ExitCode)." }
    "PASS published EXE: $(if($RuntimeRequired){'installed .NET'}else{'bundled .NET'}), main form and clean exit; no capture or keyboard input."
}
finally {
    $env:DOTNET_ROOT = $taskOriginalRoot
    if ($null -ne $taskProcess) {
        if (-not $taskProcess.HasExited) {
            $taskWindow = [PublishedWindowProbe]::FindMainWindow($taskProcess.Id)
            if ($taskWindow -ne [IntPtr]::Zero) { [PublishedWindowProbe]::Close($taskWindow) }
            if (-not $taskProcess.WaitForExit(3000)) { $taskProcess.Kill() }
        }
        $taskProcess.Dispose()
    }
}
