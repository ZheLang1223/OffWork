#define AppName "下班了"
#define AppVersion "1.0.0-beta.3"
#define AppExeName "下班了.exe"

[Setup]
AppId={{A62E8D6B-53B4-48E8-A9F3-5C67BB775E6B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\dist
OutputBaseFilename=下班了-Setup
SetupIconFile=app.ico
PrivilegesRequired=lowest
MinVersion=10.0
DisableProgramGroupPage=yes
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："

[Files]
Source: "bin\Release\net48\下班了.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net48\下班了.exe.config"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
  begin
    if not WizardSilent then
    begin
      if MsgBox('此程序需要 .NET Framework 4.8。大多数 Windows 10、11 已自带。' + #13#10 +
        '当前电脑未检测到，是否打开微软下载页面？安装后请重新运行本安装程序。',
        mbInformation, MB_YESNO) = IDYES then
        ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48',
          '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end
    else
      Log('Installation requires .NET Framework 4.8 or later.');
  end;
end;
