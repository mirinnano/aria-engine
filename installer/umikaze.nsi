Unicode true
RequestExecutionLevel user
SetCompressor /SOLID lzma
!include MUI2.nsh

!ifndef APPDIR
  !error "APPDIR is required"
!endif
!ifndef OUTFILE
  !error "OUTFILE is required"
!endif
!ifndef VERSION
  !define VERSION "dev"
!endif
!ifndef ICONFILE
  !define ICONFILE "..\src\AriaEngine\assets\branding\umikaze.ico"
!endif

!define PRODUCT_NAME "umikaze"
!define PUBLISHER "Ponkotusoft"
!define REGKEY "Software\${PUBLISHER}\${PRODUCT_NAME}"
!define RUN_ARGS "--run-mode release"

!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "${PRODUCT_NAME} セットアップ"
!define MUI_WELCOMEPAGE_TEXT "${PRODUCT_NAME} をこのPCにインストールするよ。$\r$\n$\r$\n続行するには [次へ] を押してね。"
!define MUI_DIRECTORYPAGE_TEXT_TOP "インストール先フォルダーを選んでね。通常はこのままで大丈夫だよ。"
!define MUI_INSTFILESPAGE_COLORS "FFFFFF 202020"
!define MUI_FINISHPAGE_TITLE "インストール完了"
!define MUI_FINISHPAGE_TEXT "${PRODUCT_NAME} のインストールが完了したよ。"
!define MUI_FINISHPAGE_RUN "$INSTDIR\AriaEngine.exe"
!define MUI_FINISHPAGE_RUN_PARAMETERS "${RUN_ARGS}"
!define MUI_FINISHPAGE_RUN_TEXT "${PRODUCT_NAME} を起動する"
!define MUI_FINISHPAGE_LINK "インストール先を開く"
!define MUI_FINISHPAGE_LINK_LOCATION "$INSTDIR"
!define MUI_UNFINISHPAGE_TITLE "${PRODUCT_NAME} アンインストール"
!define MUI_UNFINISHPAGE_TEXT "${PRODUCT_NAME} をこのPCから削除するよ。"

Name "${PRODUCT_NAME} ${VERSION}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\${PUBLISHER}\${PRODUCT_NAME}"
InstallDirRegKey HKCU "${REGKEY}" "InstallDir"
Icon "${ICONFILE}"
UninstallIcon "${ICONFILE}"

VIProductVersion "1.0.0.0"
VIAddVersionKey /LANG=1041 "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey /LANG=1041 "CompanyName" "${PUBLISHER}"
VIAddVersionKey /LANG=1041 "FileDescription" "${PRODUCT_NAME} セットアップ"
VIAddVersionKey /LANG=1041 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1041 "LegalCopyright" "Copyright ${PUBLISHER}"
VIAddVersionKey /LANG=1041 "ProductVersion" "${VERSION}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Japanese"

Section "Install"
  DetailPrint "インストール先: $INSTDIR"
  SetOutPath "$INSTDIR"
  DetailPrint "ゲームファイルをコピーしているよ..."
  File /r "${APPDIR}\*.*"

  WriteRegStr HKCU "${REGKEY}" "InstallDir" "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  DetailPrint "ショートカットを作成しているよ..."
  CreateDirectory "$SMPROGRAMS\${PUBLISHER}"
  CreateShortCut "$SMPROGRAMS\${PUBLISHER}\${PRODUCT_NAME}.lnk" "$INSTDIR\AriaEngine.exe" "${RUN_ARGS}" "$INSTDIR\AriaEngine.exe" 0 SW_SHOWNORMAL "" "${PRODUCT_NAME}" "$INSTDIR"
  CreateShortCut "$SMPROGRAMS\${PUBLISHER}\アンインストール ${PRODUCT_NAME}.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortCut "$DESKTOP\${PRODUCT_NAME}.lnk" "$INSTDIR\AriaEngine.exe" "${RUN_ARGS}" "$INSTDIR\AriaEngine.exe" 0 SW_SHOWNORMAL "" "${PRODUCT_NAME}" "$INSTDIR"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\${PRODUCT_NAME}.lnk"
  Delete "$SMPROGRAMS\${PUBLISHER}\${PRODUCT_NAME}.lnk"
  Delete "$SMPROGRAMS\${PUBLISHER}\アンインストール ${PRODUCT_NAME}.lnk"
  RMDir "$SMPROGRAMS\${PUBLISHER}"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "${REGKEY}"
SectionEnd

