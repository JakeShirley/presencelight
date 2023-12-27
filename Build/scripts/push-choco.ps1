Param
(
    [parameter(Mandatory = $true)]    [string]
    $Version,
    [parameter(Mandatory = $true)]    [string]
    $CHOCOAPIKEY
)

# Chocolatey Pack
& choco.exe pack ".\Chocolatey\PresenceLight.nuspec" --version "${Version}.0" --OutputDirectory ".\Chocolatey"

$CHOCOAPIKEY = $CHOCOAPIKEY -replace "`n","" -replace "`r","" -replace " ", ""

& choco.exe apikey --key "${CHOCOAPIKEY}" --source https://push.chocolatey.org/

$nupkgs = gci ".\Chocolatey\PresenceLight.*.nupkg" | Select -ExpandProperty FullName
foreach ($nupkg in $nupkgs) {
    & choco.exe push $nupkg --source https://push.chocolatey.org/ --debug --verbose
}