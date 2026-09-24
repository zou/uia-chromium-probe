$ErrorActionPreference = 'Continue'
New-Item -ItemType Directory -Force results | Out-Null
$page = (Resolve-Path page.html).Path
$url = 'file:///' + ($page -replace '\\', '/')
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
$electron = (Resolve-Path electron-app\node_modules\electron\dist\electron.exe).Path
$eapp = (Resolve-Path electron-app).Path
$probe = (Resolve-Path out\probe\probe.exe).Path
$wv2 = (Resolve-Path out\wv2\wv2app.exe).Path
$t = $env:RUNNER_TEMP

function Probe($name, $exe, $argline) {
  Write-Host "=== $name"
  & $probe $name $exe $argline "results\$name.json"
  Start-Sleep -Seconds 2
}

# Order: the three hosts with no accessibility flag (the question), then the forced controls.
Probe 'chrome'          $chrome   "--user-data-dir=$t\c1 --no-first-run --no-default-browser-check --app=$url"
Probe 'electron'        $electron "$eapp"
Probe 'webview2'        $wv2      "$url"
Probe 'chrome-forced'   $chrome   "--user-data-dir=$t\c2 --no-first-run --no-default-browser-check --force-renderer-accessibility --app=$url"
Probe 'electron-forced' $electron "--force-renderer-accessibility $eapp"

$rows = foreach ($f in Get-ChildItem results\*.json) {
  $j = Get-Content $f | ConvertFrom-Json
  [pscustomobject]@{ target = $j.target; foreground = $j.foregroundIsTarget; title = $j.windowTitleAfter;
    nodes = $j.treeNodes; textarea = $j.foundTextarea; rich = $j.foundRich; error = $j.error }
}
$rows | Format-Table -AutoSize | Out-String -Width 300 | Tee-Object results\summary.txt
