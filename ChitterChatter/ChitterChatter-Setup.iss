; ChitterChatter Inno Setup Script
; Creates a Windows installer for the ChitterChatter voice chat application
;
; This script is compiled on Linux using Wine + Inno Setup
; Output: ChitterChatter-Setup-{version}.exe

#define MyAppName "ChitterChatter"
#define MyAppVersion GetEnv('CHITTERCHATTER_VERSION')
#if MyAppVersion == ""
#define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "Infoforum"
#define MyAppURL "https://longmanrd.net"
#define MyAppExeName "ChitterChatter.exe"

; Source path is set via environment variable from deploy.sh
#define SourcePath GetEnv('CHITTERCHATTER_SOURCE')
#if SourcePath == ""
#define SourcePath "publish"
#endif

[Setup]
; Application identity - unique GUID for this application
AppId={{8F7E9D5A-3C2B-4A1F-9E8D-7C6B5A4D3E2F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/infoforum/downloads/

; Installation directory - Program Files\Infoforum\ChitterChatter
DefaultDirName={autopf}\Infoforum\{#MyAppName}
DefaultGroupName=Infoforum\{#MyAppName}
DisableProgramGroupPage=yes

; Output settings
OutputDir=Output
OutputBaseFilename=ChitterChatter-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Privileges - admin required for Program Files
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Uninstall settings
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

; Minimum Windows version (Windows 10 1809 or later for WebView2)
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main application files
Source: "{#SourcePath}\ChitterChatter.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourcePath}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourcePath}\*.json"; DestDir: "{app}"; Flags: ignoreversion

; WebView2 files if present
Source: "{#SourcePath}\WebView2Loader.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#SourcePath}\*.xml"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; Include any subdirectories (runtimes, wwwroot, etc.)
Source: "{#SourcePath}\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#SourcePath}\wwwroot\*"; DestDir: "{app}\wwwroot"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; NOTE: Don't include .pdb files in production releases
; Source: "{#SourcePath}\*.pdb"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
; Option to launch after install
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Check if WebView2 Runtime is installed
function IsWebView2RuntimeInstalled(): Boolean;
begin
  Result := RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or
            RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or
            RegKeyExists(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}');
end;

// Show warning if WebView2 not installed
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpReady then
  begin
    if not IsWebView2RuntimeInstalled() then
    begin
      MsgBox('WebView2 Runtime is not installed on this system.' + #13#10 + #13#10 +
             'ChitterChatter requires the Microsoft Edge WebView2 Runtime.' + #13#10 +
             'Please install it from:' + #13#10 +
             'https://developer.microsoft.com/en-us/microsoft-edge/webview2/' + #13#10 + #13#10 +
             'The application may not function correctly without it.',
             mbInformation, MB_OK);
    end;
  end;
end;

// Clean up user data on uninstall (optional - ask user)
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  UserDataPath: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    UserDataPath := ExpandConstant('{localappdata}\ChitterChatter');
    if DirExists(UserDataPath) then
    begin
      if MsgBox('Do you want to remove ChitterChatter user data and settings?',
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(UserDataPath, True, True, True);
      end;
    end;
  end;
end;
