param (
    [Parameter(Mandatory=$true)][string]$project,
    [Parameter(Mandatory=$true)][string]$dotnet,
    [Parameter(Mandatory=$true)][string]$managed,
    [string]$appid = "0",
    [string]$branch = "public",
    [string]$depot = "",
    [string]$access = "anonymous",
    [string]$deobf = ""
)

Clear-Host
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Format game name and set depot ID if provided
$game_name = $project -Replace "Oxide."
if ($depot) { $depot = "-depot $depot" }

# Set directory variables and create directories
$root_dir = $PSScriptRoot
$tools_dir = "$root_dir\tools"
$project_dir = "$root_dir\src"
$deps_dir = "$project_dir\Dependencies"
$patch_dir = "$deps_dir\Patched"
$managed_dir = "$patch_dir\$managed"
New-Item "$tools_dir", "$managed_dir" -ItemType Directory -Force | Out-Null

# Set name for Oxide patcher file (.opj)
if ("$branch" -ne "public" -and (Test-Path "$root_dir\resources\$game_name-$branch.opj")) {
    $opj_name = "$root_dir\resources\$game_name-$branch.opj"
} else {
    $opj_name = "$root_dir\resources\$game_name.opj"
}

# Remove patched file(s) and replace with _Original file(s)
Get-ChildItem "$managed_dir\*_Original.*" -Recurse | ForEach-Object {
    Remove-Item $_.FullName.Replace("_Original", "")
    Rename-Item $_ $_.Name.Replace("_Original", "")
}

# TODO: Add support for GitHub API tokens for higher rate limit

function Find-Dependencies {
    # Check if project file exists for game
    if (!(Test-Path "$project.csproj")) {
        Write-Host "Could not find a .csproj file for $game_name"
        exit 1
        if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
    }

    # Copy any local dependencies
    if (Test-Path "$deps_dir\Original\*.dll") {
        Copy-Item "$deps_dir\Original\*.dll" "$managed_dir" -Force
    }

    # Check if Steam is used for game dependencies
    if ($access.ToLower() -ne "nosteam") {
        # Get project information from .csproj file
        $csproj = Get-Item "$project.csproj"
        $xml = [xml](Get-Content $csproj)
        Write-Host "Getting references for $branch branch of $appid"
        try {
            # TODO: Exclude dependencies included in repository
            $hint_path = "Dependencies\\Patched\\\$\(ManagedDir\)\\"
            ($xml.selectNodes("//Reference") | Select-Object HintPath -ExpandProperty HintPath | Select-String -Pattern "Oxide" -NotMatch) -Replace $hint_path | Out-File "$tools_dir\.references"
        } catch {
            Write-Host "Could not get references or none found in $project.csproj"
            Write-Host $_.Exception.Message
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
    $depot_dll = "$tools_dir\DepotDownloader.dll"
    if (!(Test-Path "$depot_dll") -or (Get-ChildItem "$depot_dll").CreationTime -lt (Get-Date).AddDays(-7)) {
        # Get latest release info for DepotDownloader
        Write-Host "Determining latest release of DepotDownloader"
        try {
            $json = (Invoke-WebRequest "https://api.github.com/repos/SteamRE/DepotDownloader/releases" | ConvertFrom-Json)[0]
            # TODO: Implement auth/token handling for GitHub API
            $version = $json.tag_name -Replace '\w+(\d+(?:\.\d+)+)', '$1'
            $release_zip = $json.assets[0].name
        } catch {
            Write-Host "Could not get DepotDownloader information from GitHub"
            Write-Host $_.Exception.Message
            if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            exit 1
        }

        # Download and extract DepotDownloader
        Write-Host "Downloading version $version of DepotDownloader"
        try {
            Invoke-WebRequest $json.assets[0].browser_download_url -Out "$tools_dir\$release_zip"
        } catch {
            Write-Host "Could not download DepotDownloader from GitHub"
            Write-Host $_.Exception.Message
            if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            exit 1
        }

        # TODO: Compare size and hash of .zip vs. what GitHub has via API
        Write-Host "Extracting DepotDownloader release files"
        Expand-Archive "$tools_dir\$release_zip" -DestinationPath "$tools_dir" -Force

        if (!(Test-Path "$tools_dir\DepotDownloader.dll")) {
            Get-Downloader # TODO: Add infinite loop prevention
            return
        }

        # Cleanup downloaded .zip file
        Remove-Item "$tools_dir\depotdownloader-*.zip"
    } else {
        Write-Host "Recent version of DepotDownloader already downloaded"
    }

    Get-Dependencies
}

function Get-Dependencies {
    if ($access.ToLower() -ne "nosteam") {
        # TODO: Add handling for SteamGuard code entry/use
        # Check if Steam login information is required or not
        if ($access.ToLower() -ne "anonymous") {
            if (Test-Path "$root_dir\.steamlogin") {
                $steam_login = Get-Content "$root_dir\.steamlogin"
                if ($steam_login.Length -ne 2) {
                    Write-Host "Steam username AND password not set in .steamlogin file"
                    exit 1
                    if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
                } else {
                    $login = "-username $($steam_login[0]) -password $($steam_login[1])"
                }
            } elseif ($env:STEAM_USERNAME -and $env:STEAM_PASSWORD) {
                $login = "-username $env:STEAM_USERNAME -password $env:STEAM_PASSWORD"
            } else {
                Write-Host "No Steam credentials found, skipping build for $game_name"
                exit 1
                if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            }
        }

        # Cleanup existing game files, else they aren't always the latest
        #Remove-Item $managed_dir -Include *.dll, *.exe -Exclude "Oxide.Core.dll" -Verbose –Force

        # TODO: Check for and compare Steam buildid before downloading again

        # Attempt to run DepotDownloader to get game DLLs
        try {
            $depot_process = Start-Process dotnet -ArgumentList "$tools_dir\DepotDownloader.dll $login -app $appid -branch $branch $depot -dir $patch_dir -filelist $tools_dir\.references" -NoNewWindow -PassThru
            try
            {
                $depot_process | Wait-Process -Timeout 30 -ErrorAction Stop
            }
            catch
            {
                $depot_process | Stop-Process -Force
            }
        } catch {
            Write-Host "Could not start or complete DepotDownloader process"
            Write-Host $_.Exception.Message
            if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            exit 1
        }

        # TODO: Store Steam buildid somewhere for comparison during next check
        # TODO: Confirm all dependencies were downloaded (no 0kb files), else stop/retry and error with details
    }

    # TODO: Check Oxide.Core.dll version and update if needed
    # Grab latest Oxide.Core.dll build
    Write-Host "Copying latest build of Oxide.Core.dll for $game_name"
    #$core_version = Get-ChildItem -Directory $core_path | Where-Object { $_.PSIsContainer } | Sort-Object CreationTime -desc | Select-Object -f 1
    if (!(Test-Path "$tools_dir\Oxide.Core.dll")) {
        try {
            Copy-Item "$root_dir\packages\oxide.core\*\lib\$dotnet\Oxide.Core.dll" "$tools_dir" -Force
        } catch {
            Write-Host "Could not copy Oxide.Core.dll to $tools_dir"
            Write-Host $_.Exception.Message
            if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            exit 1
        }
    }
    Write-Host "Copying latest build of Oxide.Core.dll for OxidePatcher"
    if (!(Test-Path "$managed_dir\Oxide.Core.dll")) {
        try {
            Copy-Item "$root_dir\packages\oxide.core\*\lib\$dotnet\Oxide.Core.dll" "$managed_dir" -Force
        } catch {
            Write-Host "Could not copy Oxide.Core.dll to $managed_dir"
            Write-Host $_.Exception.Message
            if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            exit 1
        }
    }

    if ($deobf) {
        Get-Deobfuscators
    } else {
        Get-Patcher
    }
}

function Get-Deobfuscators {
    # Check for which deobfuscator to get and use
    if ($deobf.ToLower() -eq "de4dot") {
        $de4dot_dir = "$tools_dir\.de4dot"
        New-Item "$de4dot_dir" -ItemType Directory -Force | Out-Null

        # Check if de4dot is already downloaded
        $de4dot_exe = "$de4dot_dir\de4dot.exe"
        if (!(Test-Path "$de4dot_exe") -or (Get-ChildItem "$de4dot_exe").CreationTime -lt (Get-Date).AddDays(-7)) {
            # Download and extract de4dot
            Write-Host "Downloading latest version of de4dot" # TODO: Get and show version
            try {
                Invoke-WebRequest "https://ci.appveyor.com/api/projects/0xd4d/de4dot/artifacts/de4dot.zip" -Out "$de4dot_dir\de4dot.zip"
            } catch {
                Write-Host "Could not download de4dot from AppVeyor"
                Write-Host $_.Exception.Message
                if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
                exit 1
            }

            # TODO: Compare size and hash of .zip vs. what AppVeyor has via API
            Write-Host "Extracting de4dot release files"
            Expand-Archive "$de4dot_dir\de4dot.zip" -DestinationPath "$de4dot_dir" -Force

            if (!(Test-Path "$de4dot_exe")) {
                Get-Deobfuscators # TODO: Add infinite loop prevention
                return
            }

            # Cleanup downloaded .zip file
            Remove-Item "$de4dot_dir\de4dot.zip"
        } else {
            Write-Host "Recent version of de4dot already downloaded"
        }

        Start-Deobfuscator
    }
}

function Start-Deobfuscator {
    if ($deobf.ToLower() -eq "de4dot") {
        # Attempt to deobfuscate game file(s)
        try {
            Start-Process "$tools_dir\.de4dot\de4dot.exe" -WorkingDirectory "$managed_dir" -ArgumentList "-r $managed_dir -ru" -NoNewWindow -Wait
        } catch {
            Write-Host "Could not start or complete de4dot deobufcation process"
            Write-Host $_.Exception.Message
            if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            exit 1
        }

        # Remove obfuscated file(s) and replace with cleaned file(s)
        Get-ChildItem "$managed_dir\*-cleaned.*" -Recurse | ForEach-Object {
            Remove-Item $_.FullName.Replace("-cleaned", "")
            Rename-Item $_ $_.Name.Replace("-cleaned", "")
        }
    }

    Get-Patcher
}

function Get-Patcher {
    # TODO: MD5 comparision of local OxidePatcher.exe and remote header
    # Check if OxidePatcher is already downloaded
    $patcher_exe = "$tools_dir\OxidePatcher.exe"
    if (!(Test-Path "$patcher_exe") -or (Get-ChildItem "$patcher_exe").CreationTime -lt (Get-Date).AddDays(-7)) {
        # Download latest Oxide Patcher build
        Write-Host "Downloading latest build of OxidePatcher"
        $patcher_url = "https://github.com/OxideMod/OxidePatcher/releases/download/latest/OxidePatcher.exe"
        try {
            Invoke-WebRequest $patcher_url -Out "$patcher_exe"
        } catch {
            Write-Host "Could not download OxidePatcher.exe from GitHub"
            Write-Host $_.Exception.Message
            if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
            exit 1
        }
    } else {
        Write-Host "Recent build of OxidePatcher already downloaded"
    }

    Start-Patcher
}

function Start-Patcher {
    # Check if we need to get the Oxide patcher
    if (!(Test-Path "$tools_dir\OxidePatcher.exe")) {
        Get-Patcher # TODO: Add infinite loop prevention
        return
    }

    # TODO: Make sure dependencies exist before trying to patch

    # Attempt to patch game using the Oxide patcher
    try {
        Start-Process "$tools_dir\OxidePatcher.exe" -WorkingDirectory "$managed_dir" -ArgumentList "-c -p `"$managed_dir`" $opj_name" -NoNewWindow -Wait
    } catch {
        Write-Host "Could not start or complete OxidePatcher process"
        Write-Host $_.Exception.Message
        if ($LastExitCode -ne 0) { $host.SetShouldExit($LastExitCode) }
        exit 1
    }
}

Find-Dependencies
