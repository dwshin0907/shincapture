# 신캡쳐 OCR (텍스트 추출) — 설계 문서

> 작성일: 2026-04-23
> 버전: v1.1 기능 추가
> 상태: 승인 대기

---

## 1. 개요

### 1.1 목적
캡쳐한 이미지에서 텍스트를 추출(OCR)해서 클립보드로 복사하거나 편집기에서 바로 사용할 수 있게 한다.

### 1.2 사용 시나리오
- **스크린 텍스트 빠른 복사**: "이 화면의 문단을 빠르게 복사하고 싶다" — 단축키 → 영역 드래그 → 클립보드
- **캡쳐 후 텍스트 뽑기**: "방금 캡쳐한 이미지에서 글자만 가져오고 싶다" — 편집기에서 버튼 클릭

### 1.3 비목표 (YAGNI)
- 손글씨 인식
- PDF/여러 페이지 OCR
- 검색 가능 PDF 생성
- 표/레이아웃 보존 출력

### 1.4 비기능 요구사항
- 설치 용량 증가 0 MB (Windows 내장 API 사용)
- OCR 1회 실행 2초 이내 (해상도 FHD 기준)
- 오프라인 작동
- 외부 패키지 0개

---

## 2. 기술 선택

### 2.1 OCR 엔진: Windows.Media.Ocr
- Windows 10+ 에 내장된 WinRT API
- 한국어/영어/일본어 등 언어별 엔진 (Windows 설정 > 시간 및 언어 > 언어 > 언어팩 설치)
- 한국어 팩은 대부분 한국 사용자 PC에 이미 설치되어 있음
- 외부 DLL/모델 불필요

### 2.2 TargetFramework 변경
현재: `net8.0-windows`
변경: `net8.0-windows10.0.19041.0` (Windows 10 v2004 / May 2020 Update SDK)
이유: `Windows.Media.Ocr` 접근에 Windows SDK 필요

호환성 영향: 사실상 없음. 현재도 Windows 10/11 타깃이며 v2004은 2020년 릴리즈로 2026년 기준 5년 이상 경과.

---

## 3. 아키텍처

### 3.1 새 컴포넌트

**`Services/OcrService.cs`** (정적 클래스, 신규)
- 단일 책임: `System.Drawing.Bitmap` → 텍스트
- `Windows.Media.Ocr` WinRT API를 .NET에서 사용하기 위해 Bitmap → `SoftwareBitmap` 변환 포함
- 모든 Windows.Media.Ocr 호출을 여기서 격리 → 테스트와 에러 처리 일원화

**공개 API:**
```csharp
public static class OcrService
{
    // 지정 언어로 OCR 실행. 실패 시 예외.
    public static Task<string> ExtractTextAsync(Bitmap image, string langTag);

    // 이미지 자동 업스케일 옵션 포함
    public static Task<string> ExtractTextAsync(Bitmap image, string langTag, bool upscaleSmall);

    // 현재 Windows에 설치된 OCR 사용 가능 언어 목록 (BCP-47 태그)
    public static IReadOnlyList<string> GetAvailableLanguages();

    // 해당 언어로 OCR 가능한지
    public static bool IsLanguageAvailable(string langTag);
}
```

### 3.2 수정되는 컴포넌트

| 파일 | 변경 내용 |
|---|---|
| `Capture/CaptureMode.cs` | enum에 `Text` 추가 |
| `Models/AppSettings.cs` | `HotkeySettings.TextCapture`(기본 "Ctrl+Shift+T"), 새 `OcrSettings` 클래스 |
| `Views/MainWindow.xaml.cs` | `RegisterHotkeys()`에서 `TextCapture` 핫키 등록, `HandleCaptureResult`에서 `CaptureMode.Text` 분기 (OCR → 클립보드 → 토스트) |
| `Views/EditorWindow.xaml` | 툴바에 `🔤` 버튼 + 하단에 OCR 패널 Row 추가 |
| `Views/EditorWindow.xaml.cs` | `BuildToolbar()`에 OCR 버튼, 패널 표시/숨김 핸들러, OCR 실행 |
| `Views/SettingsWindow.xaml/.cs` | 단축키 탭에 "텍스트 캡쳐" 행, 새 "텍스트(OCR)" 탭 또는 단축키 탭 하단 섹션 |
| `ShinCapture.csproj` | TargetFramework 업데이트 |

---

## 4. 사용 흐름

### 4.1 흐름 A: 편집기 툴바 버튼

```
[편집기 열림 + 이미지 로드됨]
   ↓ 사용자 "🔤" 버튼 클릭 (툴바의 색상추출 옆 정도)
[비동기 OcrService.ExtractTextAsync 실행]
   ↓ 상태바에 "OCR 실행 중..." 표시
[결과 반환]
   ↓
[편집기 하단 OCR 패널 펼쳐짐 (Row 추가)]
 ┌──────────────────────────────────────┐
 │ 추출된 텍스트 (N자)              [×] │
 │ ┌──────────────────────────────────┐ │
 │ │ <편집 가능 TextBox, 3~8줄>       │ │
 │ │ 추출 결과                         │ │
 │ └──────────────────────────────────┘ │
 │           [모두 선택]  [복사]       │
 └──────────────────────────────────────┘
```

- 패널은 기본 숨김 (`Visibility=Collapsed`), OCR 성공 시 `Visible`
- 패널은 `Grid.Row=3` (기존 상태바) 위에 새 Row 추가
- `[×]` 또는 패널 바깥 클릭으로 접음 (상태는 세션 내에서 유지)
- `[복사]` 클릭 → `Clipboard.SetText(textBox.Text)` → 상태바 "텍스트 복사됨"
- OCR 대상: 편집기의 원본 이미지 (`_sourceImage`), 사용자가 그린 주석은 제외

### 4.2 흐름 B: 텍스트 캡쳐 모드

```
[사용자 Ctrl+Shift+T 누름]
   ↓ MainWindow.StartCapture(CaptureMode.Text)
[기존 RegionCaptureMode 재사용 → 오버레이 표시]
   ↓ 사용자 영역 드래그 후 마우스 뗌
[HandleCaptureResult에서 CaptureMode.Text 분기]
   ↓
[OcrService.ExtractTextAsync 실행]
   ↓
[성공]
  → Clipboard.SetText(result)
  → _trayIcon.ShowBalloonTip(3000, "신캡쳐", "텍스트 N자 복사됨", Info)
  → 편집기 열지 않음

[텍스트 없음]
  → ShowBalloonTip "텍스트를 찾지 못했습니다"

[실패]
  → ShowBalloonTip "OCR 실패: <원인>"
```

- **편집기 미실행**: 텍스트 캡쳐 모드는 이미지 저장/편집이 목적이 아니므로 편집기를 띄우지 않음
- **이미지 보관**: 캡쳐된 비트맵은 `HistoryPanel`에도 추가하지 않음 (텍스트만 목적)

---

## 5. 데이터 모델

### 5.1 AppSettings 변경

```csharp
public class HotkeySettings
{
    // ...기존 필드
    public string TextCapture { get; set; } = "Ctrl+Shift+T";
}

public class OcrSettings
{
    public string Language { get; set; } = "ko";         // BCP-47 언어 태그
    public bool UpscaleSmallImages { get; set; } = true;
}

public class AppSettings
{
    // ...기존 필드
    public OcrSettings Ocr { get; set; } = new();
}
```

### 5.2 CaptureMode enum

```csharp
public enum CaptureMode
{
    Region, Freeform, Window, Element, Fullscreen, Scroll, FixedSize,
    Text,  // 신규
}
```

---

## 6. 에러 처리

| 상황 | 감지 | 동작 |
|---|---|---|
| 언어팩 미설치 | `OcrEngine.TryCreateFromLanguage()` → null | 다이얼로그: "한국어 OCR을 위한 언어팩이 필요합니다. Windows 설정에서 '한국어 언어팩'을 설치해주세요." + `[Windows 설정 열기]` 버튼 → `ms-settings:regionlanguage` |
| OCR 성공했으나 텍스트 없음 | 결과 문자열 `string.IsNullOrWhiteSpace` | 편집기 흐름: 패널에 "텍스트를 찾지 못했습니다" 메시지 표시<br>캡쳐모드 흐름: 토스트 "텍스트를 찾지 못했습니다" |
| 이미지 크기 너무 작음 | width 또는 height < 40px | `OcrSettings.UpscaleSmallImages=true`면 2배 `System.Drawing` 리샘플 후 재시도. false면 원본으로 실행 |
| API 예외 (WinRT) | try/catch | 로그 + 사용자에게 일반 에러 토스트/다이얼로그 "OCR 실패: {ex.Message}" |
| 언어 설정값이 현재 사용 불가 | `IsLanguageAvailable(settings.Ocr.Language) == false` | 폴백 순서: 설정값 → "ko" → "en-US" → `GetAvailableLanguages()[0]`. 사용자에게 알림은 패널/토스트 본문에 "({사용 언어}) 로 실행됨" 부가 표시 |

---

## 7. 설정 UI

### 7.1 단축키 탭에 추가
기존 "지정사이즈" 행 다음에 "텍스트 캡쳐" 행 추가:
```
텍스트 캡쳐    [Ctrl+Shift+T  ]
```

### 7.2 단축키 탭 하단 또는 새 "텍스트(OCR)" 탭
```
┌────────────────────────────────────────┐
│ OCR 언어                                │
│ [한국어 (ko) ▼]                         │
│ (Windows에 설치된 언어팩만 표시됨)      │
│                                         │
│ ☑ 작은 이미지 자동 업스케일             │
│                                         │
│ ℹ 언어팩이 목록에 없나요?              │
│   [Windows 언어 설정 열기]              │
└────────────────────────────────────────┘
```

신설 탭보다는 **단축키 탭에 포함**이 낫다 — OCR 관련 설정이 2개뿐이라 전용 탭은 과함.

---

## 8. 테스트 전략

### 8.1 단위/통합 테스트 (`OcrServiceTests.cs`)
- 영어 샘플 PNG (테스트 리소스) → `ExtractTextAsync(img, "en-US")` → 특정 단어 Assert.Contains
- 빈 이미지 (흰 배경) → 빈 문자열
- `IsLanguageAvailable("en-US")` → true (모든 Windows에 있음)
- `IsLanguageAvailable("zz-XX")` → false

**CI 고려사항**: GitHub Actions Windows 러너는 영어 OCR 팩이 기본 포함됨. 한국어 테스트는 로컬에서만 실행 가능하므로 CI에서는 영어 기준으로 검증.

### 8.2 설정 라운드트립 테스트
기존 `SettingsManagerTests`에 케이스 추가:
- `settings.Hotkeys.TextCapture = "Ctrl+Alt+O"` 저장 → 로드 → 일치
- `settings.Ocr.Language = "en-US"`, `Ocr.UpscaleSmallImages = false` 저장 → 로드 → 일치

### 8.3 수동 테스트 체크리스트
- 편집기 툴바 OCR 버튼 → 패널 표시 → 복사 → 다른 앱 붙여넣기 작동
- Ctrl+Shift+T → 영역 드래그 → 토스트 표시 → 클립보드에 텍스트 있음
- 언어팩 없는 언어로 설정 후 실행 → 폴백 동작
- 텍스트 없는 이미지 → "텍스트를 찾지 못했습니다" 메시지
- 40px 미만 이미지 → 업스케일 후 인식 성공

---

## 9. 구현 순서 (제안)

1. `csproj` TargetFramework 업데이트 + 빌드 확인
2. `OcrService` 작성 + 단위 테스트
3. `AppSettings` 변경 + 설정 라운드트립 테스트 통과
4. `SettingsWindow` UI 추가 (단축키 행 + OCR 섹션)
5. `CaptureMode.Text` + `MainWindow` 핫키/분기 + 토스트
6. `EditorWindow` 툴바 버튼 + OCR 패널
7. 수동 테스트 + 문서 업데이트

---

## 10. 오픈 이슈 / 향후 확장 (현재 스코프 외)

- 한국어 + 영어 동시 인식 (현재: 단일 언어)
- 이미지 주석 포함한 OCR 옵션
- 텍스트 편집 창을 별도 플로팅 창으로 분리
- 손글씨 인식을 위한 대체 엔진 (Tesseract/AI) — 필요 시 `OcrService`를 인터페이스화
