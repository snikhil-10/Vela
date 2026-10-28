param([string]$Root=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath($Root)
Push-Location $taskRoot
try {
    if(Get-Process -Name Vela.Windows -ErrorAction SilentlyContinue) { throw 'Exit Vela normally before running desktop checks; they need its single tray identity. No process was stopped.' }
    & ([scriptblock]::Create((Get-Content -LiteralPath 'tools/source-manifest.ps1' -Raw))) -Mode Verify -Root $taskRoot
    $taskCandidate=Join-Path $taskRoot 'artifacts/Vela-x64'
    $taskCompiled=Join-Path $taskCandidate 'compiled-source-manifest.json'
    if(-not (Test-Path -LiteralPath $taskCompiled) -or (Get-FileHash -LiteralPath $taskCompiled).Hash -cne (Get-FileHash -LiteralPath 'build-source-manifest.json').Hash) { throw 'Build the current x64 source with tools\build.cmd x64 before desktop checks.' }
    $taskDllHash=(Get-FileHash -LiteralPath (Join-Path $taskCandidate 'Vela.Windows.dll')).Hash
    foreach($taskFixture in @(@{Name='SessionTools';Project='session-tools/SessionTools.csproj'},@{Name='RuntimeLifecycle';Project='runtime-lifecycle/RuntimeLifecycle.csproj'})) {
        $taskOutput=Join-Path $taskRoot ('artifacts/DesktopChecks-x64/'+$taskFixture.Name)
        if(Get-Process -Name $taskFixture.Name -ErrorAction SilentlyContinue) { throw "A $($taskFixture.Name) fixture is still running." }
        dotnet publish ('validation/fixtures/'+$taskFixture.Project) -c Release -p:Platform=x64 -r win-x64 --self-contained true -o $taskOutput
        if($LASTEXITCODE -ne 0) { throw "Failed to publish $($taskFixture.Name)." }
        foreach($taskName in @('Vela.Windows.dll','Vela.Core.dll','Vela.Windows.pri')) { Copy-Item -LiteralPath (Join-Path $taskCandidate $taskName) -Destination $taskOutput -Force }
        Copy-Item -LiteralPath (Join-Path $taskCandidate 'Vela.Windows.pri') -Destination (Join-Path $taskOutput ($taskFixture.Name+'.pri')) -Force
        foreach($taskFolder in @('Assets','Ocr')) {
            $taskDestination=Join-Path $taskOutput $taskFolder
            New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
            Get-ChildItem -LiteralPath (Join-Path $taskCandidate $taskFolder) -Force | Copy-Item -Destination $taskDestination -Recurse -Force
        }
        if((Get-FileHash -LiteralPath (Join-Path $taskOutput 'Vela.Windows.dll')).Hash -cne $taskDllHash) { throw 'The fixture DLL differs from the candidate.' }
        & (Join-Path $taskOutput ($taskFixture.Name+'.exe'))
        if($LASTEXITCODE -ne 0) { throw "$($taskFixture.Name) failed; inspect the generated component log in $taskOutput." }
        Write-Output "Passed $($taskFixture.Name) against the exact candidate DLL."
    }
}
finally { Pop-Location }
