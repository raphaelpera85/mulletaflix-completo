<h1 align="center">MulletaFlix for Windows</h1>
<h3 align="center">Part of the MulletaFlix Project</h3>

---

MulletaFlix for Windows collects the tray application, service utilities, and NSIS installer that are used when setting up and running MulletaFlix Server with embedded MariaDB database engine.

<br/>

# Getting Started

Do you want to build MulletaFlix's tray app or installer for yourself? Read on!

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

## Building the Installer
### Requirements
* The compiled tray app from above
* [NSIS 3.x+](https://nsis.sourceforge.io/Download)
* A copy of the branding assets / repository
* The latest MulletaFlix Windows Combined package containing the server and the embedded MariaDB binaries.
* The GPLv3/GPLv2 License as a file simply named `LICENSE`

### Steps
1. Ensure that a complete copy of MulletaFlix Server is available in a folder.
2. Copy the GPL License file and place it in the same directory as the server. Ensure that it is named `LICENSE` with no extension.
3. Copy the contents of the compiled tray app (including its DLLs) into the folder with the server. If there is a duplicate DLL, skip it. We only need to add anything that isn't already included.
4. Download the branding files at a path ending with `\branding\NSIS\`:
    * modern-install.ico
    * installer-header.bmp
    * installer-right.bmp
5. Install NSIS if not already available. Be sure to select a Full install.
6. Open Powershell. Set the environment variable `InstallLocation` to the folder where MulletaFlix Server is available.
    * e.g. `$env:InstallLocation = "C:\Users\User\Downloads\mulletaflix_release"`
7. Go to the directory where NSIS is installed. In most systems, this is at `C:\Program Files (x86)\NSIS`.
8. Run the following command, substituting the path to your branding files and the NSIS script from this repository:

    ```powershell
    .\makensis /Dx64 /DUXPATH=C:\Path\To\Branding\Files "C:\Path\To\mulletaflix-server-windows\nsis\mulletaflix.nsi"
    ```

9. Wait for the installer to build. When complete, it will be located next to the NSIS script file. It is now ready to be used.
