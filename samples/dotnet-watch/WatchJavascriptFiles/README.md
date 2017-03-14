Watch javascript files with dotnet-watch
========================================

## Prerequisites

1. Install .NET Core command line from <https://dot.net/core>

## Usage

Open a terminal to the directory containing this project.

```
dotnet restore
dotnet watch run
```

Changing the .csproj file, or \*.js file in wwwroot, or any \*.cs file will cause dotnet-watch to restart the website.
