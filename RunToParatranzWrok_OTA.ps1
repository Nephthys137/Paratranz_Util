Set-Location "Debug"
Remove-Item Localize/ -Recurse
Remove-Item Localize_OTA/ -Recurse
Start-Process "LLC_Paratranz_Util.exe" -Wait

$commitMessage = $(Get-Date)
git add Localize/*
git commit -m $commitMessage
$changedFiles=$(git diff --name-only HEAD HEAD^ -- Localize/)

New-Item -Name "Localize_OTA" -ItemType "directory" -Force
$changedFilesList = $changedFiles -split " "
foreach ($file in $changedFilesList) {
    if (Test-Path -Path $file) {
        $destination = "Localize_OTA/$file"
        $destination = $destination.Replace("Localize/", "")
        $destinationDirectory = Split-Path -Path $destination -Parent
        if (!(Test-Path -Path $destinationDirectory)) {
            New-Item -ItemType Directory -Force -Path $destinationDirectory
        }
        Copy-Item -Path $file -Destination $destination -Force -Recurse
    }
}