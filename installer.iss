#define MyAppName "Apex Toolbox"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Apex Toolbox Contributors"
#define MyAppExeName "ApexToolbox.exe"

[Setup]
AppId={{7F9C2A44-1E5B-4C8D-9A77-2B6E4F1D8C31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Apex Toolbox
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=dist\installer
OutputBaseFilename=ApexToolbox-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
DisableProgramGroupPage=yes

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"; Flags: unchecked

[Files]
Source: "dist\lite\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function HasDotnetDesktopRuntime: Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), FindRec) then
  begin
    Result := True;
    FindClose(FindRec);
  end;
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := HasDotnetDesktopRuntime;
  if not Result then
  begin
    if MsgBox('Apex Toolbox 需要 Microsoft .NET 10 桌面运行时（Desktop Runtime）。' + #13#10#13#10 +
              '本机尚未安装或版本低于 10，是否打开微软官方下载页面？' + #13#10 +
              '安装完成后请重新运行本安装程序。',
              mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;
