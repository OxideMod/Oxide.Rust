#Requires -Version 6.0

param (
    [Parameter(Mandatory=$true)][string]$game_name,
    [Parameter(Mandatory=$true)][string]$dotnet,
    [Parameter(Mandatory=$true)][string]$target_dir,
    [Parameter(Mandatory=$true)][string]$managed_dir,
    [string]$platform = "windows",
    [string]$deobfuscator = "",
    [string]$steam_appid = "0",
    [string]$steam_branch = "public",
    [string]$steam_depot = "",
    [string]$steam_access = "anonymous"
)

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Format project name and set depot ID if provided
$project = "Oxide." + $game_name
if ($steam_depot) { $steam_depot = "-depot $steam_depot" }

# Set directory/file variables and create directories
$root_dir = $PSScriptRoot
$tools_dir = Join-Path $root_dir "tools"
$project_dir = Join-Path $root_dir "src"
$resources_dir = Join-Path $root_dir "resources"
$deps_dir = Join-Path $project_dir "Dependencies"
$platform_dir = Join-Path $deps_dir $platform
$managed_dir = Join-Path $platform_dir $managed_dir # TODO: Make sure passed path is Linux-compatible
$patcher_exe = Join-Path $tools_dir "uModPatcherConsole.exe"
$references_file = Join-Path $tools_dir ".references"
New-Item "$tools_dir", "$managed_dir" -ItemType Directory -Force | Out-Null

# Set URLs of dependencies and tools to download
$steam_depotdl_url = "https://github.com/SteamRE/DepotDownloader/releases/download/DepotDownloader_2.4.5/depotdownloader-2.4.5.zip"
$de4dot_url = "https://github.com/0xd4d/de4dot/suites/507020524/artifacts/2658127" # TODO: Replace expired artifact
$patcher_url = "https://github.com/OxideMod/Oxide.Patcher/releases/download/latest/uModPatcherConsole.exe"

# Set file path for patcher file (.opj)
$patcher_file = Join-Path $resources_dir "$game_name.opj"

# Set project file and get contents
$csproj = Get-Item "$project.csproj"
$xml = [xml](Get-Content $csproj)

# Remove patched file(s) and replace with _Original file(s)
Get-ChildItem (Join-Path $managed_dir "*_Original.*") -Recurse | ForEach-Object {
    Remove-Item $_.FullName.Replace("_Original", "")
    Rename-Item $_ $_.Name.Replace("_Original", "")
}

# TODO: Add support for GitHub API tokens for higher rate limit

function Find-Dependencies {
    # Check if project file exists for game
    if (!(Test-Path "$project.csproj")) {
        Write-Host "Error: Could not find a .csproj file for $game_name"
        if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
        exit 1
    }

    # Copy any local dependencies
    $deps_pattern = Join-Path $deps_dir "Original" "*.dll"
    if (Test-Path $deps_pattern) {
        Copy-Item $deps_pattern $managed_dir -Force
    }

    # Check if Steam is used for game dependencies
    if ($steam_access.ToLower() -ne "nosteam") {
        # Get references from .csproj file
        Write-Host "Getting references for $steam_branch branch of $steam_appid"
        try {
            # TODO: Exclude dependencies included in repository
            ($xml.selectNodes("//Reference") | Select-Object Include -ExpandProperty Include) -Replace "\S+$", "regex:$&.dll" | Out-File $references_file
            Write-Host "References:" ((Get-Content $references_file).Replace('regex:', '') -Join ', ')
        } catch {
            Write-Host "Error: Could not get references or none found in $project.csproj"
            Write-Host $_.Exception | Format-List -Force
            if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            exit 1
        }

        Get-Downloader
    } else {
        Get-Dependencies
    }
}

function Get-Downloader {
    # Check if DepotDownloader is already downloaded
    $steam_depotdl_dll = Join-Path $tools_dir "DepotDownloader.dll"
    $steam_depotdl_zip = Join-Path $tools_dir "DepotDownloader.zip"
    if (!(Test-Path $steam_depotdl_dll) -or (Get-Item $steam_depotdl_dll).LastWriteTime -lt (Get-Date).AddDays(-7)) {
        # Download and extract DepotDownloader
        Write-Host "Downloading latest version of DepotDownloader"
        try {
            Start-BitsTransfer $steam_depotdl_url $steam_depotdl_zip
        } catch {
            Write-Host "Error: Could not download DepotDownloader"
            Write-Host $_.Exception | Format-List -Force
            if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            exit 1
        }

        # TODO: Compare size and hash of .zip vs. what GitHub has via API
        Write-Host "Extracting DepotDownloader release files"
        Expand-Archive $steam_depotdl_zip -DestinationPath $tools_dir -Force

        if (!(Test-Path $steam_depotdl_zip)) {
            Get-Downloader # TODO: Add infinite loop prevention
            return
        }

        # Cleanup downloaded .zip file
        Remove-Item $steam_depotdl_zip
    } else {
        Write-Host "Recent version of DepotDownloader already downloaded"
    }

    Get-Dependencies
}

function Get-Dependencies {
    if ($steam_access.ToLower() -ne "nosteam") {
        # TODO: Add handling for SteamGuard code entry

        # Check if Steam login information is required or not
        $steam_file = Join-Path $root_dir ".steamlogin"
        if ($steam_access.ToLower() -ne "anonymous") {
            if (Test-Path $steam_file) {
                $steam_login = Get-Content $steam_file
                if ($steam_login.Length -ne 2) {
                    Write-Host "Steam username and password not set in .steamlogin file"
                    if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
                    exit 1
                } else {
                    $steam_access = "-username $($steam_login[0]) -password $($steam_login[1])"
                }
            } elseif ($env:STEAM_USERNAME -and $env:STEAM_PASSWORD) {
                $steam_access = "-username $env:STEAM_USERNAME -password $env:STEAM_PASSWORD"
            } else {
                Write-Host "Error: No Steam credentials found, skipping build for $game_name"
                if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
                exit 1
            }
        }

        # Cleanup existing game files, else they are not always the latest
        #Remove-Item $managed_dir -Recurse -Force

        # TODO: Check for and compare Steam buildid before downloading again

        # Attempt to run DepotDownloader to get game DLLs
        try {
            Write-Host "$steam_access -app $steam_appid -branch $steam_branch $steam_depot -os $platform -dir $deps_dir"
            Start-Process dotnet -WorkingDirectory $tools_dir -ArgumentList "$steam_depotdl_dll $steam_access -app $steam_appid -branch $steam_branch $steam_depot -os $platform -dir $platform_dir -filelist $references_file" -NoNewWindow -Wait
        } catch {
            Write-Host "Error: Could not start or complete getting dependencies"
            Write-Host $_.Exception | Format-List -Force
            if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            exit 1
        }

        # TODO: Store Steam buildid somewhere for comparison during next check

        # TODO: Confirm all dependencies were downloaded (no 0kb files), else stop or retry and error with details
    }

    # Get package references from .csproj file
    Write-Host "Getting package references for $game_name"
    $packages = $null
    try {
        $packages = $xml.selectNodes("//PackageReference") | Select-Object Include,Version
        Write-Host "Packages:" (($packages | Select-Object -ExpandProperty Include) -Join ', ')
    } catch {
        Write-Host "Error: Could not get package references or none found in $project.csproj"
        Write-Host $_.Exception | Format-List -Force
        if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
        exit 1
    }

    # Copy latest package references for patching
    Write-Host "Copying latest package references for $game_name"
    try {
        # Copy package references specified in .csproj file
        ForEach ($package in $packages) {
            Write-Host "Copying package reference $($package.Include) $($package.Version)..."
            $lib = Join-Path $root_dir "packages" $package.Include.ToLower() $package.Version.ToLower() "lib" $dotnet "$($package.Include).dll"
            Copy-Item $lib $managed_dir -Force
            Copy-Item $lib $tools_dir -Force
        }
    } catch {
        Write-Host "Error: Could not copy one or more dependencies to $deps_dir or $tools_dir"
        Write-Host $_.Exception | Format-List -Force
        if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
        exit 1
    }

    if ($deobfuscator) {
        Get-Deobfuscators
    } else {
        Get-Patcher
    }
}

function Get-Deobfuscators {
    # Check for which deobfuscator to get and use
    if ($deobfuscator.ToLower() -eq "de4dot") {
        $de4dot_dir = Join-Path $tools_dir ".de4dot"
        $de4dot_dll = Join-Path $de4dot_dir "de4dot.dll"
        $de4dot_zip = Join-Path $de4dot_dir "de4dot.zip"
        New-Item $de4dot_dir -ItemType Directory -Force | Out-Null

        # Check if de4dot is already downloaded
        if (!(Test-Path $de4dot_dll) -or (Get-Item $de4dot_dll).LastWriteTime -lt (Get-Date).AddDays(-7)) {
            # Download and extract de4dot
            Write-Host "Downloading latest version of de4dot" # TODO: Get and show version
            try {
                Start-BitsTransfer $de4dot_url $de4dot_zip
            } catch {
                Write-Host "Error: Could not download de4dot"
                Write-Host $_.Exception | Format-List -Force
                if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
                exit 1
            }

            # TODO: Compare size and hash of .zip vs. what GitHub has via API
            Write-Host "Extracting de4dot release files"
            Expand-Archive "$de4dot_dir\de4dot.zip" -DestinationPath "$de4dot_dir" -Force
            Move-Item "$de4dot_dir\de4dot-net35\*" $de4dot_dir
            Remove-Item "$de4dot_dir\de4dot-net35"

            if (!(Test-Path $de4dot_dll)) {
                Get-Deobfuscators # TODO: Add infinite loop prevention
                return
            }

            # Cleanup downloaded .zip file
            Remove-Item $de4dot_zip
        } else {
            Write-Host "Recent version of de4dot already downloaded"
        }

        Start-Deobfuscator
    }
}

function Start-Deobfuscator {
    if ($deobfuscator.ToLower() -eq "de4dot") {
        # Attempt to deobfuscate game file(s)
        try {
            Start-Process dotnet -WorkingDirectory $managed_dir -ArgumentList "$de4dot_dll -r $managed_dir -ru" -NoNewWindow -Wait
        } catch {
            Write-Host "Error: Could not start or complete deobufcation process"
            Write-Host $_.Exception | Format-List -Force
            if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            exit 1;
        }

        # Remove obfuscated file(s) and replace with cleaned file(s)
        Get-ChildItem (Join-Path $managed_dir "*-cleaned.*") -Recurse | ForEach-Object {
            Remove-Item $_.FullName.Replace("-cleaned", "")
            Rename-Item $_ $_.Name.Replace("-cleaned", "")
            Write-Host "Deobfuscated and cleaned $_ file to patch"
        }
    }

    Get-Patcher
}

function Get-Patcher {
    # TODO: MD5 comparison of local patcher file and remote header
    # Check if patcher is already downloaded
    #if (!(Test-Path $patcher_exe) -or (Get-Item $patcher_exe).LastWriteTime -lt (Get-Date).AddDays(-7)) {
        # Download latest patcher build
        Write-Host "Downloading latest patcher"
        try {
            Start-BitsTransfer $patcher_url $patcher_exe
        } catch {
            Write-Host "Error: Could not download patcher"
            Write-Host $_.Exception | Format-List -Force
            if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            exit 1
        }
    #} else {
    #    Write-Host "Recent build of patcher already downloaded"
    #}

    Start-Patcher
}

function Start-Patcher {
    # Check if we need to get the patcher
    if (!(Test-Path $patcher_exe)) {
        Get-Patcher # TODO: Add infinite loop prevention
        return
    }

    # TODO: Make sure dependencies exist before trying to patch

    # Attempt to patch game using the patcher
    try {
        if ($IsLinux) {
            Start-Process mono -WorkingDirectory $managed_dir -ArgumentList "$patcher_exe $patcher_file -d `"$managed_dir`"" -NoNewWindow -Wait
        } elseif ($IsWindows) {
            Start-Process $patcher_exe -WorkingDirectory $managed_dir -ArgumentList "$patcher_file -d `"$managed_dir`"" -NoNewWindow -Wait
        }
    } catch {
        Write-Host "Error: Could not start or complete patching process"
        Write-Host $_.Exception | Format-List -Force
        if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
        exit 1
    }
}

Find-Dependencies
