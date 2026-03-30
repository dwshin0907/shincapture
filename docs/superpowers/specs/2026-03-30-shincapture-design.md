# 신캡쳐 (ShinCapture) — 설계 문서

> 작성일: 2026-03-30
> 버전: v1.0 설계
> 상태: 승인됨

---

## 1. 개요

### 1.1 목적
알캡쳐(AlCapture)를 대체하는 광고 없는 무료 화면 캡쳐 + 편집 프로그램. 캡쳐 후 모자이크, 그리기 등 편집 기능을 광고 없이 즉시 사용할 수 있는 것이 핵심 가치.

### 1.2 대상 사용자
- 화면 캡쳐를 자주 사용하는 일반 PC 사용자
- 캡쳐 후 편집(모자이크, 화살표, 텍스트 등)이 필요한 직장인/블로거
- 알캡쳐의 광고에 불만이 있는 사용자

### 1.3 핵심 가치
- **광고 없음** — 캡쳐~편집~저장까지 방해 요소 제로
- **가볍고 빠름** — 네이티브 Windows 앱, 낮은 메모리 사용
- **알캡쳐 완전 대체** — 7가지 캡쳐 모드 + 편집 기능 모두 지원

### 1.4 비기능 목표
- 설치 용량 30MB 이하
- 메모리 사용 유휴시 30MB 이하, 편집시 150MB 이하
- 캡쳐 반응 시간 200ms 이하 (단축키 입력 → 오버레이 표시)
- Windows 10/11 지원

---

## 2. 기술 스택

| 항목 | 선택 | 이유 |
|------|------|------|
| 언어 | C# (.NET 8) | 최신 LTS, Windows 네이티브 성능 |
| UI 프레임워크 | WPF | 투명 오버레이, 커스텀 렌더링, GPU 가속 |
| 디자인 시스템 | Windows 11 Fluent Design | Mica 배경, 라운드 코너, Segoe UI |
| 이미지 처리 | System.Drawing + SkiaSharp | 비트맵 조작, 필터, 고성능 렌더링 |
| 설정 저장 | JSON (System.Text.Json) | 사람이 읽을 수 있는 설정 파일 |
| 인스톨러 | Inno Setup | 무료, 가볍고, 커스터마이징 유연 |
| 빌드 | dotnet publish (self-contained) | 단일 실행파일 배포 가능 |

---

## 3. 아키텍처

### 3.1 전체 구조

단일 WPF 프로세스, 5개 핵심 모듈.

```
┌─────────────────────────────────────────────────┐
│              신캡쳐 (ShinCapture)                  │
│              단일 WPF 프로세스 (.NET 8)             │
├─────────────────────────────────────────────────┤
│                                                 │
│  ┌────────────┐  ┌─────────────┐  ┌───────────┐│
│  │ TrayManager│→│CaptureEngine│→│ImageEditor ││
│  │            │  │             │  │           ││
│  │ 시스템트레이│  │ 7가지 모드   │  │ 그리기도구 ││
│  │ 글로벌단축키│  │ 투명오버레이 │  │ 효과도구  ││
│  │ 메인윈도우 │  │ 돋보기/색상  │  │ 편집도구  ││
│  └────────────┘  └─────────────┘  └───────────┘│
│        │                               │        │
│  ┌────────────┐                 ┌───────────┐  │
│  │ Settings   │                 │SaveManager │  │
│  │ Manager    │                 │            │  │
│  │            │                 │ PNG/JPG/   │  │
│  │ 단축키설정 │                 │ BMP/GIF    │  │
│  │ 캡쳐설정   │                 │ 클립보드   │  │
│  │ 저장설정   │                 │ 자동저장   │  │
│  └────────────┘                 └───────────┘  │
└─────────────────────────────────────────────────┘
```

### 3.2 모듈 상세

#### TrayManager
- 시스템 트레이 아이콘 등록 및 상주
- 트레이 우클릭 컨텍스트 메뉴 (7가지 캡쳐 모드 + 최근캡쳐 + 설정 + 종료)
- 트레이 더블클릭 → 메인 윈도우 토글
- Win32 `RegisterHotKey` API로 글로벌 단축키 등록
- 종료 버튼 → 트레이로 최소화 (설정으로 변경 가능)

#### CaptureEngine
- 7가지 캡쳐 모드 실행 (상세: 섹션 4)
- 전체화면 투명 오버레이 윈도우 관리
- 캡쳐 완료 후 비트맵을 ImageEditor 또는 SaveManager로 전달

#### ImageEditor
- 캡쳐 이미지 편집 윈도우 (상세: 섹션 5)
- WPF Canvas 기반 편집 표면
- 도구별 독립적인 Tool 클래스 구조
- Undo/Redo 스택 (Command 패턴)

#### SettingsManager
- JSON 파일로 설정 저장/로드 (`%AppData%/ShinCapture/settings.json`)
- 설정 변경 이벤트 발행 (옵저버 패턴)
- 기본값 제공 + 마이그레이션 지원

#### SaveManager
- 파일 저장 (PNG/JPG/BMP/GIF)
- 클립보드 복사
- 자동 파일명 생성 (`신캡쳐_YYYYMMDD_HHmmss.png`)
- 최근 캡쳐 목록 관리 (최대 100장, 썸네일 캐시)

### 3.3 테마 시스템 (디자인 변경 용이성)

WPF ResourceDictionary로 테마를 분리하여 한 곳에서 수정하면 전체 반영:

```
Themes/
  LightTheme.xaml    ← 색상, 간격, 폰트 크기 등 상수 정의
  DarkTheme.xaml     ← 다크 모드 (향후 추가 용이)
```

주요 테마 속성:
- `BackgroundPrimary`, `BackgroundSecondary`, `BackgroundTertiary`
- `TextPrimary`, `TextSecondary`, `TextDisabled`
- `AccentColor` (#0078D4)
- `ToolbarButtonActive` (#e8e8e8), `ToolbarButtonHover` (#f0f0f0)
- `CornerRadius` (6px/8px), `ToolbarPadding`, `ToolbarGap`
- `FontFamily` (Segoe UI Variable)

---

## 4. 캡쳐 엔진 상세

### 4.1 캡쳐 모드

| 모드 | 기본 단축키 | 구현 방식 |
|------|------------|----------|
| 영역지정 | PrintScreen | 투명 오버레이 → 마우스 드래그 사각형 선택 |
| 자유형 | Ctrl+Shift+F | 오버레이 위 마우스 경로 폴리곤 추적 → 클리핑 |
| 창 캡쳐 | Ctrl+Shift+W | `EnumWindows` + `GetWindowRect`로 윈도우 핸들 감지 → 하이라이트 → 클릭 캡쳐 |
| 단위영역 | Ctrl+Shift+D | `UIAutomation` API로 컨트롤 요소 감지 → 하이라이트 |
| 전체화면 | Ctrl+Shift+A | 즉시 전체 비트맵 캡쳐 (멀티 모니터: 모니터 선택 가능) |
| 스크롤 | Ctrl+Shift+S | 영역 지정 후 자동 `SendMessage(WM_SCROLL)` + 프레임별 캡쳐 → 이미지 스티칭 |
| 지정사이즈 | Ctrl+Shift+Z | 고정 프레임 마우스 이동 → 클릭 캡쳐, 사이즈 프리셋 설정 가능 |

### 4.2 캡쳐 공통 흐름

```
1. 단축키 입력 감지 (TrayManager → CaptureEngine)
2. 전체 화면 비트맵을 메모리에 캡쳐 (BitBlt)
3. 투명 오버레이 윈도우를 전체 모니터에 표시
4. 캡쳐한 비트맵을 오버레이 배경으로 사용 (반투명 어둡게)
5. 모드별 영역 선택 UI 표시
6. 사용자가 영역 확정 (클릭/드래그 완료)
7. 선택 영역 잘라내기
8. 설정에 따라 분기:
   - "편집기 열기" → ImageEditor에 비트맵 전달
   - "바로 저장" → SaveManager에 비트맵 전달
   - "클립보드만" → 클립보드 복사 후 완료
```

### 4.3 오버레이 공통 기능

- **돋보기**: 커서 근처 2~4배 확대 미리보기, 200x200px 크기
- **색상코드**: 커서 위치 픽셀 HEX 값 실시간 표시
- **십자선 가이드**: 화면 가로/세로 가이드라인 (정밀 선택용)
- **좌표 표시**: 현재 마우스 X, Y 좌표
- **영역 크기**: 드래그 중 선택 영역 가로x세로 픽셀 수 표시
- **ESC 취소**: 언제든 캡쳐 취소

---

## 5. 편집기 상세

### 5.1 레이아웃

상단 툴바형 레이아웃 (Windows 11 Fluent Design):

```
┌─────────────────────────────────────────────────┐
│ [앱아이콘] 신캡쳐 — filename.png      [─][□][✕] │  ← 타이틀바
├─────────────────────────────────────────────────┤
│ [펜][형광펜][도형][화살표][텍스트] | [모자이크]   │
│ [블러][번호][말풍선] | [크롭][지우개][스포이드]   │  ← 메인 툴바
│ [이미지] |                            [↩][↪]    │
├─────────────────────────────────────────────────┤
│ 색상 ●●●●●●◐ | 굵기 [3px▾] | 투명도 ━━ | 채우기 │  ← 속성 서브바
├─────────────────────────────────────────────────┤
│                                                 │
│              캡쳐 이미지 캔버스                    │  ← 캔버스 (스크롤/줌)
│              (WPF Canvas)                       │
│                                        [100%▾]  │
├─────────────────────────────────────────────────┤
│ 1920×1080 px · PNG · 2.4MB    [복사][다른이름][저장] │  ← 하단 상태바
└─────────────────────────────────────────────────┘
```

### 5.2 도구 목록

#### 그리기 도구
| 도구 | 설명 | 속성 |
|------|------|------|
| 펜 | 자유 그리기 | 색상, 굵기, 투명도 |
| 형광펜 | 반투명 강조 | 색상, 굵기, 투명도(기본 50%) |
| 도형 | 사각형, 원, 선, 점선 | 색상, 굵기, 채우기(없음/단색/반투명) |
| 화살표 | 방향 화살표 | 색상, 굵기, 머리 크기 |
| 텍스트 | 글자 입력 | 색상, 글꼴, 크기, 굵기/기울임/밑줄 |

#### 효과 도구
| 도구 | 설명 | 속성 |
|------|------|------|
| 모자이크 | 영역 픽셀화 | 블록 크기 (작게/보통/크게) |
| 블러 | 가우시안 블러 | 강도 (약/중/강) |
| 번호매기기 | 자동 증가 번호 원 | 색상, 시작 번호 |
| 말풍선 | 텍스트 + 꼬리 | 색상, 스타일(둥근/사각) |

#### 편집 도구
| 도구 | 설명 |
|------|------|
| 크롭 | 영역 선택 후 잘라내기 |
| 지우개 | 편집 객체 클릭 삭제 (원본은 유지) |
| 스포이드 | 이미지에서 색상 추출 |
| 이미지 삽입 | 외부 이미지 파일 위에 배치 |

### 5.3 도구 상태 UI

은은한 회색 톤 변화로 상태 구분:

| 상태 | 배경 | 테두리 | 텍스트 |
|------|------|--------|--------|
| 비활성 | 투명 | 없음 | #555 |
| 호버 | #f0f0f0 | 없음 | #444 |
| 활성 (사용중) | #e8e8e8 | #d0d0d0 | #333 |
| 비활성 (불가) | 투명 | 없음 | #ccc |

### 5.4 편집 시스템

- **Command 패턴**: 모든 편집 동작을 Command 객체로 캡슐화
- **Undo/Redo**: Command 스택으로 무제한 실행취소/다시실행
- **객체 기반**: 그린 도형/텍스트는 개별 객체로 관리, 선택/이동/삭제 가능
- **비파괴 편집**: 원본 이미지는 항상 보존, 편집은 레이어 위에서 수행

### 5.5 캔버스

- WPF Canvas 기반, GPU 가속 렌더링
- 마우스 휠 줌 (25%~400%)
- 드래그로 캔버스 이동 (Space + 드래그 또는 휠 클릭)
- 이미지 크기에 맞춰 자동 Fit

---

## 6. 단축키 시스템

### 6.1 글로벌 단축키 (기본값)

| 단축키 | 기능 |
|--------|------|
| PrintScreen | 영역지정 캡쳐 |
| Ctrl+Shift+F | 자유형 캡쳐 |
| Ctrl+Shift+W | 창 캡쳐 |
| Ctrl+Shift+D | 단위영역 캡쳐 |
| Ctrl+Shift+A | 전체화면 캡쳐 |
| Ctrl+Shift+S | 스크롤 캡쳐 |
| Ctrl+Shift+Z | 지정사이즈 캡쳐 |

모든 글로벌 단축키는 환경설정에서 커스터마이징 가능. `RegisterHotKey` Win32 API 사용.

### 6.2 편집기 단축키

| 단축키 | 기능 |
|--------|------|
| Ctrl+Z | 실행취소 |
| Ctrl+Y | 다시실행 |
| Ctrl+S | 저장 |
| Ctrl+Shift+S | 다른 이름으로 저장 |
| Ctrl+C | 클립보드 복사 |
| Ctrl+P | 인쇄 |
| ESC | 편집기 닫기 |
| Delete | 선택 객체 삭제 |
| Ctrl+A | 전체 선택 |

---

## 7. 설정 시스템

### 7.1 설정 파일

경로: `%AppData%/ShinCapture/settings.json`

```json
{
  "general": {
    "autoStart": false,
    "minimizeToTray": true,
    "language": "ko"
  },
  "capture": {
    "afterCapture": "openEditor",
    "magnifierZoom": 2,
    "showCrosshair": true,
    "showColorCode": true
  },
  "save": {
    "defaultFormat": "png",
    "jpgQuality": 90,
    "autoSavePath": "%Pictures%/ShinCapture",
    "fileNamePattern": "신캡쳐_{date}_{time}",
    "autoSave": false,
    "copyToClipboard": true
  },
  "hotkeys": {
    "regionCapture": "PrintScreen",
    "freeformCapture": "Ctrl+Shift+F",
    "windowCapture": "Ctrl+Shift+W",
    "elementCapture": "Ctrl+Shift+D",
    "fullscreenCapture": "Ctrl+Shift+A",
    "scrollCapture": "Ctrl+Shift+S",
    "fixedSizeCapture": "Ctrl+Shift+Z"
  },
  "fixedSizes": [
    { "name": "HD", "width": 1280, "height": 720 },
    { "name": "FHD", "width": 1920, "height": 1080 }
  ],
  "recentCaptures": {
    "maxCount": 100
  }
}
```

### 7.2 환경설정 윈도우

탭 구성:
- **일반** — 자동실행, 트레이 최소화, 언어
- **캡쳐** — 캡쳐 후 행동, 돋보기 배율, 십자선, 색상코드
- **저장** — 기본 형식, JPG 품질, 자동저장 경로, 파일명 패턴
- **단축키** — 글로벌 단축키 커스터마이징 (충돌 감지 포함)
- **지정사이즈** — 프리셋 추가/편집/삭제

---

## 8. 저장 및 파일 관리

### 8.1 저장 흐름

```
캡쳐 완료 → 설정에 따라 분기:
  ├─ "편집기 열기" → 편집 → 저장 버튼
  ├─ "바로 저장" → 자동저장 경로에 즉시 저장
  └─ "클립보드만" → 클립보드 복사

저장 버튼:
  └─ 자동저장 경로 + 자동 파일명으로 즉시 저장

다른 이름으로 저장:
  └─ SaveFileDialog → 경로/파일명/형식 선택

복사 버튼:
  └─ 편집 결과를 클립보드에 복사
```

### 8.2 지원 형식

| 형식 | 옵션 | 기본값 |
|------|------|--------|
| PNG | 무손실 | 기본 형식 |
| JPG | 품질 1~100 | 90 |
| BMP | 없음 | - |
| GIF | 256색 변환 | - |

### 8.3 최근 캡쳐 목록

- 최대 100장 보관
- 썸네일 캐시 (150x100px)
- 메인 윈도우 및 트레이 메뉴에서 접근
- 클릭시 편집기에서 다시 열기
- 원본 파일 삭제시 목록에서 자동 제거

---

## 9. UI 디자인

### 9.1 디자인 시스템

Windows 11 Fluent Design 준수:

| 요소 | 값 |
|------|-----|
| 액센트 색상 | #0078D4 |
| 배경 Primary | #f3f3f3 |
| 배경 Secondary | #fbfbfb |
| 배경 Tertiary | #f7f7f7 |
| 텍스트 Primary | #191919 |
| 텍스트 Secondary | #555555 |
| 텍스트 Disabled | #cccccc |
| 보더 | #e5e5e5 |
| 코너 라운드 (버튼) | 6px |
| 코너 라운드 (패널) | 8px |
| 폰트 | Segoe UI Variable |
| 그림자 | 0 2px 8px rgba(0,0,0,0.08) |

### 9.2 테마 변경 용이성

모든 색상, 간격, 폰트 크기를 WPF ResourceDictionary로 분리:
- `Themes/LightTheme.xaml` — 라이트 모드 (기본)
- 향후 `DarkTheme.xaml` 추가 용이
- 런타임 테마 전환 가능한 구조

### 9.3 메인 윈도우

- 트레이 아이콘 더블클릭으로 열기
- 7가지 캡쳐 모드 그리드 버튼 (2열 배치)
- 각 버튼에 아이콘 + 모드명 + 단축키 표시
- 최근 캡쳐 썸네일 미리보기 (가로 스크롤)
- 하단에 버전 정보 + 설정 버튼

### 9.4 트레이 메뉴

- Windows 11 스타일 컨텍스트 메뉴 (라운드 코너, 그림자)
- 7가지 캡쳐 모드 + 단축키 텍스트
- 구분선으로 그룹 분리: 캡쳐 모드 | 최근캡쳐·저장폴더 | 설정·정보 | 종료

---

## 10. 배포

### 10.1 배포 형태

| 형태 | 설명 |
|------|------|
| 인스톨러 (Setup.exe) | Inno Setup, 시작메뉴/바탕화면 아이콘, 시작프로그램 등록 옵션 |
| 포터블 (zip) | 압축 해제 후 바로 실행, 설정은 실행파일 옆 `config/` 폴더에 저장 (인스톨러 버전은 `%AppData%`) |

### 10.2 배포 채널

- 자체 홈페이지에서 다운로드
- 소스코드 비공개, 실행파일만 배포

### 10.3 자동 업데이트

v1.0에서는 수동 업데이트 (홈페이지에서 재다운로드). 향후 자동 업데이트 시스템 추가 가능.

---

## 11. 범위 외 (v2 이후)

- 화면 녹화 / GIF 제작
- 다크 모드
- OCR 텍스트 추출
- AI 기능 (배경 제거, 화질 개선 등)
- 자동 업데이트 시스템
- 다국어 지원 (영어 등)
- 클라우드 저장/동기화

---

## 12. 프로젝트 구조 (예상)

```
ShinCapture/
├── ShinCapture.sln
├── src/
│   └── ShinCapture/
│       ├── App.xaml                    ← 앱 진입점
│       ├── Models/                     ← 데이터 모델
│       │   ├── CaptureMode.cs
│       │   ├── Settings.cs
│       │   └── RecentCapture.cs
│       ├── Services/                   ← 비즈니스 로직
│       │   ├── TrayManager.cs
│       │   ├── CaptureEngine.cs
│       │   ├── SaveManager.cs
│       │   └── SettingsManager.cs
│       ├── Views/                      ← UI (XAML)
│       │   ├── MainWindow.xaml
│       │   ├── EditorWindow.xaml
│       │   ├── SettingsWindow.xaml
│       │   └── Overlay/
│       │       ├── CaptureOverlay.xaml
│       │       └── Magnifier.xaml
│       ├── ViewModels/                 ← MVVM 뷰모델
│       │   ├── MainViewModel.cs
│       │   ├── EditorViewModel.cs
│       │   └── SettingsViewModel.cs
│       ├── Tools/                      ← 편집 도구
│       │   ├── ITool.cs
│       │   ├── PenTool.cs
│       │   ├── HighlighterTool.cs
│       │   ├── ShapeTool.cs
│       │   ├── ArrowTool.cs
│       │   ├── TextTool.cs
│       │   ├── MosaicTool.cs
│       │   ├── BlurTool.cs
│       │   ├── NumberTool.cs
│       │   ├── BalloonTool.cs
│       │   ├── CropTool.cs
│       │   ├── EraserTool.cs
│       │   ├── EyedropperTool.cs
│       │   └── ImageInsertTool.cs
│       ├── Commands/                   ← Undo/Redo
│       │   ├── ICommand.cs
│       │   ├── CommandStack.cs
│       │   ├── DrawCommand.cs
│       │   └── CropCommand.cs
│       ├── Themes/                     ← 테마 리소스
│       │   └── LightTheme.xaml
│       ├── Assets/                     ← 아이콘, 이미지
│       │   └── icon.ico
│       └── Helpers/                    ← 유틸리티
│           ├── NativeMethods.cs        ← Win32 API P/Invoke
│           ├── ScreenHelper.cs
│           └── ImageHelper.cs
├── installer/
│   └── setup.iss                       ← Inno Setup 스크립트
└── README.md
```
