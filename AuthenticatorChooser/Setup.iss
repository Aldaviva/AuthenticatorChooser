; Inno Setup script for AuthenticatorChooser, compiled in CI.
; ISCC is invoked with /DArch=win-x64|win-arm64, /DSourceExe=<absolute path to signed exe> and /DAppVersion=<version>.

#define AppName "AuthenticatorChooser"
#define AppExeName "AuthenticatorChooser.exe"
#define AppPublisher "Ben Hutchison"

#if Arch == "win-arm64"
  #define ArchId "arm64"
#else
  #define ArchId "x64compatible"
#endif

[Setup]
AppId={{ce8383a4-bdac-4d97-b0a6-8fc582b4c102}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed={#ArchId}
ArchitecturesInstallIn64BitMode={#ArchId}
UninstallDisplayIcon={app}\{#AppExeName}
; Emit the installer into the publish folder so it's uploaded alongside the main binary.
OutputDir=bin\Release\net8.0-windows\{#Arch}\publish
OutputBaseFilename={#AppName}-{#AppVersion}-{#Arch}-Setup
Compression=lzma2
SolidCompression=yes

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExeName}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Start {#AppName} now"; Flags: nowait runascurrentuser

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/im {#AppExeName} /f"; Flags: runhidden; RunOnceId: "StopApp"

; https://github.com/DomGries/InnoDependencyInstaller - downloads and installs the .NET Desktop Runtime 8 if it's missing
#include "CodeDependencies.iss"

[Code]
function InitializeSetup: Boolean;
begin
  Dependency_AddDotNet80Desktop;
  Result := True;
end;
