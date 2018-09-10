[license]: https://tldrlegal.com/l/mit
[docs]: https://umod.org/documentation
[forums]: https://umod.org
[issues]: https://github.com/theumod/umod/issues
[downloads]: https://umod.org/games

# uMod [![License](http://img.shields.io/badge/license-MIT-lightgrey.svg?style=flat)][License] [![Build Status](https://ci.appveyor.com/api/projects/status/b7h4nw8t8d05jsnb?svg=true)](https://ci.appveyor.com/project/oxidemod/umod)

A complete rewrite of the popular, original Oxide API and Lua plugin framework. Previously only available for the legacy Rust game, uMod now supports numerous games. uMod's focus is on modularity and extensibility. The core is highly abstracted and loosely coupled, and could be used to mod any game that uses the .NET Framework.

Support for each game and plugin language is added via extensions. When loading, uMod scans the binary folder for DLL extensions with the format `uMod.*.dll`.

## Bundled Extensions

 * uMod.MySql - _Allows plugins to access a [MySQL](http://www.mysql.com/) database_
 * uMod.SQLite - _Allows plugins to access a [SQLite](http://www.sqlite.org/) database_
 * uMod.Unity - _Provides support for [Unity](http://unity3d.com/) powered games_
 * uMod.Rust - _Provides support for the new [Rust](http://playrust.com/) server_

## Open Source

uMod is free, open source software distributed under the [MIT License][license]. We accept and encourage contributions from our community, and sometimes give cookies in return.

## Compiling Source

While we recommend using one of the [official release builds][downloads], you can compile your own builds if you'd like. Keep in mind that only official builds are supported by the uMod team and community. _Good luck!_

 1. Download a Git client such as [GitHub Desktop](https://desktop.github.com/) or [SourceTree](https://www.sourcetreeapp.com/).

 2. Clone the repo `https://github.com/theumod/umod.Rust.git` _(recommended)_ or download and extract the [latest zip](https://github.com/theumod/umod.Rust/archive/master.zip) archive.

 3. Download and install [Visual Studio 2017](https://www.visualstudio.com/downloads/) _(community is free, but any edition will work)_ if you do not have it installed already.

 4. Update or install [PowerShell 5.x](https://www.microsoft.com/en-us/download/details.aspx?id=54616) (if it isn't already) for use with the game file downloading and patching process.

 5. Open the `uMod.sln` solution file in Visual Studio 2017.

 6. Build the solution. If you get errors, you're likely not using the latest Visual Studio 2017; which is required as uMod uses some [C# 6.0](https://github.com/dotnet/roslyn/wiki/New-Language-Features-in-C%23-6) features.

 7. Copy the files from the `Bundles` directory for your game of choice to your server installation, then just start the server!

 7a. Alternately, create a .deploy file in the project's root directory with a path to automatically deploy to.

## Getting Help

* The best place to start with plugin development is the official [API documentation][docs].
* Still need help? Search our [community forums][forums] or create a new thread if needed.

## Contributing

* Got an idea or suggestion? Use the [community forums][forums] to share and discuss it.
* Troubleshoot issues you run into on the community forums so everyone can help and reference it later.
* File detailed [issues] on GitHub (version number, what you did, and actual vs. expected outcomes).
* Want uMod and plugins for your favorite game? Hook us up and we'll see what we can do!

## Reporting Security Issues

Please disclose security issues responsibly by emailing security@umod.org with a full description. We'll work on releasing an updated version as quickly as possible. Please do not email non-security issues; use the [forums] or [issue tracker][issues] instead.
