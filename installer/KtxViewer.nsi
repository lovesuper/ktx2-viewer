; KTX2 Viewer - NSIS installer
; Builds a Windows installer that deploys the app and associates
; .ktx / .ktx2 files with it so Windows opens them via this application.
;
; Build with build-installer.ps1, or manually:
;   makensis /DVERSION=1.0.0 /DPUBLISH_DIR=..\publish /DOUTFILE=..\dist\KtxViewerSetup.exe KtxViewer.nsi

Unicode true

;--------------------------------
; Configurable defines (override via makensis /D...)

!ifndef VERSION
  !define VERSION "1.0.0"
!endif

!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\publish"
!endif

!ifndef OUTFILE
  !define OUTFILE "..\dist\KtxViewerSetup-${VERSION}.exe"
!endif

!ifndef APP_ICON
  !define APP_ICON "..\KtxViewer.UI\KtxViewer.UI\icon.ico"
!endif

;--------------------------------
; Constants

!define APP_NAME        "KTX2 Viewer"
!define APP_PUBLISHER   "KTX2 Viewer"
!define APP_EXE         "KtxViewer.UI.exe"
!define PROGID          "KtxViewer.Texture"
!define PROGID_DESC     "KTX Texture"
!define UNINST_KEY      "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"

; SHCNE_ASSOCCHANGED | SHCNF_IDLIST -> ask the shell to refresh file associations/icons
!define SHCNE_ASSOCCHANGED 0x08000000
!define SHCNF_IDLIST       0x0000

;--------------------------------
; General

Name "${APP_NAME}"
OutFile "${OUTFILE}"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "Software\${APP_NAME}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName"     "${APP_NAME}"
VIAddVersionKey "FileDescription" "${APP_NAME} Setup"
VIAddVersionKey "FileVersion"     "${VERSION}"
VIAddVersionKey "ProductVersion"  "${VERSION}"
VIAddVersionKey "CompanyName"     "${APP_PUBLISHER}"
VIAddVersionKey "LegalCopyright"  "${APP_PUBLISHER}"

;--------------------------------
; Modern UI

!include "MUI2.nsh"

!define MUI_ICON   "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

; Launch via a custom function so the app starts NON-elevated (see LaunchAppAsUser).
!define MUI_FINISHPAGE_RUN ""
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${APP_NAME}"
!define MUI_FINISHPAGE_RUN_FUNCTION "LaunchAppAsUser"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

;--------------------------------
; Launch the app with the user's normal token after install.
; The installer runs elevated (admin), so a direct Exec would start the app elevated
; too - and Windows (UIPI) then blocks drag-and-drop from a normal Explorer window.
; Launching through Explorer makes the app inherit the logged-in user's (non-elevated)
; token, so drag-and-drop works.

Function LaunchAppAsUser
  Exec '"$WINDIR\explorer.exe" "$INSTDIR\${APP_EXE}"'
FunctionEnd

;--------------------------------
; Sections

Section "${APP_NAME} (required)" SecCore
  SectionIn RO

  ; Stop a running instance so files are not locked during upgrade.
  nsExec::Exec 'taskkill /IM "${APP_EXE}" /F'

  SetOutPath "$INSTDIR"
  File /r /x "*.pdb" "${PUBLISH_DIR}\*.*"

  ; Remember install location.
  WriteRegStr HKLM "Software\${APP_NAME}" "InstallDir" "$INSTDIR"

  ; Start menu shortcut.
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut  "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"

  ; Uninstall information (Add/Remove Programs).
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayName"     "${APP_NAME}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayVersion"  "${VERSION}"
  WriteRegStr   HKLM "${UNINST_KEY}" "Publisher"       "${APP_PUBLISHER}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayIcon"     "$INSTDIR\${APP_EXE},0"
  WriteRegStr   HKLM "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${UNINST_KEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr   HKLM "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1

  WriteUninstaller "$INSTDIR\uninstall.exe"
SectionEnd

Section "Desktop shortcut" SecDesktop
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
SectionEnd

Section "Associate .ktx / .ktx2 files" SecAssoc
  ; Application registration (used by the "Open with" list and friendly name).
  WriteRegStr HKLM "Software\Classes\Applications\${APP_EXE}" "FriendlyAppName" "${APP_NAME}"
  WriteRegStr HKLM "Software\Classes\Applications\${APP_EXE}\shell\open\command" "" '"$INSTDIR\${APP_EXE}" "%1"'
  WriteRegStr HKLM "Software\Classes\Applications\${APP_EXE}\SupportedTypes" ".ktx" ""
  WriteRegStr HKLM "Software\Classes\Applications\${APP_EXE}\SupportedTypes" ".ktx2" ""

  ; ProgID describing how to open KTX textures.
  WriteRegStr HKLM "Software\Classes\${PROGID}" "" "${PROGID_DESC}"
  WriteRegStr HKLM "Software\Classes\${PROGID}\DefaultIcon" "" "$INSTDIR\${APP_EXE},0"
  WriteRegStr HKLM "Software\Classes\${PROGID}\shell\open\command" "" '"$INSTDIR\${APP_EXE}" "%1"'

  ; Point both extensions at the ProgID and advertise it under OpenWithProgids.
  WriteRegStr HKLM "Software\Classes\.ktx"  "" "${PROGID}"
  WriteRegStr HKLM "Software\Classes\.ktx\OpenWithProgids" "${PROGID}" ""
  WriteRegStr HKLM "Software\Classes\.ktx2" "" "${PROGID}"
  WriteRegStr HKLM "Software\Classes\.ktx2\OpenWithProgids" "${PROGID}" ""

  ; Tell the shell associations changed so new icons/handlers take effect immediately.
  System::Call 'shell32::SHChangeNotify(i ${SHCNE_ASSOCCHANGED}, i ${SHCNF_IDLIST}, i 0, i 0)'
SectionEnd

;--------------------------------
; Section descriptions

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore}    "The ${APP_NAME} application files (required)."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "Create a shortcut on the desktop."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecAssoc}   "Open .ktx and .ktx2 files with ${APP_NAME} by double-clicking them."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

;--------------------------------
; Uninstaller

Section "Uninstall"
  nsExec::Exec 'taskkill /IM "${APP_EXE}" /F'

  ; Remove file association only if it still points at us.
  ReadRegStr $0 HKLM "Software\Classes\.ktx" ""
  StrCmp $0 "${PROGID}" 0 +2
    DeleteRegValue HKLM "Software\Classes\.ktx" ""
  ReadRegStr $0 HKLM "Software\Classes\.ktx2" ""
  StrCmp $0 "${PROGID}" 0 +2
    DeleteRegValue HKLM "Software\Classes\.ktx2" ""

  DeleteRegValue HKLM "Software\Classes\.ktx\OpenWithProgids" "${PROGID}"
  DeleteRegValue HKLM "Software\Classes\.ktx2\OpenWithProgids" "${PROGID}"
  DeleteRegKey HKLM "Software\Classes\${PROGID}"
  DeleteRegKey HKLM "Software\Classes\Applications\${APP_EXE}"

  System::Call 'shell32::SHChangeNotify(i ${SHCNE_ASSOCCHANGED}, i ${SHCNF_IDLIST}, i 0, i 0)'

  ; Remove shortcuts.
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  RMDir  "$SMPROGRAMS\${APP_NAME}"

  ; Remove installed files.
  Delete "$INSTDIR\uninstall.exe"
  RMDir /r "$INSTDIR"

  DeleteRegKey HKLM "${UNINST_KEY}"
  DeleteRegKey HKLM "Software\${APP_NAME}"
SectionEnd
