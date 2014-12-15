; NSIS installer script for Tiled2Unity
; --------------- Headers --------------
!include "MUI2.nsh"

; fixit - don't forget to set Tiled2Unity_Exe

; --------------- General --------------
CRCCheck force
XPStyle on
SetCompressor /FINAL /SOLID lzma

!define V $%T2U_VERSION%                      	; Program version

!define P "Tiled2Unity"                       	; Program name
!define P_NORM "tiled2unity"                  	; Program name (normalized)
!define ROOT_DIR ".."                       	; Program root directory

!define BUILD_DIR $%T2U_Bin%          			; Build dir
!define SYSTEM_DIR "C:\windows\system32"
!define ADD_REMOVE "Software\Microsoft\Windows\CurrentVersion\Uninstall\Tiled2Unity"
!define PRODUCT_REG_KEY "Tiled2Unity Utility"

InstallDir "$PROGRAMFILES\${P}"               	; Default installation directory
Name "${P}"                                   	; Name displayed on installer
OutFile "${P_NORM}-${V}-win32-setup.exe" 		; Resulting installer filename
BrandingText /TRIMLEFT "${P_NORM}-${V}"
RequestExecutionLevel admin

; -------------------------------------
!define MUI_ABORTWARNING

;-------------- Install Pages -------------
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE ${ROOT_DIR}\src\License.txt
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
    ; These indented statements modify settings for MUI_PAGE_FINISH
    !define MUI_FINISHPAGE_NOAUTOCLOSE
    !define MUI_FINISHPAGE_RUN "$INSTDIR\${P_NORM}.exe"
    !define MUI_FINISHPAGE_RUN_CHECKED
    !define MUI_FINISHPAGE_RUN_TEXT "Launch ${P}"
    !define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
    !define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\ReadMe.txt"
!insertmacro MUI_PAGE_FINISH

;-------------- Uninstall Pages -------------
!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
	; These indented statements modify settings for MUI_UNPAGE_FINISH
	!define MUI_UNFINISHPAGE_NOAUTOCLOSE
!insertmacro MUI_UNPAGE_FINISH

;--------------- Languages ---------------
!insertmacro MUI_LANGUAGE "English"

; ------------- Installer Functions ---------------
Function checkAlreadyInstalled
	; check for already installed instance
	ClearErrors
	ReadRegStr $R0 HKLM "SOFTWARE\${PRODUCT_REG_KEY}" "Version"
	StrCmp $R0 "" 0 +2
	Return
	MessageBox MB_YESNO|MB_ICONQUESTION "${P} version $R0 seems \
	to be installed on your system.$\nWould you like to \
	uninstall that version first?" IDYES UnInstall
	Return
	UnInstall:
        ClearErrors
        ReadRegStr $R0 HKLM "${ADD_REMOVE}" "UninstallString"
		DetailPrint "Uninstalling previously installed version"
        ExecWait '$R0 _?=$INSTDIR'
		IfErrors OnError 0
		Return
	OnError:
		MessageBox MB_OK|MB_ICONSTOP "Error while uninstalling \
		previously installed version. Please uninstall it manually \
		and start the installer again."
		Quit
FunctionEnd

;-------------- Installer -------------------------
Section "" ; No components page, name is not important
Call checkAlreadyInstalled

SetOutPath $INSTDIR ; Set output path to the installation directory.
WriteUninstaller $INSTDIR\uninstall.exe ; Location of the uninstaller

; Copy the exe and dependencies
File ReadMe.txt
File ${BUILD_DIR}\${P_NORM}.exe
File ${BUILD_DIR}\${P_NORM}.exe.config
File ${BUILD_DIR}\Ookii.Dialogs.Modified.xml
File ${BUILD_DIR}\Ookii.Dialogs.Modified.dll
File ${BUILD_DIR}\Interop.Shell32.dll
File /oname=Tiled2Unity.unitypackage ${ROOT_DIR}\build\Tiled2Unity.${V}.unitypackage

SetOutPath $INSTDIR\nl
File /r ${BUILD_DIR}\nl\*.dll

SetOutPath $INSTDIR\License
File /oname=License.Tiled2Unity.txt ${ROOT_DIR}\src\License.txt
File /oname=License.Clipper.txt ${ROOT_DIR}\src\ThirdParty\Clipper\License.txt
File /oname=License.NDesk.txt ${ROOT_DIR}\src\ThirdParty\NDesk\License.txt
File /oname=License.Ookii.txt ${ROOT_DIR}\..\Ookii.Dialogs.Modified\license.txt
File /oname=License.Blarget2.txt ${ROOT_DIR}\TestData\license.txt

SetOutPath $INSTDIR\Examples
File /r ${ROOT_DIR}\TestData\*.png
File /r ${ROOT_DIR}\TestData\*.tmx
File /r ${ROOT_DIR}\TestData\*.tsx
File /r ${ROOT_DIR}\TestData\*.txt

; Shortcuts 
CreateDirectory "$SMPROGRAMS\${P}"
CreateShortCut  "$SMPROGRAMS\${P}\${P}.lnk" "$INSTDIR\${P_NORM}.exe"
;CreateShortCut  "$SMPROGRAMS\${P}\uninstall.lnk" "$INSTDIR\uninstall.exe"

; Add version number to Registry
WriteRegStr HKLM "Software\${PRODUCT_REG_KEY}" "Version" "${V}"

; Add uninstall information to "Add/Remove Programs"
WriteRegStr HKLM ${ADD_REMOVE} "DisplayName" "Tiled2Unity Utility"
;WriteRegStr HKLM ${ADD_REMOVE} "DisplayIcon" "$INSTDIR\${P_NORM}-icon.ico"
WriteRegStr HKLM ${ADD_REMOVE} "UninstallString" "$\"$INSTDIR\uninstall.exe$\""
WriteRegStr HKLM ${ADD_REMOVE} "QuietUninstallString" "$\"$INSTDIR\uninstall.exe$\" /S"
WriteRegStr HKLM ${ADD_REMOVE} "Version" "${V}"
SectionEnd
;------------ Uninstaller -------------
Section "uninstall"

Delete $INSTDIR\ReadMe.txt
Delete $INSTDIR\${P_NORM}.exe
Delete $INSTDIR\${P_NORM}.exe.config
Delete $INSTDIR\Ookii.Dialogs.Modified.xml
Delete $INSTDIR\Ookii.Dialogs.Modified.dll
Delete $INSTDIR\Interop.Shell32.dll
Delete $INSTDIR\Tiled2Unity.unitypackage

RMDir /r $INSTDIR\nl
RMDir /r $INSTDIR\License
RMDir /r $INSTDIR\Examples

Delete $INSTDIR\uninstall.exe
RMDir $INSTDIR

; Removing shortcuts
Delete "$SMPROGRAMS\${P}\${P}.lnk"
;Delete "$SMPROGRAMS\${P}\uninstall.lnk"
RMDir  "$SMPROGRAMS\${P}"

; Remove Procut Registry Entries
DeleteRegKey HKLM "Software\${PRODUCT_REG_KEY}"

; Remove entry from "Add/Remove Programs"
DeleteRegKey HKLM ${ADD_REMOVE}
SectionEnd