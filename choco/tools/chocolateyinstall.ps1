$ErrorActionPreference = 'Stop'

$packageName = $env:ChocolateyPackageName
$toolsDir    = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)"

$url         = 'https://github.com/mcp-tool-shop-org/control-room/releases/download/v1.0.0/ControlRoom-win-x64.zip'
$checksum    = '<SHA256_CHECKSUM_HERE>'
$checksumType = 'sha256'

$packageArgs = @{
  PackageName    = $packageName
  Url64bit       = $url
  UnzipLocation  = $toolsDir
  Checksum64     = $checksum
  ChecksumType64 = $checksumType
}

Install-ChocolateyZipPackage @packageArgs

# Prevent shims for all executables except ControlRoom.App.exe
Get-ChildItem -Path $toolsDir -Filter '*.exe' -Recurse |
  Where-Object { $_.Name -ne 'ControlRoom.App.exe' } |
  ForEach-Object {
    $ignorePath = "$($_.FullName).ignore"
    if (-not (Test-Path $ignorePath)) {
      New-Item -ItemType File -Path $ignorePath -Force | Out-Null
    }
  }
