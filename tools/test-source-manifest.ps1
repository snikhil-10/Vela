param([string]$Root=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$taskVerifier=[scriptblock]::Create((Get-Content -LiteralPath (Join-Path $Root 'tools/source-manifest.ps1') -Raw))
$taskTemporary=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$taskFixture=Join-Path $taskTemporary ('Vela-Manifest-Test-'+[guid]::NewGuid().ToString('N'))
$taskChecks=[Collections.Generic.List[string]]::new()
function Require([bool]$Condition,[string]$Reason) { if(-not $Condition) { throw $Reason } }
function Verify-Fails([string]$Expected) {
    $taskRejected=$false
    try { & $taskVerifier -Root $taskFixture -Mode Verify | Out-Null }
    catch { $taskRejected=$_.Exception.Message.Contains($Expected) }
    Require $taskRejected ('Manifest did not reject '+$Expected)
    $taskChecks.Add($Expected)
}
try {
    foreach($taskName in @('src','tests','tools','src/bin','src/obj')) { New-Item -ItemType Directory -Path (Join-Path $taskFixture $taskName) -Force | Out-Null }
    $taskSource=Join-Path $taskFixture 'src/actual.cs'
    [IO.File]::WriteAllText($taskSource,'source A')
    & $taskVerifier -Root $taskFixture -Mode Refresh | Out-Null
    $taskManifest=Join-Path $taskFixture 'build-source-manifest.json'
    $taskBefore=(Get-FileHash -LiteralPath $taskManifest).Hash
    $taskParent=(Get-Content -LiteralPath $taskManifest -Raw | ConvertFrom-Json).snapshotId
    & $taskVerifier -Root $taskFixture -Mode Verify | Out-Null
    Require ($taskBefore -ceq (Get-FileHash -LiteralPath $taskManifest).Hash) 'Verify modified a manifest.'
    $taskChecks.Add('Verify is read-only')
    $taskListed=@(& $taskVerifier -Root $taskFixture -Mode List)
    Require ($taskListed.Count -eq 1 -and $taskListed[0] -ceq 'src/actual.cs' -and $taskBefore -ceq (Get-FileHash -LiteralPath $taskManifest).Hash) 'List changed the manifest or omitted the discovered source.'
    $taskChecks.Add('Read-only inventory listing')
    [IO.File]::WriteAllText($taskSource,'source B')
    Verify-Fails 'Changed input: src/actual.cs'
    & $taskVerifier -Root $taskFixture -Mode Refresh | Out-Null
    $taskNew=Get-Content -LiteralPath $taskManifest -Raw | ConvertFrom-Json
    Require ($taskNew.parentSnapshot -eq $taskParent -and $taskNew.snapshotId -ne $taskParent) 'Refresh lost the parent identity.'
    $taskChecks.Add('Parent identity')
    [IO.File]::WriteAllText((Join-Path $taskFixture 'src/extra.cs'),'new source')
    Verify-Fails 'New input: src/extra.cs'
    & $taskVerifier -Root $taskFixture -Mode Refresh | Out-Null
    Remove-Item -LiteralPath (Join-Path $taskFixture 'src/extra.cs')
    Verify-Fails 'Removed or unrecognized input: src/extra.cs'
    & $taskVerifier -Root $taskFixture -Mode Refresh | Out-Null
    [IO.File]::WriteAllText((Join-Path $taskFixture 'src/bin/generated.cs'),'ignored build output')
    [IO.File]::WriteAllText((Join-Path $taskFixture 'src/obj/generated.cs'),'ignored build output')
    & $taskVerifier -Root $taskFixture -Mode Verify | Out-Null
    $taskChecks.Add('Generated output exclusion')
    $taskChanged=Get-Content -LiteralPath $taskManifest -Raw | ConvertFrom-Json
    $taskChanged.files | Add-Member -NotePropertyName '../outside.cs' -NotePropertyValue ('0'*64)
    $taskChanged | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $taskManifest -Encoding UTF8
    Verify-Fails 'Removed or unrecognized input: ../outside.cs'
    Write-Output "$($taskChecks.Count) source-manifest checks passed; 0 failed."
}
finally {
    $taskResolved=[IO.Path]::GetFullPath($taskFixture)
    if([IO.Path]::GetDirectoryName($taskResolved) -ne $taskTemporary -or [IO.Path]::GetFileName($taskResolved) -notmatch '^Vela-Manifest-Test-[a-f0-9]{32}$') { throw 'Refusing cleanup outside the generated test directory.' }
    if(Test-Path -LiteralPath $taskResolved) { Remove-Item -LiteralPath $taskResolved -Recurse -Force }
}
