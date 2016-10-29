$ErrorActionPreference = "Stop"


cd $PSScriptRoot

$env:DOTNET_HOME = "$PSScriptRoot/.dotnet"
$env:PATH += $env:DOTNET_HOME
mkdir $env:DOTNET_HOME -ErrorAction Ignore | Out-Null

$versions = Get-Content "$PSScriptRoot/toolversions.txt"
$channel=($(sls 'channel' $PSScriptRoot/toolversions.txt | select -exp line) -split ': ')[1]
$env:DotnetCliVersion=($(sls 'cli' $PSScriptRoot/toolversions.txt | select -exp line) -split ': ')[1]
$env:SharedFxVersion=($(sls 'sharedfx' $PSScriptRoot/toolversions.txt | select -exp line) -split ': ')[1]

function get-installer-script {
    $target = "$env:DOTNET_HOME/dotnet-install.ps1"
    if (!(test-path $target)) {
        Invoke-WebRequest https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/dotnet-install.ps1 -OutFile $target
    }
}

if ( !(Test-Path $env:DOTNET_HOME/dotnet.exe) -or "$(& $env:DOTNET_HOME/dotnet.exe --version)" -ne $env:DotnetCliVersion) {
    get-installer-script
    & $env:DOTNET_HOME/dotnet-install.ps1 -InstallDir $env:DOTNET_HOME -Version $env:DotnetCliVersion
}

if (!(Test-Path "$env:DOTNET_HOME/shared/Microsoft.NETCore.App/$env:SharedFxVersion")) {
    get-installer-script
    & $env:DOTNET_HOME/dotnet-install.ps1 -SharedRuntime -Channel $channel -InstallDir $env:DOTNET_HOME -Version $env:SharedFxVersion
}

# workaround https://github.com/dotnet/sdk/issues/203
& $env:DOTNET_HOME/dotnet.exe restore3 DotNetTools.sln

& $env:DOTNET_HOME/dotnet.exe msbuild dir.proj /nologo /v:m $args
