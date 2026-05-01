; ============================================================
;  신캡쳐 (ShinCapture) Installer
;  Inno Setup 6.x Script
; ============================================================

#define MyAppName "신캡쳐"
#define MyAppNameEn "ShinCapture"
; 버전은 로컬 기본값. CI 빌드에서는 ISCC /DMyAppVersion=x.y.z 로 덮어씀
#ifndef MyAppVersion
  #define MyAppVersion "1.2.1"
#endif
#define MyAppPublisher "ShinCapture"
#define MyAppURL "https://shincapture.com"
#define MyAppExeName "ShinCapture.exe"

[Setup]
AppId={{8F2B3C4D-5E6F-7A8B-9C0D-1E2F3A4B5C6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppNameEn}
DefaultGroupName={#MyAppName}
; 출력
OutputDir=..\dist
OutputBaseFilename=ShinCapture_Setup_v{#MyAppVersion}
; 압축
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
; 비주얼
WizardStyle=modern
WizardSizePercent=110
SetupIconFile=..\src\ShinCapture\Assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardImageFile=wizard_sidebar.bmp
WizardSmallImageFile=wizard_header.bmp
; 권한
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; 기타
DisableWelcomePage=no
DisableProgramGroupPage=yes
ShowLanguageDialog=no
CloseApplications=yes

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Messages]
korean.WelcomeLabel1=%n신캡쳐 v{#MyAppVersion}
korean.WelcomeLabel2=기존 캡쳐 프로그램의 깔끔한 대안, 신캡쳐를 설치합니다.%n%n%n다른 캡쳐 프로그램과 다른 점:%n%n  ✓  완전 무료 — 광고, 팝업, 회원가입 없음%n  ✓  7가지 캡쳐 모드 — 영역, 창, 스크롤, 전체화면 등%n  ✓  14가지 편집 도구 — 펜, 화살표, 모자이크, 번호 등%n  ✓  실시간 편집 — 색상·폰트 변경 즉시 반영%n  ✓  캡쳐 기록 관리 — 세션 내 50개 보관, 일괄 저장%n  ✓  가벼운 단일 파일 — .NET 런타임 내장, 즉시 실행%n  ✓  글로벌 단축키 — 어떤 앱 위에서든 즉시 캡쳐%n%n계속하려면 [다음]을 클릭하세요.

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; GroupDescription: "추가 설정:"
Name: "startup"; Description: "Windows 시작 시 자동 실행"; GroupDescription: "추가 설정:"
Name: "quicklaunchicon"; Description: "작업 표시줄에 고정"; GroupDescription: "추가 설정:"; Flags: unchecked

[Files]
Source: "..\publish_new\ShinCapture.exe"; DestDir: "{app}"; Flags: ignoreversion
; 아이콘 (제거 프로그램용)
Source: "..\src\ShinCapture\Assets\icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; 시작 메뉴
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"
Name: "{group}\{#MyAppName} 제거"; Filename: "{uninstallexe}"
; 바탕화면
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Registry]
; 자동 실행
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#MyAppNameEn}"; \
  ValueData: """{app}\{#MyAppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; \
  Description: "{#MyAppName} 실행"; \
  Flags: nowait postinstall skipifsilent

[UninstallRun]
; 언인스톨 시 실행 중인 프로세스 강제 종료
Filename: "taskkill"; Parameters: "/F /IM ShinCapture.exe"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
; 앱 폴더 전체 정리
Type: files; Name: "{app}\*"
Type: dirifempty; Name: "{app}\config"
Type: dirifempty; Name: "{app}\logs"
Type: dirifempty; Name: "{app}"

[Code]
// ── 설치 완료 페이지 커스텀 ──
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
  begin
    WizardForm.FinishedHeadingLabel.Caption := '신캡쳐 설치 완료!';
    WizardForm.FinishedLabel.Caption :=
      '이제 신캡쳐를 사용할 준비가 되었습니다.' + #13#10 + #13#10 +
      '빠른 시작 가이드:' + #13#10 +
      '  PrintScreen — 영역 캡쳐' + #13#10 +
      '  Ctrl+Shift+W — 창 캡쳐' + #13#10 +
      '  Ctrl+Shift+A — 전체화면 캡쳐' + #13#10 +
      '  Ctrl+Shift+S — 스크롤 캡쳐' + #13#10 + #13#10 +
      '캡쳐 후 편집기에서 펜, 화살표, 도형, 텍스트,' + #13#10 +
      '모자이크, 번호 등 14가지 도구로 즉시 편집하세요.' + #13#10 + #13#10 +
      '시스템 트레이에 상주하며 언제든 사용 가능합니다.';
  end;
end;

// ── 이전 버전 자동 제거 (묻지 않고 자동) ──
function TryUninstall(RootKey: Integer): Boolean;
var
  UninstallKey: String;
  UninstallString: String;
  ResultCode: Integer;
begin
  Result := False;
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F2B3C4D-5E6F-7A8B-9C0D-1E2F3A4B5C6D}_is1';
  if RegQueryStringValue(RootKey, UninstallKey, 'UninstallString', UninstallString) then
  begin
    // 실행 중인 프로세스 종료
    Exec('taskkill', '/F /IM ShinCapture.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    // 자동 제거 (사일런트)
    Exec(RemoveQuotes(UninstallString), '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Result := True;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not TryUninstall(HKCU) then
    TryUninstall(HKLM);
end;
