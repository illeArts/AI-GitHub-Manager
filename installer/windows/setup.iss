; ============================================================
;  AI GitHub Manager — Inno Setup Installer Script
;  Requires: Inno Setup 6  (https://jrsoftware.org/isinfo.php)
;  Build via:  build-installer-win.bat
; ============================================================

#define AppName      "AI GitHub Manager"
#define AppVersion   "1.1.0"
#define AppPublisher "illeArts"
#define AppURL       "https://github.com/illeArts"
#define AppExeName   "AI.GitHubManager.App.exe"
#define PublishDir   "..\..\publish\win-x64"
#define DistDir      "..\..\dist"

[Setup]
AppId={{6B82E11B-C6F4-4D49-9230-B3ED691A8161}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir={#DistDir}
OutputBaseFilename=AI_GitHub_Manager_Setup_{#AppVersion}_win-x64
SetupIconFile=..\..\src\AI.GitHubManager.App\Assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName}

[Languages]
Name: "german";  MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Single self-contained executable — all .NET runtime and assets embedded
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";   Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; \
    Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove settings file left by the app
Type: filesandordirs; Name: "{userappdata}\AI.GitHubManager"
