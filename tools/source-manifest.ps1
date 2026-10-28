param(
    [ValidateSet('Verify','Refresh','List')][string]$Mode='Verify',
    [string]$Root=(Split-Path $PSScriptRoot -Parent),
    [string]$Purpose='Development source and validation inputs'
)
$ErrorActionPreference='Stop'
# Windows PowerShell launched from a newer shell can inherit that shell's
# module search path. Select its own utility module, including Get-FileHash.
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1') -Force -Global
$taskRoot=[IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
$taskManifestPath=Join-Path $taskRoot 'build-source-manifest.json'
$taskInputs=[Collections.Generic.List[string]]::new()
function Find-Inputs([string]$Directory) {
    foreach($taskEntry in Get-ChildItem -LiteralPath $Directory -Force) {
        if($taskEntry.PSIsContainer) {
            if($taskEntry.Name -in @('bin','obj','artifacts','.vs','__pycache__')) { continue }
            if(($taskEntry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "A source directory is a reparse point: $($taskEntry.FullName)" }
            Find-Inputs $taskEntry.FullName
        }
        else { $taskInputs.Add($taskEntry.FullName.Substring($taskRoot.Length+1).Replace('\','/')) }
    }
}
foreach($taskName in @('src','tests','tools')) {
    $taskDirectory=Join-Path $taskRoot $taskName
    if(-not (Test-Path -LiteralPath $taskDirectory -PathType Container)) { throw "Source directory missing: $taskName" }
    Find-Inputs $taskDirectory
}
foreach($taskName in @('validation/fixtures','.github/workflows')) {
    $taskDirectory=Join-Path $taskRoot $taskName
    if(Test-Path -LiteralPath $taskDirectory -PathType Container) { Find-Inputs $taskDirectory }
}
foreach($taskName in @('.gitattributes','.gitignore','.editorconfig','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config','Vela.sln','LICENSE','NOTICE.md','docs/feature-audit.json')) {
    if(Test-Path -LiteralPath (Join-Path $taskRoot $taskName) -PathType Leaf) { $taskInputs.Add($taskName) }
}
$taskNames=$taskInputs.ToArray()
[Array]::Sort($taskNames,[StringComparer]::Ordinal)
$taskFiles=[Collections.Specialized.OrderedDictionary]::new([StringComparer]::Ordinal)
foreach($taskName in $taskNames) { $taskFiles[$taskName]=(Get-FileHash -LiteralPath (Join-Path $taskRoot $taskName) -Algorithm SHA256).Hash.ToLowerInvariant() }
if($Mode -eq 'List') { $taskNames; return }
if($Mode -eq 'Refresh') {
    $taskParent=$null
    if(Test-Path -LiteralPath $taskManifestPath) { $taskParent=(Get-Content -LiteralPath $taskManifestPath -Raw | ConvertFrom-Json).snapshotId }
    $taskSnapshot=[ordered]@{snapshotId=[guid]::NewGuid().ToString();parentSnapshot=$taskParent;createdAt=[DateTimeOffset]::Now.ToString('o');purpose=$Purpose;files=$taskFiles}
    $taskJson=($taskSnapshot | ConvertTo-Json -Depth 10).Replace("`r`n","`n")+"`n"
    [IO.File]::WriteAllText($taskManifestPath,$taskJson,[Text.UTF8Encoding]::new($false))
    Write-Output "Recorded source snapshot $($taskSnapshot.snapshotId): $($taskNames.Length) inputs."
}
else {
    if(-not (Test-Path -LiteralPath $taskManifestPath)) { throw 'Source manifest missing. Run tools\source-manifest.cmd Refresh after reviewing the source.' }
    $taskExpected=Get-Content -LiteralPath $taskManifestPath -Raw | ConvertFrom-Json
    $taskDifferences=[Collections.Generic.List[string]]::new()
    $taskExpectedFiles=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach($taskEntry in $taskExpected.files.PSObject.Properties) {
        $taskExpectedFiles[$taskEntry.Name]=$taskEntry.Value
        if(-not $taskFiles.Contains($taskEntry.Name)) { $taskDifferences.Add('Removed or unrecognized input: '+$taskEntry.Name) }
    }
    foreach($taskName in $taskNames) {
        if(-not $taskExpectedFiles.ContainsKey($taskName)) { $taskDifferences.Add('New input: '+$taskName) }
        elseif($taskExpectedFiles[$taskName] -cne $taskFiles[$taskName]) { $taskDifferences.Add('Changed input: '+$taskName) }
    }
    if($taskDifferences.Count) { throw (($taskDifferences -join [Environment]::NewLine)+[Environment]::NewLine+'Review the changes, then run tools\source-manifest.cmd Refresh before building.') }
    Write-Output "Verified source snapshot $($taskExpected.snapshotId): $($taskNames.Length) inputs."
}
