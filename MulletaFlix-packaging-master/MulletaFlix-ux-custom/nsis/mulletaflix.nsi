!verbose 3
;SetCompressor /SOLID bzip2 TODO Review if this is best option
ShowInstDetails show
ShowUninstDetails show
Unicode True

!define SF_USELECTED  0 ; used to check selected options status, rest are inherited from Sections.nsh
!define INSTDIR_REG_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\MulletaFlixServer" ;Registry to show up in Add/Remove Programs
!define INSTDIR_REG_ROOT "HKLM" ;Define root hive to use
!define INSTALL_DIRECTORY "$PROGRAMFILES64\MulletaFlix\Server"

!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "Sections.nsh"
!include "LogicLib.nsh"
!addplugindir "plugins"
!include "helpers\nsProcess.nsh"
!include "helpers\ShowError.nsh"

; Global variables that we'll use
    Var _MULLETAFLIXVERSION_
    Var _MULLETAFLIXDATADIR_
    Var _SETUPTYPE_
    Var _INSTALLSERVICE_
    Var _SERVICESTART_
    Var _SERVICEACCOUNTTYPE_
    Var _EXISTINGINSTALLATION_
    Var _EXPLICITINSTALLDIR_
    Var _EXISTINGSERVICE_
    Var _MAKESHORTCUTS_
    Var _FOLDEREXISTS_
    Var _DELETE_DATA_



;--------------------------------


!define REG_CONFIG_KEY "Software\MulletaFlix\Server" ;Registry to store all configuration

!getdllversion "${InstallLocation}\\MulletaFlix.dll" ver_ ;Align installer version with MulletaFlix.dll version

Name "MulletaFlix Server ${ver_1}.${ver_2}.${ver_3}" ; This is referred in various header text labels
OutFile "mulletaflix_${ver_1}.${ver_2}.${ver_3}_windows-x64.exe" ; Naming convention mulletaflix_{version}_windows-x64.exe
BrandingText "MulletaFlix Server ${ver_1}.${ver_2}.${ver_3} Installer" ; This shows in just over the buttons

; installer attributes, these show up in details tab on installer properties
VIProductVersion "${ver_1}.${ver_2}.${ver_3}.0" ; VIProductVersion format, should be X.X.X.X
VIFileVersion "${ver_1}.${ver_2}.${ver_3}.0" ; VIFileVersion format, should be X.X.X.X
VIAddVersionKey "ProductName" "MulletaFlix Server"
VIAddVersionKey "FileVersion" "${ver_1}.${ver_2}.${ver_3}.0"
VIAddVersionKey "LegalCopyright" "(c) 2024 MulletaFlix Contributors. Code released under the GNU General Public License."
VIAddVersionKey "FileDescription" "MulletaFlix Server: The Free Software Media System"

;TODO, check defaults
InstallDir ${INSTALL_DIRECTORY} ;Default installation folder
InstallDirRegKey HKLM "${REG_CONFIG_KEY}" "InstallFolder" ;Read the registry for install folder,

RequestExecutionLevel admin ; ask it upfront for service control, and installing in priv folders

CRCCheck on ; make sure the installer wasn't corrupted while downloading

!define MUI_ABORTWARNING ;Prompts user in case of aborting install

!ifdef UXPATH
    !define MUI_ICON "${UXPATH}\branding\NSIS\modern-install.ico" ; Installer Icon
    !define MUI_UNICON "${UXPATH}\branding\NSIS\modern-install.ico" ; Uninstaller Icon

    !define MUI_HEADERIMAGE
    !define MUI_HEADERIMAGE_BITMAP "${UXPATH}\branding\NSIS\installer-header.bmp"
    !define MUI_WELCOMEFINISHPAGE_BITMAP "${UXPATH}\branding\NSIS\installer-right.bmp"
    !define MUI_UNWELCOMEFINISHPAGE_BITMAP "${UXPATH}\branding\NSIS\installer-right.bmp"
!endif

;--------------------------------
;Pages

; Welcome Page
    !define MUI_WELCOMEPAGE_TEXT "The installer will ask for details to install MulletaFlix Server."
    !insertmacro MUI_PAGE_WELCOME

; License Page
    !insertmacro MUI_PAGE_LICENSE "${InstallLocation}\\LICENSE" ; picking up generic GPL

; Setup Type Page
    Page custom ShowSetupTypePage SetupTypePage_Config

; Components Page
    !define MUI_PAGE_CUSTOMFUNCTION_PRE HideComponentsPage
    !insertmacro MUI_PAGE_COMPONENTS

; Folder Warning Page
    Page custom ShowFolderWarningPage

; Install folder page
    !define MUI_PAGE_CUSTOMFUNCTION_PRE HideInstallDirectoryPage ; Controls when to hide / show
    !define MUI_DIRECTORYPAGE_TEXT_DESTINATION "Install folder" ; shows just above the folder selection dialog
    !define MUI_DIRECTORYPAGE_TEXT_TOP "Setup will install MulletaFlix in the following folder."
    !insertmacro MUI_PAGE_DIRECTORY

; Data folder Page
    !define MUI_PAGE_CUSTOMFUNCTION_PRE HideDataDirectoryPage ; Controls when to hide / show
    !define MUI_PAGE_HEADER_TEXT "Choose Data Location"
    !define MUI_PAGE_HEADER_SUBTEXT "Choose the folder in which to install the MulletaFlix Server data."
    !define MUI_DIRECTORYPAGE_TEXT_TOP "Setup will set the following folder for MulletaFlix Server data.$\nDo not choose the server install folder."
    !define MUI_DIRECTORYPAGE_TEXT_DESTINATION "Data folder"
    !define MUI_DIRECTORYPAGE_VARIABLE $_MULLETAFLIXDATADIR_
    !insertmacro MUI_PAGE_DIRECTORY

; Custom Dialogs
    !include "dialogs\setuptype.nsdinc"
    !include "dialogs\service-config.nsdinc"
    !include "dialogs\confirmation.nsdinc"
    !include "dialogs\warning.nsdinc"

; Select service account type
    #!define MUI_PAGE_CUSTOMFUNCTION_PRE HideServiceConfigPage ; Controls when to hide / show (This does not work for Page, might need to go PageEx)
    #!define MUI_PAGE_CUSTOMFUNCTION_SHOW fnc_service_config_Show
    #!define MUI_PAGE_CUSTOMFUNCTION_LEAVE ServiceConfigPage_Config
    #!insertmacro MUI_PAGE_CUSTOM ServiceAccountType
    Page custom ShowServiceConfigPage ServiceConfigPage_Config

; Confirmation Page
    Page custom ShowConfirmationPage ; just letting the user know what they chose to install

; Actual Installion Page
    !insertmacro MUI_PAGE_INSTFILES

    !insertmacro MUI_UNPAGE_CONFIRM
    !insertmacro MUI_UNPAGE_INSTFILES
    #!insertmacro MUI_UNPAGE_FINISH

;--------------------------------
;Languages; Add more languages later here if needed

    !insertmacro MUI_LANGUAGE "English"

;--------------------------------
;Installer Sections

Function StopRunningMulletaFlixProcesses
    DetailPrint "Stopping running MulletaFlix processes before install..."
    SetOutPath "$PLUGINSDIR"
    File "/oname=stop-mulletaflix-processes.ps1" "${UXPATH}\nsis\stop-mulletaflix-processes.ps1"
    ExecWait 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PLUGINSDIR\stop-mulletaflix-processes.ps1" -InstallDirectory "$INSTDIR" -DataDirectory "$_MULLETAFLIXDATADIR_"' $0
    ${If} $0 <> 0
        DetailPrint "MulletaFlix process cleanup returned $0; continuing with SCM shutdown."
    ${EndIf}
    ; Nebula runs from Python and can keep .pyd files locked during upgrades.
    ; Filter by the installed Nebula path so unrelated Python processes survive.
    SetOutPath "$PLUGINSDIR"
    File "/oname=stop-nebula-processes.ps1" "${UXPATH}\nsis\stop-nebula-processes.ps1"
    ExecWait 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PLUGINSDIR\stop-nebula-processes.ps1" -InstallDirectory "$INSTDIR"' $0
    ; Garantia adicional via taskkill para liberar arquivos bloqueados
    nsExec::Exec 'cmd.exe /c taskkill /F /IM MulletaFlix.exe /IM MulletaFlix.Windows.Tray.exe /IM mysqld.exe /IM mariadbd.exe /IM rclone.exe >nul 2>&1'
    Sleep 1500
FunctionEnd

Function WaitForMulletaFlixServiceStopped
    SetOutPath "$PLUGINSDIR"
    File "/oname=wait-mulletaflix-service.ps1" "${UXPATH}\nsis\wait-mulletaflix-service.ps1"
    ExecWait 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PLUGINSDIR\wait-mulletaflix-service.ps1"' $0
    ${If} $0 <> 0
        DetailPrint "MulletaFlix service did not reach Stopped state ($0)."
    ${EndIf}
FunctionEnd

; NSIS compiles the uninstaller into a separate function namespace.
Function un.StopRunningMulletaFlixProcesses
    DetailPrint "Stopping running MulletaFlix processes before uninstall..."
    SetOutPath "$PLUGINSDIR"
    File "/oname=stop-mulletaflix-processes.ps1" "${UXPATH}\nsis\stop-mulletaflix-processes.ps1"
    ExecWait 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PLUGINSDIR\stop-mulletaflix-processes.ps1" -InstallDirectory "$INSTDIR" -DataDirectory "$_MULLETAFLIXDATADIR_"' $0
    ${If} $0 <> 0
        DetailPrint "MulletaFlix process cleanup returned $0; continuing with uninstall."
    ${EndIf}
    ; Nebula's Python process can lock files below the selected install root.
    SetOutPath "$PLUGINSDIR"
    File "/oname=stop-nebula-processes.ps1" "${UXPATH}\nsis\stop-nebula-processes.ps1"
    ExecWait 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PLUGINSDIR\stop-nebula-processes.ps1" -InstallDirectory "$INSTDIR"' $0
    nsExec::Exec 'cmd.exe /c taskkill /F /IM MulletaFlix.exe /IM MulletaFlix.Windows.Tray.exe /IM mysqld.exe /IM mariadbd.exe /IM rclone.exe >nul 2>&1'
    Sleep 1500
FunctionEnd

Function un.WaitForMulletaFlixServiceStopped
    SetOutPath "$PLUGINSDIR"
    File "/oname=wait-mulletaflix-service.ps1" "${UXPATH}\nsis\wait-mulletaflix-service.ps1"
    ExecWait 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PLUGINSDIR\wait-mulletaflix-service.ps1"' $0
    ${If} $0 <> 0
        DetailPrint "MulletaFlix service did not reach Stopped state ($0)."
    ${EndIf}
FunctionEnd

Section "!MulletaFlix Server (required)" InstallMulletaFlixServer
    SectionIn RO ; Mandatory section, isn't this the whole purpose to run the installer.

    StrCmp "$_EXISTINGINSTALLATION_" "Yes" PrepareUpgrade CarryOn

    PrepareUpgrade:
        ; Never launch the previous Uninstall.exe during an upgrade. That
        ; executable belongs to the old installation and may still contain
        ; obsolete NSSM commands. Stop/remove the service through SCM first,
        ; then overwrite the installation in place while preserving data.
        DetailPrint "Preparing existing MulletaFlix installation for upgrade..."
        ExecWait 'sc.exe query "MulletaFlixServer"' $0
        ${If} $0 <> 1060
            ExecWait 'sc.exe stop "MulletaFlixServer"' $0
            DetailPrint "MulletaFlix Server service stop request, $0"
            Call WaitForMulletaFlixServiceStopped
            ${If} $0 <> 0
                MessageBox MB_OK|MB_ICONSTOP "The MulletaFlix Server service did not stop within the timeout. Close applications using the server and retry."
                Abort
            ${EndIf}
            ExecWait 'sc.exe delete "MulletaFlixServer"' $0
            ${If} $0 <> 0
                MessageBox MB_OK|MB_ICONSTOP "Could not remove the previous MulletaFlix Server service."
                Abort
            ${EndIf}
            DetailPrint "Removed previous MulletaFlix Server service, $0"
        ${EndIf}
        Call StopRunningMulletaFlixProcesses
        ExecWait 'netsh advfirewall firewall delete rule name="MulletaFlix MariaDB"' $0
        ExecWait 'netsh advfirewall firewall delete rule name="MulletaFlix MongoDB"' $0
        ExecWait 'netsh http delete urlacl url=http://+:2123/' $0
        ExecWait 'netsh http delete urlacl url=http://127.0.0.1:2123/' $0
        ExecWait 'netsh http delete urlacl url=http://localhost:2123/' $0

    CarryOn:
        ${If} $_EXISTINGINSTALLATION_ != 'Yes'
            Call StopRunningMulletaFlixProcesses
        ${EndIf}

    ; Basic installs run as the interactive user instead of a Windows
    ; service. Ensure a custom/basic data path is writable by that user;
    ; service installs receive the service-account ACL in the service section.
    ${If} $_INSTALLSERVICE_ == "No"
        CreateDirectory "$_MULLETAFLIXDATADIR_"
        CreateDirectory "$_MULLETAFLIXDATADIR_\data"
        CreateDirectory "$_MULLETAFLIXDATADIR_\config"
        CreateDirectory "$_MULLETAFLIXDATADIR_\cache"
        ExecWait 'icacls "$_MULLETAFLIXDATADIR_" /inheritance:e /grant "$%USERDOMAIN%\$%USERNAME%":(OI)(CI)M /T /C' $0
        ${If} $0 <> 0
            MessageBox MB_OK|MB_ICONSTOP "Could not grant the current user access to the MulletaFlix data folder."
            Abort
        ${EndIf}
    ${EndIf}

    ; -------------------------------------------------------------
    ; Limpa qualquer pasta legada nebula / .venv de instalações antigas com Python
    ; -------------------------------------------------------------
    DetailPrint "Removendo resquicios de instalacoes antigas do Nebula em Python..."
    RMDir /r "$INSTDIR\nebula"
    ClearErrors

    SetOutPath "$INSTDIR"

    File "/oname=icon.ico" "${UXPATH}\branding\NSIS\modern-install.ico"
    File /r "${InstallLocation}\*"


    ; Write the InstallFolder, DataFolder, Network Service info into the registry for later use
    WriteRegExpandStr HKLM "${REG_CONFIG_KEY}" "InstallFolder" "$INSTDIR"
    WriteRegExpandStr HKLM "${REG_CONFIG_KEY}" "DataFolder" "$_MULLETAFLIXDATADIR_"
    WriteRegStr HKLM "${REG_CONFIG_KEY}" "ServiceAccountType" "$_SERVICEACCOUNTTYPE_"

    !getdllversion "${InstallLocation}\MulletaFlix.dll" ver_
    StrCpy $_MULLETAFLIXVERSION_ "${ver_1}.${ver_2}.${ver_3}" ;

    ; Write the uninstall keys for Windows
    WriteRegStr HKLM "${INSTDIR_REG_KEY}" "DisplayName" "MulletaFlix Server $_MULLETAFLIXVERSION_"
    WriteRegExpandStr HKLM "${INSTDIR_REG_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegStr HKLM "${INSTDIR_REG_KEY}" "DisplayIcon" '"$INSTDIR\Uninstall.exe",0'
    WriteRegStr HKLM "${INSTDIR_REG_KEY}" "Publisher" "MulletaFlix"
    WriteRegStr HKLM "${INSTDIR_REG_KEY}" "URLInfoAbout" "https://mulletaflix.local/"
    WriteRegStr HKLM "${INSTDIR_REG_KEY}" "DisplayVersion" "$_MULLETAFLIXVERSION_"
    WriteRegDWORD HKLM "${INSTDIR_REG_KEY}" "NoModify" 1
    WriteRegDWORD HKLM "${INSTDIR_REG_KEY}" "NoRepair" 1

    ; The embedded MariaDB is bound to loopback and must not be reachable from
    ; the network. Remove a broad rule left by older installers.
    ExecWait 'netsh advfirewall firewall delete rule name="MulletaFlix MariaDB"' $0

    ; HttpListener usa HTTP.sys. Reserve a porta padrão do Nebula para que o
    ; streaming HTTP em LAN não falhe com "Access is denied".
    DetailPrint "Configurando reserva HTTP do Nebula (porta 2123)..."
    ; Permit only the supported Windows service identities to reserve the
    ; HTTP.sys prefix. Granting WD allowed any local user to bind this port.
    ${If} $_INSTALLSERVICE_ == "No"
        ; Basic installs run as the interactive user. Grant that user only
        ; loopback prefixes; do not broaden the LAN wildcard reservation.
        ExecWait 'netsh http add urlacl url=http://127.0.0.1:2123/ user="$%USERDOMAIN%\$%USERNAME%"' $0
        DetailPrint "Reserva HTTP loopback para a instalação básica, $0"
        ExecWait 'netsh http add urlacl url=http://localhost:2123/ user="$%USERDOMAIN%\$%USERNAME%"' $0
        DetailPrint "Reserva HTTP localhost para a instalação básica, $0"
    ${Else}
        ExecWait 'netsh http add urlacl url=http://+:2123/ sddl=D:(A;;GX;;;S-1-5-20)(A;;GX;;;S-1-5-18)(A;;GX;;;S-1-5-19)' $0
    ${EndIf}

    ; -------------------------------------------------------------
    ; Verificação e instalação do Python (necessário para o helper de montagem N:)
    ; -------------------------------------------------------------
    DetailPrint "Verificando o Python, requisito do Nebula..."
    ${If} ${FileExists} "$INSTDIR\install-python-if-missing.ps1"
        ExecWait 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\install-python-if-missing.ps1"' $0
        ${If} $0 <> 0
            MessageBox MB_OK|MB_ICONSTOP "O Python é necessário para montar a unidade N:. Instalação interrompida."
            Abort
        ${EndIf}
    ${Else}
        MessageBox MB_OK|MB_ICONSTOP "O instalador não contém o instalador do Python. Gere o instalador novamente."
        Abort
    ${EndIf}

    ; -------------------------------------------------------------
    ; Verificação e Instalação Automática do MongoDB
    ; -------------------------------------------------------------
    DetailPrint "Verificando se o MongoDB esta instalado no sistema..."
    DetailPrint "Verificando instalacao e estado do servico MongoDB..."
    ${If} ${FileExists} "$INSTDIR\install-mongodb-if-missing.ps1"
        ExecWait 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\install-mongodb-if-missing.ps1"' $0
        DetailPrint "Processo de verificacao do MongoDB finalizado com codigo: $0"
        ${If} $0 <> 0
            MessageBox MB_OK|MB_ICONSTOP "O MongoDB nao iniciou corretamente. A instalacao nao pode continuar."
            Abort
        ${EndIf}
    ${Else}
        MessageBox MB_OK|MB_ICONSTOP "O instalador nao contem o verificador do MongoDB. Gere o instalador novamente."
        Abort
    ${EndIf}

    ; -------------------------------------------------------------
    ; Verificação e instalação do WinFsp (necessário para rclone mount)
    ; -------------------------------------------------------------
    DetailPrint "Verificando o WinFsp, requisito da montagem da unidade N:..."
    ${If} ${FileExists} "$INSTDIR\install-winfsp-if-missing.ps1"
        ExecWait 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\install-winfsp-if-missing.ps1"' $0
        ${If} $0 <> 0
            MessageBox MB_OK|MB_ICONSTOP "O WinFsp é necessário para montar a unidade N:. Instalação interrompida."
            Abort
        ${EndIf}
    ${Else}
        MessageBox MB_OK|MB_ICONSTOP "O instalador não contém o verificador do WinFsp. Gere o instalador novamente."
        Abort
    ${EndIf}

    ; MongoDB is consumed locally by Nebula. Remove any broad rule created
    ; by earlier installers rather than exposing port 27017 to the network.
    ExecWait 'netsh advfirewall firewall delete rule name="MulletaFlix MongoDB"' $0

    ; Create uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "MulletaFlix Server Service" InstallService
${If} $_INSTALLSERVICE_ == "Yes" ; Only run this if we're going to install the service!
    ; The installer runs elevated, but the service account can vary between
    ; fresh installs, upgrades, and existing NSSM registrations. Use
    ; well-known SIDs instead of localized account names so icacls works on
    ; Portuguese, English, and other Windows installations. Create the
    ; subdirectories explicitly because migrations write config/system.xml
    ; during the first startup. Grant all supported service identities so an
    ; existing service account cannot leave the installation unwritable.
    CreateDirectory "$_MULLETAFLIXDATADIR_"
    CreateDirectory "$_MULLETAFLIXDATADIR_\data"
    CreateDirectory "$_MULLETAFLIXDATADIR_\config"
    CreateDirectory "$_MULLETAFLIXDATADIR_\cache"
    ExecWait 'icacls "$_MULLETAFLIXDATADIR_" /inheritance:e /grant *S-1-5-20:(OI)(CI)M /grant *S-1-5-18:(OI)(CI)M /grant *S-1-5-19:(OI)(CI)M /T /C' $0
    ${If} $0 <> 0
        MessageBox MB_OK|MB_ICONSTOP "Could not grant the MulletaFlix service access to the data folder."
        Abort
    ${EndIf}

    ; NSSM 2.24 has no `statuscode` command; using it opens the NSSM usage
    ; dialog and makes fresh installs follow the wrong service branch. Query
    ; the Windows service manager directly instead. Error 1060 means absent.
    ExecWait 'sc.exe query "MulletaFlixServer"' $0
    DetailPrint "MulletaFlix Server service query, $0"
    ${If} $0 <> 0
        InstallRetry:
        ExecWait '"$INSTDIR\nssm.exe" install MulletaFlixServer "$INSTDIR\MulletaFlix.exe" --service --datadir \"$_MULLETAFLIXDATADIR_\"' $0
        ${If} $0 <> 0
            !insertmacro ShowError "Could not install the MulletaFlix Server service." InstallRetry
        ${EndIf}
        DetailPrint "MulletaFlix Server Service install, $0"
    ${Else}
        DetailPrint "MulletaFlix Server Service exists, updating..."

        ConfigureApplicationRetry:
        ExecWait '"$INSTDIR\nssm.exe" set MulletaFlixServer Application "$INSTDIR\MulletaFlix.exe"' $0
        ${If} $0 <> 0
            !insertmacro ShowError "Could not configure the MulletaFlix Server service." ConfigureApplicationRetry
        ${EndIf}
        DetailPrint "MulletaFlix Server Service setting (Application), $0"

        ConfigureAppParametersRetry:
        ExecWait '"$INSTDIR\nssm.exe" set MulletaFlixServer AppParameters --service --datadir \"$_MULLETAFLIXDATADIR_\"' $0
        ${If} $0 <> 0
            !insertmacro ShowError "Could not configure the MulletaFlix Server service." ConfigureAppParametersRetry
        ${EndIf}
        DetailPrint "MulletaFlix Server Service setting (AppParameters), $0"
    ${EndIf}


    Sleep 3000 ; Give time for Windows to catchup
    ConfigureStartRetry:
    ExecWait '"$INSTDIR\nssm.exe" set MulletaFlixServer Start SERVICE_AUTO_START' $0
    ${If} $0 <> 0
        !insertmacro ShowError "Could not configure the MulletaFlix Server service." ConfigureStartRetry
    ${EndIf}
    DetailPrint "MulletaFlix Server Service setting (Start), $0"

    ConfigureDescriptionRetry:
    ExecWait '"$INSTDIR\nssm.exe" set MulletaFlixServer Description "MulletaFlix Server: The Free Software Media System"' $0
    ${If} $0 <> 0
        !insertmacro ShowError "Could not configure the MulletaFlix Server service." ConfigureDescriptionRetry
    ${EndIf}
    DetailPrint "MulletaFlix Server Service setting (Description), $0"
    ConfigureDisplayNameRetry:
    ExecWait '"$INSTDIR\nssm.exe" set MulletaFlixServer DisplayName "MulletaFlix Server"' $0
    ${If} $0 <> 0
        !insertmacro ShowError "Could not configure the MulletaFlix Server service." ConfigureDisplayNameRetry

    ${EndIf}
    DetailPrint "MulletaFlix Server Service setting (DisplayName), $0"

    Sleep 3000
    ${If} $_SERVICEACCOUNTTYPE_ == "NetworkService" ; the default install using NSSM is Local System
        ConfigureNetworkServiceRetry:
        ExecWait '"$INSTDIR\nssm.exe" set MulletaFlixServer Objectname "NT Authority\NetworkService"' $0
        ${If} $0 <> 0
            !insertmacro ShowError "Could not configure the MulletaFlix Server service account." ConfigureNetworkServiceRetry
        ${EndIf}
        DetailPrint "MulletaFlix Server service account change, $0"
    ${EndIf}

    Sleep 3000
    ConfigureDefaultAppExit:
        ExecWait '"$INSTDIR\nssm.exe" set MulletaFlixServer AppExit Default Exit' $0
        ${If} $0 <> 0
            !insertmacro ShowError "Could not configure the MulletaFlix Server service app exit action." ConfigureDefaultAppExit
        ${EndIf}
        DetailPrint "MulletaFlix Server service exit action set, $0"
${EndIf}
SectionEnd

Section "-start service" StartService
${If} $_SERVICESTART_ == "Yes"
${AndIf} $_INSTALLSERVICE_ == "Yes"
    StartRetry:
    ExecWait '"$INSTDIR\nssm.exe" start MulletaFlixServer' $0
    ${If} $0 <> 0
        !insertmacro ShowError "Could not start the MulletaFlix Server service." StartRetry
    ${EndIf}
    DetailPrint "MulletaFlix Server service start, $0"
${EndIf}
SectionEnd

Section "Create Shortcuts" CreateWinShortcuts
    ${If} $_MAKESHORTCUTS_ == "Yes"
        CreateDirectory "$SMPROGRAMS\MulletaFlix Server"
        CreateShortCut "$SMPROGRAMS\MulletaFlix Server\MulletaFlix (View Console).lnk" "$INSTDIR\MulletaFlix.exe" "--datadir $\"$_MULLETAFLIXDATADIR_$\"" "$INSTDIR\icon.ico" 0 SW_SHOWMAXIMIZED
        CreateShortCut "$SMPROGRAMS\MulletaFlix Server\MulletaFlix Tray App.lnk" "$INSTDIR\mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe" "" "$INSTDIR\icon.ico" 0
        ;CreateShortCut "$DESKTOP\MulletaFlix Server.lnk" "$INSTDIR\MulletaFlix.exe" "--datadir $\"$_MULLETAFLIXDATADIR_$\"" "$INSTDIR\icon.ico" 0 SW_SHOWMINIMIZED
        CreateShortCut "$DESKTOP\MulletaFlix Server.lnk" "$INSTDIR\mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe" "" "$INSTDIR\icon.ico" 0
    ${EndIf}
SectionEnd

;--------------------------------
;Descriptions

;Language strings
    LangString DESC_InstallMulletaFlixServer ${LANG_ENGLISH} "Install MulletaFlix Server"
    LangString DESC_InstallService ${LANG_ENGLISH} "Install As a Service"

;Assign language strings to sections
    !insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${InstallMulletaFlixServer} $(DESC_InstallMulletaFlixServer)
    !insertmacro MUI_DESCRIPTION_TEXT ${InstallService} $(DESC_InstallService)
    !insertmacro MUI_FUNCTION_DESCRIPTION_END

;--------------------------------
;Uninstaller Section

Section "Uninstall"

    ReadRegStr $INSTDIR HKLM "${REG_CONFIG_KEY}" "InstallFolder"  ; read the installation folder
    ReadRegStr $_MULLETAFLIXDATADIR_ HKLM "${REG_CONFIG_KEY}" "DataFolder"  ; read the data folder
    ReadRegStr $_SERVICEACCOUNTTYPE_ HKLM "${REG_CONFIG_KEY}" "ServiceAccountType"  ; read the account name
    StrCpy $_DELETE_DATA_ "No"

    DetailPrint "MulletaFlix Install location: $INSTDIR"
    DetailPrint "MulletaFlix Data folder: $_MULLETAFLIXDATADIR_"

    MessageBox MB_YESNO|MB_ICONINFORMATION "Do you want to keep the MulletaFlix Server data folder? $\r$\nIf unsure choose YES." /SD IDYES IDYES PreserveData IDNO DeleteConfirmation

    DeleteConfirmation:
    MessageBox MB_YESNOCANCEL|MB_ICONEXCLAMATION "Are you sure? Everything in $\r$\n$_MULLETAFLIXDATADIR_ $\r$\nwill be deleted. $\r$\nIf you are sure, press YES." IDYES DeleteData IDNO PreserveData ;IDCANCEL StopNow

    DeleteData:
    ; Defer deletion until the service has been stopped and removed. Deleting
    ; first could leave locked database/config files behind.
    StrCpy $_DELETE_DATA_ "Yes"

    ;StopNow:
    ;    Abort

    PreserveData:
    ; noop

    ; Query SCM instead of the unsupported NSSM `statuscode` command. NSSM
    ; would show its Usage dialog and falsely route upgrades to service stop.
    ExecWait 'sc.exe query "MulletaFlixServer"' $0
    DetailPrint "MulletaFlix Server service query, $0"
    ${If} $0 == 1060
        Goto NoServiceUninstall
    ${EndIf}

    Sleep 3000 ; Give time for Windows to catchup

    ; Use the Windows Service Control Manager directly. NSSM's stop/remove
    ; commands can show its Usage dialog or fail for an already-stopped
    ; service, which made uninstall/upgrade appear stuck.
    UninstallStopRetry:
    ExecWait 'sc.exe stop "MulletaFlixServer"' $0
    ${If} $0 <> 0
    ${AndIf} $0 <> 1060 ; service does not exist
    ${AndIf} $0 <> 1062 ; service is already stopped
        !insertmacro ShowError "Could not stop the MulletaFlix Server service." UninstallStopRetry
    ${EndIf}
    DetailPrint "MulletaFlix Server stop request, $0"
    Call un.WaitForMulletaFlixServiceStopped
    ${If} $0 <> 0
        !insertmacro ShowError "Could not confirm that the MulletaFlix Server service stopped." UninstallStopRetry
    ${EndIf}
    UninstallRemoveRetry:
    ExecWait 'sc.exe delete "MulletaFlixServer"' $0
    ${If} $0 <> 0
    ${AndIf} $0 <> 1060 ; service was removed concurrently
        !insertmacro ShowError "Could not remove the MulletaFlix Server service." UninstallRemoveRetry
    ${EndIf}
    DetailPrint "MulletaFlix Server delete request, $0"

    ; Stop only processes owned by this installation. Never use /IM here:
    ; another MulletaFlix/MariaDB instance may belong to a different install.
    Call un.StopRunningMulletaFlixProcesses

    ; Remove MariaDB firewall rule
    ExecWait 'netsh advfirewall firewall delete rule name="MulletaFlix MariaDB"' $0
    ExecWait 'netsh http delete urlacl url=http://+:2123/' $0
    ExecWait 'netsh http delete urlacl url=http://127.0.0.1:2123/' $0
    ExecWait 'netsh http delete urlacl url=http://localhost:2123/' $0

    Sleep 3000 ; Give time for Windows to catchup

    NoServiceUninstall: ; existing install was present but no service was detected. Remove shortcuts if account is set to none
        ${If} $_DELETE_DATA_ == "Yes"
            DetailPrint "Removing MulletaFlix data after service shutdown..."
            RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\mariadb_data"
            RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\cache"
            RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\config"
            RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\data"
            RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\log"
            RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\metadata"
            RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\plugins"
            RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\root"
            RMDir /REBOOTOK "$_MULLETAFLIXDATADIR_"     ; Delete final dir only if empty
        ${EndIf}
        ${If} $_SERVICEACCOUNTTYPE_ == "None"
            RMDir /r "$SMPROGRAMS\MulletaFlix Server"
            Delete "$DESKTOP\MulletaFlix Server.lnk"
            DetailPrint "Removed old shortcuts..."
        ${EndIf}

    DeleteRegKey HKLM "Software\MulletaFlix"
    DeleteRegKey HKLM "${INSTDIR_REG_KEY}"
SectionEnd

Function .onInit
; Setting up defaults
    StrCpy $_INSTALLSERVICE_ "Yes"
    StrCpy $_SERVICESTART_ "Yes"
    StrCpy $_SERVICEACCOUNTTYPE_ "NetworkService"
    StrCpy $_EXISTINGINSTALLATION_ "No"
    StrCpy $_EXPLICITINSTALLDIR_ "No"
    StrCpy $_EXISTINGSERVICE_ "No"
    StrCpy $_MAKESHORTCUTS_ "No"

    SetShellVarContext current
    StrCpy $_MULLETAFLIXDATADIR_ "$%ProgramData%\MulletaFlix\Server"

    ; An explicit NSIS /D= path is used by clean-install smoke tests and must
    ; take precedence over a previous installation recorded in the registry.
    ; Normal upgrades without /D= continue to reuse the registered path.
    ${GetParameters} $R1
    ClearErrors
    ${GetOptions} "$R1" "/D=" $R2
    ${IfNot} ${Errors}
        StrCpy $_EXPLICITINSTALLDIR_ "Yes"
        StrCpy $INSTDIR $R2
    ${EndIf}

    ; The clean-install smoke test supplies an isolated data directory. Keep
    ; the option explicit so a test cannot accidentally write into ProgramData.
    ClearErrors
    ${GetOptions} "$R1" "/DATA=" $R2
    ${IfNot} ${Errors}
        StrCpy $_MULLETAFLIXDATADIR_ $R2
    ${EndIf}

    ; Test installs must never inspect or modify the machine's existing
    ; installation recorded in HKLM. This is intentionally opt-in and is
    ; used only by isolated smoke tests and automation.
    ClearErrors
    ${GetOptions} "$R1" "/TESTMODE" $R2
    ${IfNot} ${Errors}
        Goto NoExisitingInstall
    ${EndIf}

    ; This blocks another installer from running at the same time
    System::Call 'kernel32::CreateMutex(p 0, i 0, t "MulletaFlixServerMutex") p .r1 ?e'
    Pop $R0
    StrCmp $R0 0 +3
    !insertmacro ShowErrorFinal "The installer is already running."

;Detect if MulletaFlix is already installed.
; In case it is installed, let the user choose either
;	1. Exit installer
;   2. Upgrade without messing with data
; 		2a. Don't ask for any details, uninstall and install afresh with old settings

; Read Registry for previous installation
    ${If} $_EXPLICITINSTALLDIR_ == "Yes"
        Goto NoExisitingInstall
    ${EndIf}

    ClearErrors
    ReadRegStr "$0" HKLM "${REG_CONFIG_KEY}" "InstallFolder"
    IfErrors NoExisitingInstall

    DetailPrint "Existing MulletaFlix Server detected at: $0"
    StrCpy "$INSTDIR" "$0" ; set the location fro registry as new default

    StrCpy $_EXISTINGINSTALLATION_ "Yes" ; Set our flag to be used later
    SectionSetText ${InstallMulletaFlixServer} "Upgrade MulletaFlix Server (required)" ; Change install text to "Upgrade"

    ; check if service was run using Network Service account
    ClearErrors
    ReadRegStr $_SERVICEACCOUNTTYPE_ HKLM "${REG_CONFIG_KEY}" "ServiceAccountType" ; in case of error _SERVICEACCOUNTTYPE_ will be NetworkService as default

    ClearErrors
    ReadRegStr $_MULLETAFLIXDATADIR_ HKLM "${REG_CONFIG_KEY}" "DataFolder" ; in case of error, the default holds

    ; Hide sections which will not be needed in case of previous install
    ; SectionSetText ${InstallService} ""

    ; Check the service through SCM. Error 1060 means the existing install
    ; has no service and may be running from the desktop shortcut.
    ExecWait 'sc.exe query "MulletaFlixServer"' $0
    DetailPrint "MulletaFlix Server service query, $0"
    ${If} $0 == 1060
        Goto NoService
    ${EndIf}

    ; if service was detected, set defaults going forward.
    StrCpy $_EXISTINGSERVICE_ "Yes"
    StrCpy $_INSTALLSERVICE_ "Yes"
    StrCpy $_SERVICESTART_ "Yes"
    StrCpy $_MAKESHORTCUTS_ "No"
    SectionSetText ${CreateWinShortcuts} ""

    NoService: ; existing install was present but no service was detected
        ${If} $_SERVICEACCOUNTTYPE_ == "None"
            StrCpy $_SETUPTYPE_ "Basic"
            StrCpy $_INSTALLSERVICE_ "No"
            StrCpy $_SERVICESTART_ "No"
            StrCpy $_MAKESHORTCUTS_ "Yes"
            ; The installation section stops only processes belonging to this
            ; install/data pair. Do not abort the upgrade beforehand: doing so
            ; leaves the old desktop server running and serving its old web bundle.
        ${EndIf}

    ; Let the user know that we'll upgrade and provide an option to quit
    MessageBox MB_OKCANCEL|MB_ICONINFORMATION "Existing installation of MulletaFlix Server was detected, it'll be upgraded, settings will be retained. \
    $\r$\nClick OK to proceed, Cancel to exit installer." /SD IDOK IDOK ProceedWithUpgrade
    Quit ; Quit if the user is not sure about upgrade

    ProceedWithUpgrade:

    NoExisitingInstall: ; by this time, the variables have been correctly set to reflect previous install details
FunctionEnd

Function HideFolderWarningPage
    ${If} $_EXISTINGINSTALLATION_ == "Yes" ; Existing installation detected, so don't warn for folder directories
        Abort
    ${EndIf}
FunctionEnd

Function HideInstallDirectoryPage
    ${If} $_EXISTINGINSTALLATION_ == "Yes" ; Existing installation detected, so don't ask for InstallFolder
        Abort
    ${EndIf}
FunctionEnd

Function HideDataDirectoryPage
    ${If} $_EXISTINGINSTALLATION_ == "Yes" ; Existing installation detected, so don't ask for DataFolder
        Abort
    ${EndIf}
FunctionEnd

Function HideServiceConfigPage
    ${If} $_INSTALLSERVICE_ == "No" ; Not running as a service, don't ask for service type
    ${OrIf} $_EXISTINGINSTALLATION_ == "Yes" ; Existing installation detected, so don't ask for InstallFolder
        Abort
    ${EndIf}
FunctionEnd

Function HideConfirmationPage
    ${If} $_EXISTINGINSTALLATION_ == "Yes" ; Existing installation detected, so don't ask for InstallFolder
        Abort
    ${EndIf}
FunctionEnd

Function HideSetupTypePage
    ${If} $_EXISTINGINSTALLATION_ == "Yes" ; Existing installation detected, so don't ask for SetupType
        Abort
    ${EndIf}
FunctionEnd

Function HideComponentsPage
     ${If} $_SETUPTYPE_ == "Basic" ; Basic installation chosen, don't show components choice
        Abort
    ${EndIf}
FunctionEnd

; Setup Type dialog show function
Function ShowSetupTypePage
  Call HideSetupTypePage
  Call fnc_setuptype_Show
FunctionEnd

; Folder Warning dialog show function
Function ShowFolderWarningPage
  Call HideFolderWarningPage
  Call fnc_warning_Show
FunctionEnd

; Service Config dialog show function
Function ShowServiceConfigPage
  Call HideServiceConfigPage
  Call fnc_service_config_Create
  nsDialogs::Show
FunctionEnd

; Confirmation dialog show function
Function ShowConfirmationPage
  Call HideConfirmationPage
  Call fnc_confirmation_Create
  nsDialogs::Show
FunctionEnd

; Declare temp variables to read the options from the custom page.
Var StartServiceAfterInstall
Var UseNetworkServiceAccount
Var UseLocalSystemAccount
Var BasicInstall


Function SetupTypePage_Config
${NSD_GetState} $hCtl_setuptype_BasicInstall $BasicInstall
 IfFileExists "$LOCALAPPDATA\MulletaFlix" folderfound foldernotfound ; if the folder exists, use this, otherwise, go with new default
        folderfound:
            StrCpy $_FOLDEREXISTS_ "Yes"
            Goto InstallCheck
        foldernotfound:
            StrCpy $_FOLDEREXISTS_ "No"
            Goto InstallCheck

InstallCheck:
${If} $BasicInstall == 1
    StrCpy $_SETUPTYPE_ "Basic"
    StrCpy $_INSTALLSERVICE_ "No"
    StrCpy $_SERVICESTART_ "No"
    StrCpy $_SERVICEACCOUNTTYPE_ "None"
    StrCpy $_MAKESHORTCUTS_ "Yes"
    ; Basic mode is per-user and must not default to ProgramData, where an
    ; unelevated interactive server cannot write MariaDB/InnoDB files.
    StrCpy $_MULLETAFLIXDATADIR_ "$LOCALAPPDATA\MulletaFlix\"
${Else}
    StrCpy $_SETUPTYPE_ "Advanced"
    StrCpy $_INSTALLSERVICE_ "Yes"
    StrCpy $_MAKESHORTCUTS_ "No"
    ${If} $_FOLDEREXISTS_ == "Yes"
            MessageBox MB_OKCANCEL|MB_ICONINFORMATION "An existing data folder was detected.\
            $\r$\nBasic Setup is highly recommended.\
            $\r$\nIf you proceed, you will need to set up MulletaFlix again." IDOK GoAhead IDCANCEL GoBack
        GoBack:
            Abort
    ${EndIf}
        GoAhead:
            StrCpy $_MULLETAFLIXDATADIR_ "$%ProgramData%\MulletaFlix\Server"
            SectionSetText ${CreateWinShortcuts} ""
${EndIf}
FunctionEnd

Function ServiceConfigPage_Config
${NSD_GetState} $hCtl_service_config_StartServiceAfterInstall $StartServiceAfterInstall
${If} $StartServiceAfterInstall == 1
    StrCpy $_SERVICESTART_ "Yes"
${Else}
    StrCpy $_SERVICESTART_ "No"
${EndIf}
${NSD_GetState} $hCtl_service_config_UseNetworkServiceAccount $UseNetworkServiceAccount
${NSD_GetState} $hCtl_service_config_UseLocalSystemAccount $UseLocalSystemAccount

${If} $UseNetworkServiceAccount == 1
    StrCpy $_SERVICEACCOUNTTYPE_ "NetworkService"
${ElseIf} $UseLocalSystemAccount == 1
    StrCpy $_SERVICEACCOUNTTYPE_ "LocalSystem"
${Else}
    !insertmacro ShowErrorFinal "Service account type not properly configured."
${EndIf}

FunctionEnd

; This function handles the choices during component selection
Function .onSelChange

; If we are not installing service, we don't need to set the NetworkService account or StartService
    SectionGetFlags ${InstallService} $0
    ${If} $0 = ${SF_SELECTED}
        StrCpy $_INSTALLSERVICE_ "Yes"
    ${Else}
        StrCpy $_INSTALLSERVICE_ "No"
        StrCpy $_SERVICESTART_ "No"
        StrCpy $_SERVICEACCOUNTTYPE_ "None"
    ${EndIf}
FunctionEnd

Function .onInstSuccess
    ; Basic installs do not create a Windows service. Start the tray host so it
    ; can launch MulletaFlix.exe and keep the local server available after setup.
    ${If} $_INSTALLSERVICE_ == "No"
        ${If} ${FileExists} "$INSTDIR\mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe"
            Exec '"$INSTDIR\mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe"'
            DetailPrint "MulletaFlix Tray App started for basic installation"
        ${Else}
            DetailPrint "MulletaFlix Tray App not found; server must be started manually"
        ${EndIf}
    ${EndIf}
FunctionEnd
