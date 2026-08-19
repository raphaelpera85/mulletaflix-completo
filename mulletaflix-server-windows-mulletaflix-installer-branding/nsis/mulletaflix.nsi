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
    Var _EXISTINGSERVICE_
    Var _MAKESHORTCUTS_
    Var _FOLDEREXISTS_



;--------------------------------


!define REG_CONFIG_KEY "Software\MulletaFlix\Server" ;Registry to store all configuration

!getdllversion "$%InstallLocation%\MulletaFlix.dll" ver_ ;Align installer version with MulletaFlix.dll version

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
    !insertmacro MUI_PAGE_LICENSE "$%InstallLocation%\LICENSE" ; picking up generic GPL

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
    ExecWait 'TaskKill /IM MulletaFlix.Windows.Tray.exe /F /T' $0
    ExecWait 'TaskKill /IM MulletaFlix.exe /F /T' $0
    ExecWait 'TaskKill /IM mysqld.exe /F /T' $0
    ExecWait 'TaskKill /IM mariadbd.exe /F /T' $0
    Sleep 3000
FunctionEnd

Section "!MulletaFlix Server (required)" InstallMulletaFlixServer
    SectionIn RO ; Mandatory section, isn't this the whole purpose to run the installer.

    StrCmp "$_EXISTINGINSTALLATION_" "Yes" RunUninstaller ; Silently uninstall in case of previous installation

    RunUninstaller:
    DetailPrint "Looking for uninstaller at $INSTDIR"
    FindFirst $0 $1 "$INSTDIR\Uninstall.exe"
    FindClose $0
    StrCmp $1 "" CarryOn ; the registry key was there but uninstaller was not found

    DetailPrint "Silently running the uninstaller at $INSTDIR"
    ExecWait '"$INSTDIR\Uninstall.exe" /S _?=$INSTDIR' $0
    DetailPrint "Uninstall finished, $0"

    CarryOn: ; We should never hit this under normal circumstances. We should probably rewrite this
        Call StopRunningMulletaFlixProcesses
        ${If} $_EXISTINGSERVICE_ == 'Yes'
            ExecWait '"$INSTDIR\nssm.exe" stop MulletaFlixServer' $0
            ${If} $0 <> 0
                MessageBox MB_OK|MB_ICONSTOP "Could not stop the MulletaFlix Server service."
                Abort
            ${EndIf}
            DetailPrint "Stopped MulletaFlix Server service, $0"
        ${EndIf}

    SetOutPath "$INSTDIR"


    File "/oname=icon.ico" "${UXPATH}\branding\NSIS\modern-install.ico"
    File /r "$%InstallLocation%\*"


    ; Write the InstallFolder, DataFolder, Network Service info into the registry for later use
    WriteRegExpandStr HKLM "${REG_CONFIG_KEY}" "InstallFolder" "$INSTDIR"
    WriteRegExpandStr HKLM "${REG_CONFIG_KEY}" "DataFolder" "$_MULLETAFLIXDATADIR_"
    WriteRegStr HKLM "${REG_CONFIG_KEY}" "ServiceAccountType" "$_SERVICEACCOUNTTYPE_"

    !getdllversion "$%InstallLocation%\MulletaFlix.dll" ver_
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

    ; Allow MariaDB through firewall
    ExecWait 'netsh advfirewall firewall add rule name="MulletaFlix MariaDB" dir=in action=allow program="$INSTDIR\mariadb\bin\mysqld.exe" enable=yes' $0

    ; Create uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "MulletaFlix Server Service" InstallService
${If} $_INSTALLSERVICE_ == "Yes" ; Only run this if we're going to install the service!
    ExecWait '"$INSTDIR\nssm.exe" statuscode MulletaFlixServer' $0
    DetailPrint "MulletaFlix Server service statuscode, $0"
    ${If} $0 == 0
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

    DetailPrint "MulletaFlix Install location: $INSTDIR"
    DetailPrint "MulletaFlix Data folder: $_MULLETAFLIXDATADIR_"

    MessageBox MB_YESNO|MB_ICONINFORMATION "Do you want to keep the MulletaFlix Server data folder? $\r$\nIf unsure choose YES." /SD IDYES IDYES PreserveData IDNO DeleteConfirmation

    DeleteConfirmation:
    MessageBox MB_YESNOCANCEL|MB_ICONEXCLAMATION "Are you sure? Everything in $\r$\n$_MULLETAFLIXDATADIR_ $\r$\nwill be deleted. $\r$\nIf you are sure, press YES." IDYES DeleteData IDNO PreserveData ;IDCANCEL StopNow

    DeleteData:
    ; Try to delete only known data dir folders
    RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\mariadb_data"
    RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\cache"
    RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\config"
    RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\data"
    RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\log"
    RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\metadata"
    RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\plugins"
    RMDir /r /REBOOTOK "$_MULLETAFLIXDATADIR_\root"
    RMDir /REBOOTOK "$_MULLETAFLIXDATADIR_"     ; Delete final dir only if empty

    ;StopNow:
    ;    Abort

    PreserveData:
    ; noop

    ExecWait "TaskKill /IM MulletaFlix.Windows.Tray.exe /F /T"
    ExecWait "TaskKill /IM MulletaFlix.exe /F /T"
    ExecWait "TaskKill /IM mysqld.exe /F /T"
    ExecWait "TaskKill /IM mariadbd.exe /F /T"
    ExecWait '"$INSTDIR\nssm.exe" statuscode MulletaFlixServer' $0
    DetailPrint "MulletaFlix Server service statuscode, $0"
    IntCmp $0 0 NoServiceUninstall ; service doesn't exist, may be run from desktop shortcut

    Sleep 3000 ; Give time for Windows to catchup

    UninstallStopRetry:
    ExecWait '"$INSTDIR\nssm.exe" stop MulletaFlixServer' $0
    ${If} $0 <> 0
        !insertmacro ShowError "Could not stop the MulletaFlix Server service." UninstallStopRetry
    ${EndIf}
    DetailPrint "Stopped MulletaFlix Server service, $0"

    UninstallRemoveRetry:
    ExecWait '"$INSTDIR\nssm.exe" remove MulletaFlixServer confirm' $0
    ${If} $0 <> 0
        !insertmacro ShowError "Could not remove the MulletaFlix Server service." UninstallRemoveRetry
    ${EndIf}
    DetailPrint "Removed MulletaFlix Server service, $0"

    ; Remove MariaDB firewall rule
    ExecWait 'netsh advfirewall firewall delete rule name="MulletaFlix MariaDB"' $0

    Sleep 3000 ; Give time for Windows to catchup

    NoServiceUninstall: ; existing install was present but no service was detected. Remove shortcuts if account is set to none
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
    StrCpy $_EXISTINGSERVICE_ "No"
    StrCpy $_MAKESHORTCUTS_ "No"

    SetShellVarContext current
    StrCpy $_MULLETAFLIXDATADIR_ "$%ProgramData%\MulletaFlix\Server"

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

    ; check if there is a service called MulletaFlix, there should be
    ; hack : nssm statuscode MulletaFlix will return non zero return code in case it exists
    ExecWait '"$INSTDIR\nssm.exe" statuscode MulletaFlixServer' $0
    DetailPrint "MulletaFlix Server service statuscode, $0"
    IntCmp $0 0 NoService ; service doesn't exist, may be run from desktop shortcut

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
            ; This stops the installer from starting if MulletaFlix.exe is open
            StrCpy $3 "MulletaFlix.exe"
            nsProcess::_FindProcess "$3"
            Pop $R3
            ${If} $R3 = 0
                !insertmacro ShowErrorFinal "MulletaFlix is running. Please close it first."
                Abort
            ${EndIf}
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
    ${If} $_FOLDEREXISTS_ == "Yes"
        StrCpy $_MULLETAFLIXDATADIR_ "$LOCALAPPDATA\MulletaFlix\"
    ${EndIf}
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
    ; TODO - Eventually add an option to launch tray app or service instead, and remind/offer to start browser
FunctionEnd
