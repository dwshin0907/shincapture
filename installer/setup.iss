[Setup]
AppName=신캡쳐
AppVersion=1.0.0
AppPublisher=ShinCapture
DefaultDirName={autopf}\ShinCapture
DefaultGroupName=신캡쳐
OutputDir=output
OutputBaseFilename=ShinCapture_Setup_1.0.0
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\ShinCapture.exe
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\신캡쳐"; Filename: "{app}\ShinCapture.exe"
Name: "{autodesktop}\신캡쳐"; Filename: "{app}\ShinCapture.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; GroupDescription: "추가 설정"
Name: "startup"; Description: "Windows 시작 시 자동 실행"; GroupDescription: "추가 설정"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ShinCapture"; ValueData: """{app}\ShinCapture.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\ShinCapture.exe"; Description: "신캡쳐 실행"; Flags: nowait postinstall skipifsilent
