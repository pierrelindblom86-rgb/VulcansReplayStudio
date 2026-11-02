#define AppName "Vulcans Replay Studio"
#define AppVersion "1.1.0"
#define AppPublisher "Vulcans Replay Studio"
#define AppExeName "VulcansReplayStudio2.App.exe"
#define SourceDir "..\dist\VulcansReplayStudio2_1.1.0"

[Setup]
AppId={{6C2F1B24-8D1E-4C7A-9B62-3E5F0A7D4C12}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion=1.1.0.0
VersionInfoProductName={#AppName}
VersionInfoDescription=Vulcans Replay Studio installer
DefaultDirName={localappdata}\Programs\Vulcans Replay Studio
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=VulcansReplayStudio-Setup-1.1.0
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon


[Code]
function HasDesktop9(const Key: String): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(HKLM, Key, Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Copy(Names[I], 1, 2) = '9.' then Result := True;
end;

function IsDotNetDesktopInstalled(): Boolean;
begin
  Result := HasDesktop9('SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App') or
            HasDesktop9('SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App');
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNetDesktopInstalled() then
  begin
    if MsgBox('Vulcans Replay Studio requires the .NET 9 Desktop Runtime (x64).' + #13#10 +
              'It was not found. Install it from https://dotnet.microsoft.com/download/dotnet/9.0 and run this installer again.' + #13#10 +
              'Continue anyway?', mbConfirmation, MB_YESNO) = IDNO then
      Result := False;
  end;
end;
