; Instalador do vibeVocal Studio (Inno Setup 6.3+)
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\installer\app"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#define AppName "vibeVocal Studio"
#define AppExe "vibeVocal Studio.exe"
#define VbCableUrl "https://vb-audio.com/Cable/"

[Setup]
AppId={{6F2B8E4A-3C1D-4B7E-9A55-2D8C1F0E7B93}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=viniciuscanutx
AppPublisherURL=https://github.com/viniciuscanutx/vibeVocal-Studio
AppSupportURL=https://github.com/viniciuscanutx/vibeVocal-Studio/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=vibeVocal-Studio-Setup-{#AppVersion}
SetupIconFile=..\Assets\Microphone.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=force

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\README.md"; DestDir: "{app}"; DestName: "LEIA-ME.md"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Remove a entrada de "Abrir com o Windows" criada pelo app
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#AppName}"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{#VbCableUrl}"; Description: "Abrir o site do VB-CABLE para baixar (necessário para o áudio)"; Flags: postinstall shellexec skipifsilent; Check: not IsVbCableInstalled
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: postinstall nowait skipifsilent

[Code]
var
  VbCableChecked: Boolean;
  VbCableFound: Boolean;

// VB-CABLE é instalado à parte: a licença não permite embuti-lo em outro instalador
function IsVbCableInstalled: Boolean;
var
  ResultCode: Integer;
begin
  if not VbCableChecked then
  begin
    VbCableFound := Exec('powershell.exe',
      '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "if (Get-CimInstance Win32_SoundDevice | Where-Object { $_.Name -like ''*VB-Audio Virtual Cable*'' }) { exit 0 } else { exit 1 }"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
    VbCableChecked := True;
  end;
  Result := VbCableFound;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := MemoDirInfo + NewLine + NewLine;
  if MemoGroupInfo <> '' then
    Result := Result + MemoGroupInfo + NewLine + NewLine;
  if MemoTasksInfo <> '' then
    Result := Result + MemoTasksInfo + NewLine + NewLine;
  if IsVbCableInstalled then
    Result := Result + 'VB-CABLE:' + NewLine + Space + 'Encontrado neste PC.'
  else
    Result := Result + 'VB-CABLE:' + NewLine + Space + 'Não encontrado. Ao final, o site oficial será aberto para você baixar e instalar.' + NewLine + Space + 'Depois de instalar o VB-CABLE, reinicie o Windows.';
end;
