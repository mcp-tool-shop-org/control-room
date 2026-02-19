$ErrorActionPreference = 'Stop'

$packageName = $env:ChocolateyPackageName
$zipFileName = 'ControlRoom-win-x64.zip'

Uninstall-ChocolateyZipPackage -PackageName $packageName -ZipFileName $zipFileName
