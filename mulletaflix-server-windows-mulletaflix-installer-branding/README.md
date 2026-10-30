<h1 align="center">MulletaFlix for Windows</h1>
<h3 align="center">Part of the MulletaFlix Project</h3>

---

MulletaFlix for Windows collects the tray application, service utilities, and NSIS installer that are used when setting up and running MulletaFlix Server with embedded MariaDB database engine.

> **Legacy packaging tree:** this directory is retained for historical reference
> and is not the supported installer source. Do not build releases from this
> copy. Use `MulletaFlix-packaging-master\build-stage-and-installer.ps1`, which
> contains the current service-stop, ACL, readiness, smoke-test and validation
> fixes documented in the workspace audit.

<br/>

# Getting Started

This archived tree is not a supported source for building or installing MulletaFlix Server. Its historical NSIS installer includes global process termination and must not be compiled or used. Build releases only with `MulletaFlix-packaging-master\build-stage-and-installer.ps1`.

---

## Compiling the Tray App
### Requirements
* [.NET SDK](https://dotnet.microsoft.com/download) (Compatible with .NET 10.0 and .NET Framework 4.7.2 compiling target)

### Steps
1. Build using the dotnet command, or using Visual Studio/VS Code.
    * On the command line, in the root of the cloned repository, execute this command: `dotnet build -c Release -f net472`
2. From the resulting bin folder, collect `MulletaFlix.Windows.Tray.exe` (or `Jellyfin.Windows.Tray.exe` if not fully renamed) and all the DLLs within.
3. For use with a MulletaFlix install, place in its own directory, such as `mulletaflix-windows-tray`.

### Usage
The tray app is designed to do three things:
1. Start and Stop MulletaFlix Server
2. Open the Web UI
3. Open the Log Folder

To control MulletaFlix, it expects that either MulletaFlix is installed as a service, or that a corresponding set of registry keys has been set by the installer.

The registry entries look like the following in a typical install:

Location: `HKEY_LOCAL_MACHINE\SOFTWARE\MulletaFlix\Server` (or under `WOW6432Node` on 64-bit systems running 32-bit apps)
| Name               | Type          | Data                                 |
| ------------------ | ------------- | ------------------------------------ |
| DataFolder         | REG_EXPAND_SZ | C:\\ProgramData\\MulletaFlix\\Server |
| InstallFolder      | REG_EXPAND_SZ | C:\\Program Files\\MulletaFlix\\Server|
| ServiceAccountType | REG_SZ        | None                                 |

* DataFolder must be the location where the application support files will go (database, configs, logs, etc).
* InstallFolder must be the location where `mulletaflix.exe` (or `jellyfin.exe`) can be found.
* ServiceAccountType is "None" unless MulletaFlix is installed as a service.

---

## Building the Installer (unsupported from this archived tree)

Do not build from this archived tree. The supported release flow, validation and installer source are in `MulletaFlix-packaging-master`. This historical NSIS file still contains global process termination commands.
