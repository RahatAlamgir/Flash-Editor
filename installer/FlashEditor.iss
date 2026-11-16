; =============================================================================
; Flash Editor installer (Inno Setup 6)
;
; One script, two editions:
;   * Standard  - the app only. AI tools ask to download their model on first use.
;   * AI        - the app plus both AI models, ready to use offline.
; Build with build-installers.ps1 (it publishes the app and passes /DWithAI=0 or 1).
;
; EVERYTHING lives in the install folder: the program, its libraries, the .NET runtime
; (self-contained build) and the AI models (in "models\"). Nothing is written to AppData
; or other folders. Uninstalling removes the whole folder, including downloaded models.
; =============================================================================

#ifndef WithAI
  #define WithAI "0"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName      "Flash Editor"
#define AppExe       "FlashEditor.exe"
#define BuildDir     "build\app"
#define ModelsDir    "build\models"

#if WithAI == "1"
  #define Edition    "AI"
  #define OutName    "FlashEditor-Setup-" + AppVersion + "-AI"
#else
  #define Edition    "Standard"
  #define OutName    "FlashEditor-Setup-" + AppVersion
#endif

[Setup]
; Same AppId for both editions: installing one over the other upgrades in place.
AppId={{6B1E9F4C-2E7A-4C1B-9A55-3F0D7C2B8E11}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion} ({#Edition})
AppPublisher=Flash Editor
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\src\FlashEditor.App\Assets\FlashEditor.ico
UninstallDisplayName={#AppName} ({#Edition})
OutputDir=output
OutputBaseFilename={#OutName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
#ifdef TestInstall
; Test builds only (no admin prompt): install into a folder of your choice with /DIR=...
PrivilegesRequired=lowest
#else
PrivilegesRequired=admin
#endif
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Dirs]
; The app downloads AI models into this folder (Standard edition), so normal users may write here.
Name: "{app}\models"; Permissions: users-modify

[Files]
; The program, its libraries and the .NET runtime (self-contained), all in {app}.
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
#if WithAI == "1"
; AI models, ready to use (background removal 170 MB, upscaling 67 MB).
Source: "{#ModelsDir}\*"; DestDir: "{app}\models"; Flags: ignoreversion recursesubdirs createallsubdirs
#endif

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Models downloaded later by the app are not in the installer's file list: remove them too,
; so uninstalling leaves nothing behind.
Type: filesandordirs; Name: "{app}\models"
Type: dirifempty; Name: "{app}"
