# 설치 교체 및 GitHub 업데이트 알림 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 재설치가 실행 중인 트레이 앱을 종료하고, 새 GitHub Release를 사용자가 인지하게 한다.

**Architecture:** UI와 HTTP/버전 판단을 `GitHubReleaseUpdateService`로 분리한다. `MainWindow`는 로드 완료 뒤 결과를 풍선 알림으로 표현하고, 설치 스크립트는 앱 프로세스 종료를 선행한다.

**Tech Stack:** .NET 8, WPF, Windows Forms `NotifyIcon`, `HttpClient`, `System.Text.Json`, Inno Setup, xUnit.

---

### Task 1: GitHub 릴리즈 판단을 테스트 가능하게 분리

**Files:**
- Create: `tests/ShinCapture.Tests/Services/GitHubReleaseUpdateServiceTests.cs`
- Create: `src/ShinCapture/Services/GitHubReleaseUpdateService.cs`

- [ ] **Step 1: 새 정식 버전 판단 테스트를 작성한다.**

```csharp
[Theory]
[InlineData("v1.3.8", "1.3.7", true)]
[InlineData("v1.3.7", "1.3.7", false)]
[InlineData("v1.3.6", "1.3.7", false)]
public void TryCreateUpdate_OnlyReturnsVersionsNewerThanInstalled(
    string tag, string installed, bool expected)
{
    bool actual = GitHubReleaseUpdateService.TryCreateUpdate(
        tag, "https://example.test/release", Version.Parse(installed), out _);

    Assert.Equal(expected, actual);
}
```

- [ ] **Step 2: 테스트가 새 서비스 부재로 실패함을 확인한다.**

Run: `dotnet test tests/ShinCapture.Tests --filter GitHubReleaseUpdateServiceTests`

- [ ] **Step 3: `tag_name`과 `html_url`만 역직렬화하고 `v` 접두사 제거·버전 비교를 하는 최소 서비스를 작성한다.**

- [ ] **Step 4: 테스트가 통과함을 확인한다.**

Run: `dotnet test tests/ShinCapture.Tests --filter GitHubReleaseUpdateServiceTests`

### Task 2: 트레이 알림 연결

**Files:**
- Modify: `src/ShinCapture/Views/MainWindow.xaml.cs`

- [ ] **Step 1: `MainWindow.Loaded` 뒤에서 비동기 최신 Release 확인을 시작하고, 6시간 `DispatcherTimer` 재확인을 등록한다.**
- [ ] **Step 2: 새 버전 결과일 때 `NotifyIcon.ShowBalloonTip`으로 버전과 클릭 안내를 표시한다.**
- [ ] **Step 3: 풍선 클릭 이벤트에서 결과의 `html_url`을 기본 브라우저로 연다.**
- [ ] **Step 4: 종료 이후에는 알림이나 브라우저 실행을 하지 않도록 가드한다.**

### Task 3: 재설치 시 실행 중인 앱 종료 보장

**Files:**
- Modify: `installer/setup.iss`

- [ ] **Step 1: `StopRunningShinCapture` Pascal 함수를 작성해 `taskkill /F /T /IM ShinCapture.exe`를 완료까지 기다린다.**
- [ ] **Step 2: `InitializeSetup` 첫 줄에서 호출해 기존 설치 키가 없는 포터블 실행도 종료한다.**
- [ ] **Step 3: 기존 `TryUninstall`의 중복 종료 호출은 제거해 한 번만 실행하게 한다.**

### Task 4: 전체 검증과 패키지

**Files:**
- 없음 (검증)

- [ ] **Step 1: 전체 테스트를 실행한다.**

Run: `dotnet test ShinCapture.sln --configuration Release --no-restore --verbosity minimal`

- [ ] **Step 2: 단일 실행 파일을 publish한다.**

Run: `dotnet publish src/ShinCapture/ShinCapture.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish`

- [ ] **Step 3: Inno Setup으로 설치 파일을 컴파일한다.**

Run: `& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\setup.iss`
