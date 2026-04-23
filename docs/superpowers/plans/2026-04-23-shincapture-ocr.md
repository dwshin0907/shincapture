# 신캡쳐 OCR (텍스트 추출) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 신캡쳐에 OCR(텍스트 추출) 기능을 두 가지 진입점으로 추가한다 — 편집기 툴바 버튼(하단 패널) + 새로운 "텍스트 캡쳐" 모드(Ctrl+Shift+T, 토스트+즉시 클립보드 복사).

**Architecture:** `Services/OcrService.cs` 정적 클래스에 Windows.Media.Ocr WinRT API를 완전히 격리한다. `System.Drawing.Bitmap` 입력 → `string` 출력이라는 단순 인터페이스만 노출. UI/윈도우 레이어는 이 서비스만 호출하며 WinRT 타입을 직접 다루지 않는다. 외부 패키지 0개, 설치 용량 증가 0 MB.

**Tech Stack:** C# .NET 8, WPF, Windows.Media.Ocr (WinRT), xUnit

**참고 문서:**
- 설계 스펙: `docs/superpowers/specs/2026-04-23-shincapture-ocr-design.md`
- 기존 설계: `docs/superpowers/specs/2026-03-30-shincapture-design.md`

**빌드 명령:** `& "C:\Users\popol\dotnet-sdk2\dotnet.exe" build src\ShinCapture\ShinCapture.csproj -c Release` (SDK 경로가 일반적이지 않으니 주의)
**테스트 명령:** `& "C:\Users\popol\dotnet-sdk2\dotnet.exe" test tests\ShinCapture.Tests\ShinCapture.Tests.csproj -c Release --nologo`

---

## File Structure

**신규 파일:**
- `src/ShinCapture/Services/OcrService.cs` — Windows.Media.Ocr 래퍼 (정적)
- `tests/ShinCapture.Tests/Services/OcrServiceTests.cs` — OCR 서비스 단위/통합 테스트

**수정 파일:**
- `src/ShinCapture/ShinCapture.csproj` — TargetFramework `net8.0-windows` → `net8.0-windows10.0.19041.0`
- `tests/ShinCapture.Tests/ShinCapture.Tests.csproj` — 동일하게 TFM 업데이트
- `src/ShinCapture/Models/CaptureMode.cs` — enum에 `Text` 추가
- `src/ShinCapture/Models/AppSettings.cs` — `HotkeySettings.TextCapture` + 새 `OcrSettings` 클래스
- `src/ShinCapture/Views/SettingsWindow.xaml/.cs` — 단축키 탭에 "텍스트 캡쳐" 행 + OCR 섹션 (언어 드롭다운 + 업스케일 체크박스 + 언어팩 설정 링크)
- `src/ShinCapture/Views/MainWindow.xaml.cs` — TextCapture 핫키 등록, `HandleCaptureResult`에 `CaptureMode.Text` 분기 추가
- `src/ShinCapture/Views/EditorWindow.xaml` — 상태바 위에 OCR 결과 패널 Row 추가
- `src/ShinCapture/Views/EditorWindow.xaml.cs` — 툴바에 🔤 버튼 추가, OCR 실행/패널 표시 로직
- `tests/ShinCapture.Tests/Services/SettingsManagerTests.cs` — 새 필드 라운드트립 테스트 추가

---

## Task 1: TargetFramework 업데이트 + 빌드 검증

**Files:**
- Modify: `src/ShinCapture/ShinCapture.csproj:4`
- Modify: `tests/ShinCapture.Tests/ShinCapture.Tests.csproj:4`

Windows.Media.Ocr는 WinRT API이므로 `net8.0-windows10.0.19041.0` TFM이 필요하다. Windows 10 v2004 (May 2020)의 Windows SDK를 사용한다.

- [ ] **Step 1: 앱 프로젝트 TFM 변경**

`src/ShinCapture/ShinCapture.csproj` 4번 줄:

```xml
<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
```

- [ ] **Step 2: 테스트 프로젝트 TFM 변경**

`tests/ShinCapture.Tests/ShinCapture.Tests.csproj` 4번 줄:

```xml
<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
```

- [ ] **Step 3: 빌드 확인**

명령:
```powershell
& "C:\Users\popol\dotnet-sdk2\dotnet.exe" build src\ShinCapture\ShinCapture.csproj -c Release -v minimal
```
예상: "빌드했습니다. 경고 0개 오류 0개" — 기존 코드가 새 TFM에서 문제없이 빌드되는지 확인

- [ ] **Step 4: 기존 테스트 통과 확인**

명령:
```powershell
& "C:\Users\popol\dotnet-sdk2\dotnet.exe" test tests\ShinCapture.Tests\ShinCapture.Tests.csproj -c Release --nologo
```
예상: 18개 테스트 모두 통과 (Task 1에서 새 테스트는 아직 없음)

- [ ] **Step 5: 커밋**

```bash
git add src/ShinCapture/ShinCapture.csproj tests/ShinCapture.Tests/ShinCapture.Tests.csproj
git commit -m "build: bump TargetFramework to net8.0-windows10.0.19041.0 for WinRT OCR API"
```

---

## Task 2: OcrService 작성 (TDD)

**Files:**
- Create: `src/ShinCapture/Services/OcrService.cs`
- Create: `tests/ShinCapture.Tests/Services/OcrServiceTests.cs`

**설계 참조:** 스펙 섹션 3.1 (공개 API), 섹션 6 (에러 처리)

**핵심 결정:**
- `System.Drawing.Bitmap` → `SoftwareBitmap` 변환은 PNG 메모리 스트림 경유 (`.AsRandomAccessStream()` 확장 사용)
- `OcrEngine.TryCreateFromLanguage` 반환값 null → `InvalidOperationException("OCR 언어팩이 설치되지 않았습니다: {tag}")`
- 작은 이미지 업스케일: width 또는 height < 40px → `HighQualityBicubic` 2배

- [ ] **Step 1: 실패 테스트 작성 — 영어 이미지 OCR**

`tests/ShinCapture.Tests/Services/OcrServiceTests.cs` (신규 파일):

```csharp
using System.Drawing;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class OcrServiceTests
{
    [Fact]
    public async Task ExtractTextAsync_EnglishTextImage_ContainsExpectedWord()
    {
        using var bitmap = CreateTextImage("Hello World", width: 400, height: 80);
        var text = await OcrService.ExtractTextAsync(bitmap, "en-US");
        Assert.Contains("Hello", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractTextAsync_BlankImage_ReturnsEmptyOrWhitespace()
    {
        using var bitmap = new Bitmap(200, 200);
        using (var g = Graphics.FromImage(bitmap))
            g.Clear(Color.White);
        var text = await OcrService.ExtractTextAsync(bitmap, "en-US");
        Assert.True(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void IsLanguageAvailable_English_ReturnsTrue()
    {
        Assert.True(OcrService.IsLanguageAvailable("en-US"));
    }

    [Fact]
    public void IsLanguageAvailable_UnknownLanguage_ReturnsFalse()
    {
        Assert.False(OcrService.IsLanguageAvailable("zz-ZZ"));
    }

    [Fact]
    public void GetAvailableLanguages_ReturnsAtLeastEnglish()
    {
        var langs = OcrService.GetAvailableLanguages();
        Assert.Contains(langs, l => l.StartsWith("en", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractTextAsync_MissingLanguagePack_ThrowsInvalidOperation()
    {
        using var bitmap = new Bitmap(100, 100);
        using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.White);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => OcrService.ExtractTextAsync(bitmap, "zz-ZZ"));
    }

    private static Bitmap CreateTextImage(string text, int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        using var font = new Font("Arial", 32);
        using var brush = new SolidBrush(Color.Black);
        g.DrawString(text, font, brush, 10, 10);
        return bmp;
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

명령:
```powershell
& "C:\Users\popol\dotnet-sdk2\dotnet.exe" test tests\ShinCapture.Tests\ShinCapture.Tests.csproj -c Release --nologo
```
예상: 컴파일 에러 `OcrService` 존재하지 않음. 또는 모든 OCR 테스트 FAIL

- [ ] **Step 3: OcrService 구현**

`src/ShinCapture/Services/OcrService.cs` (신규 파일):

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace ShinCapture.Services;

/// <summary>
/// Windows.Media.Ocr WinRT API를 감싸는 정적 서비스.
/// System.Drawing.Bitmap 입력 → 추출된 텍스트 출력. UI 레이어는 WinRT 타입을 직접 다루지 않음.
/// </summary>
public static class OcrService
{
    /// <summary>지정 언어로 OCR 실행. 작은 이미지는 자동 업스케일(기본 true).</summary>
    public static Task<string> ExtractTextAsync(Bitmap image, string langTag)
        => ExtractTextAsync(image, langTag, upscaleSmall: true);

    /// <summary>지정 언어로 OCR 실행.</summary>
    /// <exception cref="InvalidOperationException">해당 언어팩이 설치되어 있지 않을 때</exception>
    public static async Task<string> ExtractTextAsync(Bitmap image, string langTag, bool upscaleSmall)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        if (string.IsNullOrWhiteSpace(langTag)) throw new ArgumentException("langTag empty", nameof(langTag));

        var language = new Language(langTag);
        var engine = OcrEngine.TryCreateFromLanguage(language)
            ?? throw new InvalidOperationException($"OCR 언어팩이 설치되지 않았습니다: {langTag}");

        Bitmap target = image;
        Bitmap? upscaled = null;
        try
        {
            if (upscaleSmall && (image.Width < 40 || image.Height < 40))
            {
                upscaled = new Bitmap(image.Width * 2, image.Height * 2);
                using (var g = Graphics.FromImage(upscaled))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image, 0, 0, upscaled.Width, upscaled.Height);
                }
                target = upscaled;
            }

            var softwareBitmap = await BitmapToSoftwareBitmapAsync(target);
            var result = await engine.RecognizeAsync(softwareBitmap);
            return result.Text ?? string.Empty;
        }
        finally
        {
            upscaled?.Dispose();
        }
    }

    /// <summary>현재 Windows에 설치된 OCR 사용 가능 언어 목록 (BCP-47 태그).</summary>
    public static IReadOnlyList<string> GetAvailableLanguages()
        => OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).ToList();

    /// <summary>해당 BCP-47 언어 태그로 OCR이 가능한지.</summary>
    public static bool IsLanguageAvailable(string langTag)
    {
        try
        {
            return OcrEngine.IsLanguageSupported(new Language(langTag));
        }
        catch
        {
            return false;
        }
    }

    private static async Task<SoftwareBitmap> BitmapToSoftwareBitmapAsync(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        var decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
        return await decoder.GetSoftwareBitmapAsync();
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

명령:
```powershell
& "C:\Users\popol\dotnet-sdk2\dotnet.exe" test tests\ShinCapture.Tests\ShinCapture.Tests.csproj -c Release --nologo
```
예상: 총 24개 (기존 18 + 신규 6), 실패 0개.

참고: `ExtractTextAsync_EnglishTextImage_ContainsExpectedWord`는 Windows에 en-US OCR 엔진이 있어야 통과. GitHub Actions Windows 러너는 기본 포함.

- [ ] **Step 5: 커밋**

```bash
git add src/ShinCapture/Services/OcrService.cs tests/ShinCapture.Tests/Services/OcrServiceTests.cs
git commit -m "feat: add OcrService wrapping Windows.Media.Ocr WinRT API"
```

---

## Task 3: AppSettings 확장 (핫키 + OCR 설정)

**Files:**
- Modify: `src/ShinCapture/Models/AppSettings.cs`
- Modify: `tests/ShinCapture.Tests/Services/SettingsManagerTests.cs`

**설계 참조:** 스펙 섹션 5.1

- [ ] **Step 1: 실패 테스트 작성 — 새 필드 라운드트립**

`tests/ShinCapture.Tests/Services/SettingsManagerTests.cs` 파일 끝에 (`Dispose` 메서드 위에) 테스트 2개 추가:

```csharp
    [Fact]
    public void Save_ThenLoad_PersistsTextCaptureHotkey()
    {
        var settings = _manager.Load();
        settings.Hotkeys.TextCapture = "Ctrl+Alt+O";
        _manager.Save(settings);
        var loaded = _manager.Load();
        Assert.Equal("Ctrl+Alt+O", loaded.Hotkeys.TextCapture);
    }

    [Fact]
    public void Load_WhenNoFile_OcrDefaults()
    {
        var settings = _manager.Load();
        Assert.Equal("ko", settings.Ocr.Language);
        Assert.True(settings.Ocr.UpscaleSmallImages);
    }

    [Fact]
    public void Save_ThenLoad_PersistsOcrSettings()
    {
        var settings = _manager.Load();
        settings.Ocr.Language = "en-US";
        settings.Ocr.UpscaleSmallImages = false;
        _manager.Save(settings);
        var loaded = _manager.Load();
        Assert.Equal("en-US", loaded.Ocr.Language);
        Assert.False(loaded.Ocr.UpscaleSmallImages);
    }
```

- [ ] **Step 2: 테스트 실패 확인**

명령:
```powershell
& "C:\Users\popol\dotnet-sdk2\dotnet.exe" test tests\ShinCapture.Tests\ShinCapture.Tests.csproj -c Release --nologo --filter "FullyQualifiedName~SettingsManagerTests"
```
예상: 컴파일 에러 — `TextCapture`, `Ocr` 속성 없음

- [ ] **Step 3: AppSettings 수정**

`src/ShinCapture/Models/AppSettings.cs` 파일에서:

1. `HotkeySettings` 클래스 안에 필드 추가 (기존 `OverridePrintScreen` 바로 위, `FixedSizeCapture` 바로 아래):

```csharp
    public string TextCapture { get; set; } = "Ctrl+Shift+T";
```

2. `AppSettings` 클래스에 `Ocr` 프로퍼티 추가 (기존 `Hotkeys` 프로퍼티 바로 아래):

```csharp
    public OcrSettings Ocr { get; set; } = new();
```

3. 파일 끝에 새 클래스 추가 (`RecentCapturesSettings` 아래):

```csharp
public class OcrSettings
{
    public string Language { get; set; } = "ko";
    public bool UpscaleSmallImages { get; set; } = true;
}
```

- [ ] **Step 4: 테스트 통과 확인**

명령:
```powershell
& "C:\Users\popol\dotnet-sdk2\dotnet.exe" test tests\ShinCapture.Tests\ShinCapture.Tests.csproj -c Release --nologo
```
예상: 전체 27개 (기존 18 + OCR 6 + 설정 3) 모두 통과

- [ ] **Step 5: 커밋**

```bash
git add src/ShinCapture/Models/AppSettings.cs tests/ShinCapture.Tests/Services/SettingsManagerTests.cs
git commit -m "feat: add TextCapture hotkey and OcrSettings to AppSettings"
```

---

## Task 4: CaptureMode enum + 텍스트 캡쳐 핫키 & MainWindow 분기

**Files:**
- Modify: `src/ShinCapture/Models/CaptureMode.cs`
- Modify: `src/ShinCapture/Views/MainWindow.xaml.cs`

**설계 참조:** 스펙 섹션 4.2, 5.2, 6

**핵심 결정:**
- `CaptureMode.Text`는 기존 `RegionCaptureMode` (오버레이)를 그대로 재사용. 단 `HandleCaptureResult`에서 분기.
- 토스트는 `_trayIcon.ShowBalloonTip(3000, "신캡쳐", msg, ToolTipIcon.Info)`
- 언어팩 미설치 감지 시 `MessageBox` + Yes/No → Yes면 `Process.Start("ms-settings:regionlanguage")`
- OCR 실행은 비동기이므로 `async void` 대신 `.ContinueWith`로 UI 스레드 복귀

- [ ] **Step 1: CaptureMode enum 확장**

`src/ShinCapture/Models/CaptureMode.cs`:

```csharp
namespace ShinCapture.Models;

public enum CaptureMode
{
    Region, Freeform, Window, Element, Fullscreen, Scroll, FixedSize,
    Text,
}
```

- [ ] **Step 2: MainWindow에 텍스트 캡쳐 핫키 등록**

`src/ShinCapture/Views/MainWindow.xaml.cs` `RegisterHotkeys` 메서드 안, 기존 `FixedSizeCapture` 등록 바로 아래에 추가:

```csharp
        _hotkeyManager.Register(_settings.Hotkeys.TextCapture, () => StartCapture(CaptureMode.Text));
```

- [ ] **Step 3: MainWindow.HandleCaptureResult에 Text 분기 추가**

`src/ShinCapture/Views/MainWindow.xaml.cs` 기존 `HandleCaptureResult` 메서드를 수정. 메서드 맨 앞에서 `CaptureMode.Text`일 때 별도 처리 후 return:

기존:
```csharp
    private void HandleCaptureResult(CaptureResult result)
    {
        // 캡쳐 즉시 클립보드에 복사 (모든 모드 공통)
        System.Windows.Clipboard.SetImage(BitmapHelper.ToBitmapSource(result.Image));
```

변경:
```csharp
    private void HandleCaptureResult(CaptureResult result)
    {
        // 텍스트 캡쳐 모드: OCR → 클립보드(텍스트) → 토스트. 이미지 클립보드/편집기 분기 X.
        if (_lastCaptureMode == CaptureMode.Text)
        {
            RunOcrAndNotify(result.Image);
            return;
        }

        // 캡쳐 즉시 클립보드에 복사 (모든 모드 공통)
        System.Windows.Clipboard.SetImage(BitmapHelper.ToBitmapSource(result.Image));
```

- [ ] **Step 4: _lastCaptureMode 필드 추가 + StartCapture에서 설정**

`MainWindow.xaml.cs`의 필드 선언부 (`private readonly SaveManager _saveManager;` 근처)에 추가:

```csharp
    private CaptureMode _lastCaptureMode = CaptureMode.Region;
```

`StartCapture(CaptureMode mode)` 메서드 맨 첫 줄에 추가:

```csharp
    private void StartCapture(CaptureMode mode)
    {
        _lastCaptureMode = mode;
        // ...기존 코드
```

`StartCapture` 내부의 `ICaptureMode captureMode = mode switch { ... }` 분기에 `CaptureMode.Text` 케이스 추가:

기존:
```csharp
            CaptureMode.FixedSize => new FixedSizeCaptureMode(
                _settings.FixedSizes?.FirstOrDefault()?.Width  ?? 1280,
                _settings.FixedSizes?.FirstOrDefault()?.Height ?? 720),
            _ => new RegionCaptureMode()
```

변경:
```csharp
            CaptureMode.FixedSize => new FixedSizeCaptureMode(
                _settings.FixedSizes?.FirstOrDefault()?.Width  ?? 1280,
                _settings.FixedSizes?.FirstOrDefault()?.Height ?? 720),
            CaptureMode.Text => new RegionCaptureMode(),  // 영역 드래그 재사용, OCR 분기는 HandleCaptureResult
            _ => new RegionCaptureMode()
```

- [ ] **Step 5: RunOcrAndNotify 메서드 추가**

`MainWindow.xaml.cs`에서 `HandleCaptureResult` 메서드 바로 아래에 새 메서드 추가:

```csharp
    private async void RunOcrAndNotify(System.Drawing.Bitmap image)
    {
        // 언어 결정: 설정값 → ko → en-US → 첫 번째 사용 가능
        var langTag = ResolveOcrLanguage(_settings.Ocr.Language);
        if (langTag == null)
        {
            PromptInstallLanguagePack(_settings.Ocr.Language);
            return;
        }

        try
        {
            var text = await Services.OcrService.ExtractTextAsync(
                image, langTag, _settings.Ocr.UpscaleSmallImages);

            if (string.IsNullOrWhiteSpace(text))
            {
                _trayIcon.ShowBalloonTip(3000, "신캡쳐", "텍스트를 찾지 못했습니다", System.Windows.Forms.ToolTipIcon.Info);
                return;
            }

            System.Windows.Clipboard.SetText(text);
            var preview = text.Length > 40 ? text[..40] + "…" : text;
            _trayIcon.ShowBalloonTip(3000, "신캡쳐 — 텍스트 복사됨",
                $"{text.Length}자: {preview}", System.Windows.Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloonTip(4000, "신캡쳐 — OCR 실패",
                ex.Message, System.Windows.Forms.ToolTipIcon.Error);
        }
    }

    private static string? ResolveOcrLanguage(string preferred)
    {
        if (Services.OcrService.IsLanguageAvailable(preferred)) return preferred;
        if (Services.OcrService.IsLanguageAvailable("ko")) return "ko";
        if (Services.OcrService.IsLanguageAvailable("en-US")) return "en-US";
        var list = Services.OcrService.GetAvailableLanguages();
        return list.Count > 0 ? list[0] : null;
    }

    private void PromptInstallLanguagePack(string langTag)
    {
        var result = System.Windows.MessageBox.Show(
            $"OCR 언어팩이 설치되어 있지 않습니다 ({langTag}).\n\n" +
            "Windows 설정에서 언어팩을 설치하시겠습니까?\n" +
            "(예 → Windows 설정 '시간 및 언어 > 언어' 화면 열기)",
            "신캡쳐 — OCR 언어팩 필요",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Information);
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:regionlanguage",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
```

- [ ] **Step 6: 트레이 메뉴에 "텍스트 캡쳐" 항목 추가**

`MainWindow.xaml.cs` `BuildTrayMenu` 메서드에서 기존 "지정사이즈" 항목 아래에 추가:

기존:
```csharp
        menu.Items.Add("⊞ 지정사이즈 캡쳐\tCtrl+Shift+Z", null, (_, _) => StartCapture(CaptureMode.FixedSize));
        menu.Items.Add(new ToolStripSeparator());
```

변경:
```csharp
        menu.Items.Add("⊞ 지정사이즈 캡쳐\tCtrl+Shift+Z", null, (_, _) => StartCapture(CaptureMode.FixedSize));
        menu.Items.Add("🔤 텍스트 캡쳐\tCtrl+Shift+T", null, (_, _) => StartCapture(CaptureMode.Text));
        menu.Items.Add(new ToolStripSeparator());
```

- [ ] **Step 7: 빌드 확인**

명령:
```powershell
& "C:\Users\popol\dotnet-sdk2\dotnet.exe" build src\ShinCapture\ShinCapture.csproj -c Release -v minimal
```
예상: 경고 0개, 오류 0개

- [ ] **Step 8: 수동 테스트 + 커밋**

수동 테스트:
1. 앱 실행 → Ctrl+Shift+T → 영역 드래그 → 토스트 알림 "텍스트 복사됨" 확인
2. 아무 텍스트 에디터에 Ctrl+V → 텍스트 붙여넣어지는지 확인
3. 빈 영역(텍스트 없음) 캡쳐 → "텍스트를 찾지 못했습니다" 토스트

커밋:
```bash
git add src/ShinCapture/Models/CaptureMode.cs src/ShinCapture/Views/MainWindow.xaml.cs
git commit -m "feat: add text capture mode with OCR and toast notification"
```

---

## Task 5: SettingsWindow UI — 텍스트 캡쳐 단축키 + OCR 설정

**Files:**
- Modify: `src/ShinCapture/Views/SettingsWindow.xaml`
- Modify: `src/ShinCapture/Views/SettingsWindow.xaml.cs`

**설계 참조:** 스펙 섹션 7

**핵심 결정:**
- 단축키 탭에 통합 (전용 탭 X)
- 언어 드롭다운은 `OcrService.GetAvailableLanguages()` 반환값으로 동적 채움. 표시 이름은 `CultureInfo.GetCultureInfo(tag).DisplayName` (예: "한국어 (ko)")
- 언어팩 도움말 버튼 → `ms-settings:regionlanguage` 실행

- [ ] **Step 1: XAML — 단축키 탭 Row 추가 (텍스트 캡쳐 행)**

`src/ShinCapture/Views/SettingsWindow.xaml` 단축키 탭 `Grid.RowDefinitions`에서 마지막 `RowDefinition` 추가 (현재 9개인 상태에서 1개 더):

기존:
```xml
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
```

변경 (1개 추가):
```xml
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
```

기존 "지정사이즈" (Row 6)를 수정하고 그 아래 텍스트 캡쳐 행(Row 7), 구분선(Row 8)과 OCR 섹션(Row 9-11)을 추가:

기존:
```xml
                    <TextBlock Grid.Row="6" Grid.Column="0" Text="지정사이즈" VerticalAlignment="Center" Margin="0,0,0,10"/>
                    <TextBox x:Name="TxtHkFixedSize" Grid.Row="6" Grid.Column="1" Padding="4,3" Margin="0,0,0,10"/>

                    <Separator Grid.Row="7" Grid.ColumnSpan="2" Margin="0,4,0,10"/>

                    <CheckBox x:Name="ChkOverridePrintScreen" Grid.Row="8" Grid.ColumnSpan="2"
                              Content="PrintScreen 키를 신캡쳐가 독점 사용"
                              ToolTip="Windows 11의 'PrtSc 키로 화면 캡쳐 열기' 설정을 자동으로 꺼서, PrintScreen 키가 신캡쳐로 전달되도록 합니다. 해제하면 Windows 기본 동작(캡쳐 도구 열기)으로 되돌립니다."/>
```

변경:
```xml
                    <TextBlock Grid.Row="6" Grid.Column="0" Text="지정사이즈" VerticalAlignment="Center" Margin="0,0,0,10"/>
                    <TextBox x:Name="TxtHkFixedSize" Grid.Row="6" Grid.Column="1" Padding="4,3" Margin="0,0,0,10"/>

                    <TextBlock Grid.Row="7" Grid.Column="0" Text="텍스트 캡쳐" VerticalAlignment="Center" Margin="0,0,0,10"/>
                    <TextBox x:Name="TxtHkTextCapture" Grid.Row="7" Grid.Column="1" Padding="4,3" Margin="0,0,0,10"/>

                    <Separator Grid.Row="8" Grid.ColumnSpan="2" Margin="0,4,0,10"/>

                    <CheckBox x:Name="ChkOverridePrintScreen" Grid.Row="9" Grid.ColumnSpan="2"
                              Content="PrintScreen 키를 신캡쳐가 독점 사용" Margin="0,0,0,10"
                              ToolTip="Windows 11의 'PrtSc 키로 화면 캡쳐 열기' 설정을 자동으로 꺼서, PrintScreen 키가 신캡쳐로 전달되도록 합니다. 해제하면 Windows 기본 동작(캡쳐 도구 열기)으로 되돌립니다."/>

                    <StackPanel Grid.Row="10" Grid.ColumnSpan="2" Orientation="Horizontal" Margin="0,0,0,8">
                        <TextBlock Text="OCR 언어" VerticalAlignment="Center" Width="120"/>
                        <ComboBox x:Name="CmbOcrLanguage" Width="200"/>
                        <Button x:Name="BtnOcrLanguageHelp" Content="언어팩 설치" Margin="8,0,0,0"
                                Padding="8,2" Click="OnOcrLanguageHelp"/>
                    </StackPanel>

                    <CheckBox x:Name="ChkOcrUpscale" Grid.Row="11" Grid.ColumnSpan="2"
                              Content="작은 이미지 자동 업스케일 (40px 미만)"
                              ToolTip="작은 텍스트 인식률을 높이기 위해 40px 미만 이미지를 2배로 확대한 뒤 OCR을 실행합니다."/>
```

- [ ] **Step 2: 코드비하인드 — 로드 (LoadSettings)**

`src/ShinCapture/Views/SettingsWindow.xaml.cs` `LoadSettings` 메서드의 단축키 섹션 아래, 지정사이즈 섹션 위에 OCR 로드 로직 추가:

기존:
```csharp
        TxtHkFixedSize.Text = _settings.Hotkeys.FixedSizeCapture;
        ChkOverridePrintScreen.IsChecked = _settings.Hotkeys.OverridePrintScreen;
```

변경:
```csharp
        TxtHkFixedSize.Text = _settings.Hotkeys.FixedSizeCapture;
        TxtHkTextCapture.Text = _settings.Hotkeys.TextCapture;
        ChkOverridePrintScreen.IsChecked = _settings.Hotkeys.OverridePrintScreen;

        // OCR 언어 드롭다운 채우기
        PopulateOcrLanguages(_settings.Ocr.Language);
        ChkOcrUpscale.IsChecked = _settings.Ocr.UpscaleSmallImages;
```

`LoadSettings` 아래에 helper 추가:

```csharp
    private void PopulateOcrLanguages(string selectedTag)
    {
        CmbOcrLanguage.Items.Clear();
        var langs = ShinCapture.Services.OcrService.GetAvailableLanguages();
        foreach (var tag in langs)
        {
            string display;
            try { display = $"{System.Globalization.CultureInfo.GetCultureInfo(tag).DisplayName} ({tag})"; }
            catch { display = tag; }
            var item = new System.Windows.Controls.ComboBoxItem { Content = display, Tag = tag };
            CmbOcrLanguage.Items.Add(item);
            if (string.Equals(tag, selectedTag, StringComparison.OrdinalIgnoreCase))
                CmbOcrLanguage.SelectedItem = item;
        }
        if (CmbOcrLanguage.SelectedItem == null && CmbOcrLanguage.Items.Count > 0)
            CmbOcrLanguage.SelectedIndex = 0;
        if (CmbOcrLanguage.Items.Count == 0)
        {
            CmbOcrLanguage.Items.Add(new System.Windows.Controls.ComboBoxItem
            {
                Content = "(설치된 OCR 언어 없음)", Tag = ""
            });
            CmbOcrLanguage.SelectedIndex = 0;
        }
    }
```

- [ ] **Step 3: 코드비하인드 — 저장 (ApplyToSettings)**

`ApplyToSettings` 메서드에서 기존 `FixedSizeCapture` 저장 바로 아래에 추가:

기존:
```csharp
        _settings.Hotkeys.FixedSizeCapture = TxtHkFixedSize.Text;
        _settings.Hotkeys.OverridePrintScreen = ChkOverridePrintScreen.IsChecked == true;
```

변경:
```csharp
        _settings.Hotkeys.FixedSizeCapture = TxtHkFixedSize.Text;
        _settings.Hotkeys.TextCapture = TxtHkTextCapture.Text;
        _settings.Hotkeys.OverridePrintScreen = ChkOverridePrintScreen.IsChecked == true;

        var ocrLangTag = (CmbOcrLanguage.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString();
        if (!string.IsNullOrEmpty(ocrLangTag))
            _settings.Ocr.Language = ocrLangTag;
        _settings.Ocr.UpscaleSmallImages = ChkOcrUpscale.IsChecked == true;
```

- [ ] **Step 4: 언어팩 도움말 버튼 핸들러**

`SettingsWindow.xaml.cs` 끝에 (닫는 `}` 전) 추가:

```csharp
    private void OnOcrLanguageHelp(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:regionlanguage",
                UseShellExecute = true
            });
        }
        catch { }
    }
```

- [ ] **Step 5: 빌드 확인**

명령:
```powershell
& "C:\Users\popol\dotnet-sdk2\dotnet.exe" build src\ShinCapture\ShinCapture.csproj -c Release -v minimal
```
예상: 경고 0개, 오류 0개

- [ ] **Step 6: 수동 테스트 + 커밋**

수동 테스트:
1. 환경설정 > 단축키 탭 → "텍스트 캡쳐" 행 표시되는지
2. "OCR 언어" 드롭다운에 한국어/영어 등 설치된 언어팩 보이는지
3. 다른 언어로 변경 → 저장 → 다시 열어서 유지되는지
4. "언어팩 설치" 버튼 → Windows 설정 열리는지

커밋:
```bash
git add src/ShinCapture/Views/SettingsWindow.xaml src/ShinCapture/Views/SettingsWindow.xaml.cs
git commit -m "feat: add OCR language/upscale settings and text capture hotkey to UI"
```

---

## Task 6: EditorWindow — 툴바 OCR 버튼 + 하단 패널

**Files:**
- Modify: `src/ShinCapture/Views/EditorWindow.xaml`
- Modify: `src/ShinCapture/Views/EditorWindow.xaml.cs`

**설계 참조:** 스펙 섹션 4.1

**핵심 결정:**
- 패널은 기존 Grid의 상태바 Row 위에 새 Row 삽입 (Grid 구조 변경)
- 버튼은 기존 `BuildToolbar()` `tools` 배열에 추가 → 별도 핸들러에서 OCR 실행 분기
- OCR 실행 대상은 `_sourceImage` (BitmapSource) → `System.Drawing.Bitmap`로 변환 필요 (역변환 헬퍼 필요)

- [ ] **Step 1: XAML — 상태바 위에 OCR 패널 Row 추가**

`src/ShinCapture/Views/EditorWindow.xaml` 루트 Grid의 `RowDefinitions`를 수정해서 Row 4개 → 5개로 만든다:

기존 Row 3 (상태바)이 Row 4가 되고, 새 Row 3이 OCR 패널:

기존:
```xml
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
```

변경:
```xml
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
```

상태바 Border의 `Grid.Row="3"`를 `Grid.Row="4"`로 변경:

기존:
```xml
        <!-- Status Bar -->
        <Border Grid.Row="3" Background="{DynamicResource BackgroundSecondaryBrush}"
```

변경:
```xml
        <!-- Status Bar -->
        <Border Grid.Row="4" Background="{DynamicResource BackgroundSecondaryBrush}"
```

그리고 상태바 Border 바로 위에 OCR 패널 Border 추가 (Grid 닫는 태그 `</Grid>` 바로 위, Row 2 Grid 닫힌 후):

```xml
        <!-- OCR 결과 패널 (기본 숨김) -->
        <Border Grid.Row="3" x:Name="OcrPanel" Visibility="Collapsed"
                Background="{DynamicResource BackgroundSecondaryBrush}"
                BorderBrush="{DynamicResource BorderBrush}" BorderThickness="0,1,0,0"
                Padding="10,8">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>
                <DockPanel Grid.Row="0" Margin="0,0,0,6">
                    <TextBlock x:Name="OcrPanelTitle" Text="추출된 텍스트"
                               FontWeight="SemiBold" VerticalAlignment="Center"/>
                    <Button x:Name="OcrCloseBtn" Content="×" DockPanel.Dock="Right"
                            Padding="8,0" Width="28" Height="22" FontSize="16"
                            HorizontalAlignment="Right" Click="OnOcrClose"/>
                </DockPanel>
                <TextBox Grid.Row="1" x:Name="OcrTextBox" MinHeight="60" MaxHeight="160"
                         AcceptsReturn="True" TextWrapping="Wrap"
                         VerticalScrollBarVisibility="Auto"
                         FontFamily="Consolas"/>
                <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right"
                            Margin="0,6,0,0">
                    <Button x:Name="OcrSelectAllBtn" Content="모두 선택" Padding="10,3"
                            Margin="0,0,6,0" Click="OnOcrSelectAll"/>
                    <Button x:Name="OcrCopyBtn" Content="복사" Padding="14,3"
                            Style="{DynamicResource AccentButton}" Click="OnOcrCopy"/>
                </StackPanel>
            </Grid>
        </Border>
```

- [ ] **Step 2: 툴바에 OCR 버튼 추가**

`src/ShinCapture/Views/EditorWindow.xaml.cs` `BuildToolbar()` 메서드 내 `tools` 배열에 "🔤" 툴 추가 (색상추출 바로 다음):

기존:
```csharp
            ("색상추출","⌖", "insert", ""),    // ⌖
            ("크롭",    "✂", "edit",   "C"),   // ✂
```

변경:
```csharp
            ("색상추출","⌖", "insert", ""),    // ⌖
            ("OCR",     "\U0001F524", "insert", ""), // 🔤 텍스트 추출
            ("크롭",    "✂", "edit",   "C"),   // ✂
```

현재 foreach 루프는 `SelectTool(name)`을 호출하므로, OCR은 별도 처리가 필요. foreach 안에 조건 분기 추가:

기존:
```csharp
            var tip = string.IsNullOrEmpty(sc) ? name : $"{name} ({sc})";
            var btn = CreateToolButton(icon, tip);
            btn.Click += (_, _) => SelectTool(name);
            ToolbarPanel.Children.Add(btn);
            _toolButtons[name] = btn;
            lastGroup = group;
```

변경:
```csharp
            var tip = string.IsNullOrEmpty(sc) ? name : $"{name} ({sc})";
            var btn = CreateToolButton(icon, tip);
            if (name == "OCR")
            {
                btn.ToolTip = "텍스트 추출 (OCR)";
                btn.Click += (_, _) => RunEditorOcr();
            }
            else
            {
                btn.Click += (_, _) => SelectTool(name);
            }
            ToolbarPanel.Children.Add(btn);
            _toolButtons[name] = btn;
            lastGroup = group;
```

- [ ] **Step 3: EditorWindow에 RunEditorOcr 및 패널 핸들러 추가**

파일 끝의 클래스 닫는 `}` 바로 위에 메서드 추가:

```csharp
    private async void RunEditorOcr()
    {
        if (_sourceImage == null)
        {
            SetStatus("OCR: 이미지가 없습니다");
            return;
        }

        SetStatus("OCR 실행 중...");
        OcrPanel.Visibility = Visibility.Visible;
        OcrPanelTitle.Text = "추출된 텍스트 (추출 중…)";
        OcrTextBox.Text = "";

        try
        {
            var settings = _settings;  // EditorWindow는 기존에 _settings 필드 있음
            var langTag = ResolveOcrLanguageForEditor(settings.Ocr.Language);
            if (langTag == null)
            {
                OcrPanelTitle.Text = "OCR 언어팩이 필요합니다";
                OcrTextBox.Text =
                    $"설정된 언어({settings.Ocr.Language})의 OCR 언어팩이 설치되어 있지 않습니다.\n" +
                    "Windows 설정 > 시간 및 언어 > 언어에서 언어팩을 설치한 뒤 다시 시도해주세요.";
                SetStatus("OCR 언어팩 없음");
                return;
            }

            using var bmp = BitmapSourceToBitmap(_sourceImage);
            var text = await ShinCapture.Services.OcrService.ExtractTextAsync(
                bmp, langTag, settings.Ocr.UpscaleSmallImages);

            if (string.IsNullOrWhiteSpace(text))
            {
                OcrPanelTitle.Text = "추출된 텍스트 없음";
                OcrTextBox.Text = "";
                SetStatus("OCR: 텍스트를 찾지 못했습니다");
                return;
            }

            OcrTextBox.Text = text;
            var tagNote = string.Equals(langTag, settings.Ocr.Language, StringComparison.OrdinalIgnoreCase)
                ? "" : $" — {langTag} 폴백";
            OcrPanelTitle.Text = $"추출된 텍스트 ({text.Length}자{tagNote})";
            SetStatus($"OCR 완료 ({text.Length}자)");
        }
        catch (Exception ex)
        {
            OcrPanelTitle.Text = "OCR 실패";
            OcrTextBox.Text = ex.Message;
            SetStatus("OCR 실패");
        }
    }

    private static string? ResolveOcrLanguageForEditor(string preferred)
    {
        if (ShinCapture.Services.OcrService.IsLanguageAvailable(preferred)) return preferred;
        if (ShinCapture.Services.OcrService.IsLanguageAvailable("ko")) return "ko";
        if (ShinCapture.Services.OcrService.IsLanguageAvailable("en-US")) return "en-US";
        var list = ShinCapture.Services.OcrService.GetAvailableLanguages();
        return list.Count > 0 ? list[0] : null;
    }

    private static System.Drawing.Bitmap BitmapSourceToBitmap(System.Windows.Media.Imaging.BitmapSource source)
    {
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
        using var ms = new System.IO.MemoryStream();
        encoder.Save(ms);
        ms.Position = 0;
        return new System.Drawing.Bitmap(ms);
    }

    private void OnOcrClose(object sender, RoutedEventArgs e)
    {
        OcrPanel.Visibility = Visibility.Collapsed;
    }

    private void OnOcrSelectAll(object sender, RoutedEventArgs e)
    {
        OcrTextBox.SelectAll();
        OcrTextBox.Focus();
    }

    private void OnOcrCopy(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(OcrTextBox.Text)) return;
        System.Windows.Clipboard.SetText(OcrTextBox.Text);
        SetStatus($"텍스트 복사됨 ({OcrTextBox.Text.Length}자)");
    }
```

- [ ] **Step 4: SetStatus helper 확인 (이미 존재하는지)**

`SetStatus` 메서드가 EditorWindow에 이미 있는지 확인 (대부분의 WPF 편집기 창에 상태 업데이트 유틸이 존재). 없으면 단순 추가:

```csharp
    private void SetStatus(string text)
    {
        if (StatusText != null) StatusText.Text = text;
    }
```

확인 명령:
```bash
grep -n "SetStatus\|StatusText.Text" "src/ShinCapture/Views/EditorWindow.xaml.cs" | head
```

이미 있으면 건너뛰고, 없으면 위 메서드 추가.

- [ ] **Step 5: 빌드 확인**

명령:
```powershell
& "C:\Users\popol\dotnet-sdk2\dotnet.exe" build src\ShinCapture\ShinCapture.csproj -c Release -v minimal
```
예상: 경고 0개, 오류 0개

- [ ] **Step 6: 수동 테스트 + 커밋**

수동 테스트:
1. 앱 실행 → 아무 영역 캡쳐 (PrtSc) → 편집기 열림
2. 툴바에서 🔤 클릭 → 하단에 "추출된 텍스트" 패널 펼쳐짐
3. 텍스트 표시 + 복사 버튼 → 다른 앱 붙여넣기 확인
4. [×] 버튼 → 패널 닫힘
5. 텍스트 없는 단색 이미지에서 OCR → "추출된 텍스트 없음" 표시

커밋:
```bash
git add src/ShinCapture/Views/EditorWindow.xaml src/ShinCapture/Views/EditorWindow.xaml.cs
git commit -m "feat: add OCR button and result panel to editor"
```

---

## Task 7: 최종 검증 — 전체 빌드, 테스트, 수동 시나리오

**Files:** 코드 변경 없음 — 검증만

- [ ] **Step 1: 전체 빌드**

명령:
```powershell
& "C:\Users\popol\dotnet-sdk2\dotnet.exe" build src\ShinCapture\ShinCapture.csproj -c Release -v minimal
```
예상: 경고 0개, 오류 0개

- [ ] **Step 2: 전체 테스트**

명령:
```powershell
& "C:\Users\popol\dotnet-sdk2\dotnet.exe" test tests\ShinCapture.Tests\ShinCapture.Tests.csproj -c Release --nologo
```
예상: 27개 모두 통과

- [ ] **Step 3: End-to-End 수동 시나리오**

1. 앱 실행
2. **시나리오 A — 텍스트 캡쳐 모드**:
   - Ctrl+Shift+T → 웹페이지의 한국어 문단 드래그 → 토스트 "텍스트 N자 복사됨"
   - 메모장에 Ctrl+V → 한국어 텍스트 들어감
3. **시나리오 B — 편집기 OCR**:
   - PrtSc → 영역 캡쳐 → 편집기 열림
   - 툴바 🔤 클릭 → 하단 패널 펼쳐짐 → 텍스트 표시
   - [복사] 버튼 → 다른 앱 붙여넣기 확인
4. **시나리오 C — 설정**:
   - 환경설정 > 단축키 탭 → 텍스트 캡쳐 단축키 "Ctrl+Alt+O"로 변경 → 저장
   - Ctrl+Alt+O 눌러서 동작 확인
   - OCR 언어를 "en-US"로 변경 → 영어 텍스트 캡쳐 → 결과 확인
5. **시나리오 D — 에러 케이스**:
   - 빈 영역 캡쳐 → "텍스트를 찾지 못했습니다"
   - 설정에서 `zz-ZZ` 같은 가짜 언어 강제로 저장 (편집기 JSON 직접 편집) → 실행 → 폴백 또는 에러 다이얼로그

- [ ] **Step 4: README/문서 갱신 (해당사항만)**

`README.md` 또는 프로젝트 설명 파일에 "텍스트 추출 (OCR)" 기능 한 줄 추가. 기존 README가 없거나 기능 목록이 없으면 건너뛴다.

확인:
```bash
ls README.md 2>/dev/null
grep -l "캡쳐 모드\|기능 목록" docs/ 2>/dev/null
```

- [ ] **Step 5: 최종 커밋 (문서 변경 있을 때만)**

```bash
git add README.md  # 또는 수정한 파일
git commit -m "docs: mention OCR (text extraction) feature"
```

변경 없으면 건너뜀.

---

## 완료 체크리스트

- [ ] 모든 유닛 테스트 통과 (27개)
- [ ] 빌드 경고 0개
- [ ] 편집기 툴바 🔤 버튼 동작 확인 (수동)
- [ ] Ctrl+Shift+T 텍스트 캡쳐 동작 확인 (수동)
- [ ] 설정에서 단축키/언어/업스케일 저장/로드 동작
- [ ] 언어팩 없을 때 안내 다이얼로그 동작
- [ ] 텍스트 없는 이미지 처리 동작
- [ ] 모든 커밋 완료

---

## 범위 외 (Out of Scope)

스펙 섹션 10에서 명시한 향후 확장:
- 다국어 동시 인식 (현재는 단일 언어)
- 주석 포함한 OCR 옵션
- 플로팅 편집 창 분리
- 손글씨 인식용 대체 엔진
