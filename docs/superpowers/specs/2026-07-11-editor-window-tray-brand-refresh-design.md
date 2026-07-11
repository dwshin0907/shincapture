# 편집기 창 크기 모드·트레이·설치 브랜드 리프레시 설계

작성일: 2026-07-11<br>
대상 버전: v1.4.0(예정)<br>
상태: 내부 설계 검토 승인

## 1. 배경과 확인된 원인

사용자 피드백은 세 가지다.

1. 캡처할 때마다 편집기 창이 이미지 크기에 맞춰 달라져 작업 흐름이 흔들린다.
2. 트레이 아이콘 우클릭 메뉴가 기본 WinForms 메뉴와 이모지 조합이라 제품 UI와 어울리지 않는다.
3. 앱 아이콘과 설치 마법사 안의 이미지가 복잡하고 저해상도라 낡고 흐려 보인다.

코드 분석으로 다음 원인을 확인했다.

- `EditorWindow`는 최초 로드, 새 캡처, 캡처 기록 전환마다 `SizeWindowToImage()`를 호출한다.
- `MainWindow`는 기존 편집기를 다시 열 때마다 `WindowState.Normal`을 강제한다. 이 동작은 최대화 유지와 충돌한다.
- OCR 패널은 주 모니터의 `SystemParameters.WorkArea`를 기준으로 창 높이를 직접 바꾼다. 마지막 사용자 크기 및 보조 모니터와 충돌할 수 있다.
- 트레이 UI는 WPF 테마와 무관한 `System.Windows.Forms.ContextMenuStrip` 기본 렌더링과 유니코드/이모지 아이콘을 사용한다.
- 설치 사이드바는 164×314 BMP, 헤더는 150×57 BMP다. 특히 헤더는 Inno Setup이 요구하는 정사각형 비율과 다르며 고DPI에서 확대된다.

현재 기준선은 `dotnet test ShinCapture.sln --configuration Debug --no-restore`에서 100개 테스트가 모두 통과한다.

## 2. 목표와 비목표

### 목표

- 편집기 창 크기 정책을 `마지막 크기 유지`, `최대화`, `캡처 이미지에 맞춤` 세 모드로 제공한다.
- 기본값은 사용자 피드백을 즉시 해소하는 `마지막 크기 유지`로 한다.
- 사용자가 창 테두리를 마우스로 조절해 확정한 크기만 저장한다.
- 새 캡처와 캡처 기록 전환이 고정 크기/최대화 상태를 바꾸지 않게 한다.
- 혼합 DPI와 다중 모니터에서도 현재 모니터 작업 영역을 기준으로 창과 트레이 플라이아웃을 배치한다.
- 트레이 우클릭 메뉴를 WPF 기반의 현대적인 브랜드 플라이아웃으로 교체한다.
- 앱, 트레이, 설치 마법사가 같은 마크·색·아이콘 문법을 사용하게 한다.
- 고DPI용 설치 PNG와 멀티사이즈 ICO를 생성하고 빌드에서 검증한다.

### 비목표

- 편집기 전체, 설정창 전체, 메인 런처 전체의 리디자인
- 진짜 보더리스 전체화면 모드. 이번 `최대화`는 작업 표시줄을 존중하는 WPF `WindowState.Maximized`다.
- WinUI/Windows App SDK 마이그레이션
- 편집기 창 위치 저장. 위치는 모니터 변경 시 복구 문제가 크므로 크기만 저장한다.
- 앱 전체 다크 테마. 설치 마법사는 시스템 라이트/다크 모드를 따른다.

## 3. 검토한 접근법

### A. 기존 `ContextMenuStrip`만 커스텀 렌더링

장점은 네이티브에 가까운 배치, 포커스, 키보드 동작을 그대로 얻고 변경량이 작다는 점이다. 그러나 둥근 외곽, 자연스러운 그림자, 브랜드 헤더, 2열 액션 타일, 단축키 캡슐을 구현하려면 owner draw와 `ToolStripControlHost`가 겹친다. 요구한 시각 수준에서는 WinForms 한계를 우회하는 코드가 많아진다.

### B. `NotifyIcon` + WPF 트레이 플라이아웃 — 채택

현재 `NotifyIcon`은 유지하고 우클릭 이벤트에서 WPF 무테 창을 띄운다. 기존 WPF 리소스, 실제 `Button`, 포커스 표시, `AutomationProperties`를 재사용하면서 2열 타일과 브랜드 레이아웃을 구현할 수 있다. 포커스·외부 클릭 닫기·DPI 배치는 직접 처리해야 하지만 작은 독립 창으로 한정하면 통제 가능하다. 고대비 모드와 플라이아웃 표시 실패 시 텍스트 중심 네이티브 메뉴로 폴백한다.

### C. WinUI 또는 새 트레이 라이브러리 도입

시각 기능은 풍부하지만 새 런타임, 패키징, 단일 파일 배포, 기존 WPF와의 창 수명주기 문제가 추가된다. 이번 범위에는 과하다.

## 4. 편집기 창 크기 정책

### 4.1 설정 모델

`AppSettings`에 독립적인 `Editor` 섹션을 추가한다.

```text
editor:
  windowSizeMode: rememberLast
  windowWidth: 1100
  windowHeight: 750
```

`EditorWindowSizeMode` 값은 다음과 같다.

- `RememberLast`: 마지막 사용자가 확정한 보통 창 크기 유지. 기본값.
- `Maximized`: 표시할 때마다 현재 모니터 작업 영역으로 최대화.
- `FitToCapture`: 기존 `SizeWindowToImage()` 동작 유지.

이전 JSON에 `editor`가 없으면 프로퍼티 초기값으로 `RememberLast`, 1100×750 DIP를 사용한다. 유효하지 않은 숫자, 너무 작거나 현재 작업 영역보다 큰 값은 정책 계층에서 보정한다.

### 4.2 격리된 구성요소

| 구성요소 | 역할 | 의존성 |
|---|---|---|
| `EditorWindowSizingPolicy` | 저장 크기 검증, 최소/최대 보정, 모드별 자동 크기 변경 여부 결정 | 없음(순수 로직) |
| `MonitorWorkAreaService` | 현재 HWND가 속한 모니터의 물리 작업 영역과 DPI를 WPF DIP로 변환 | Win32 |
| `EditorWindow` | 정책 적용, 사용자 리사이즈 완료 감지, OCR 패널과 정책 조정 | WPF, 위 두 구성요소 |
| `SettingsWindow` | 세 모드 선택 UI와 설명 표시 | WPF, 설정 모델 |
| `SettingsManager.Update` | 최신 설정을 다시 읽고 일부만 갱신해 저장; 필요 시 변경 이벤트 생략 | JSON 설정 |

### 4.3 적용 규칙

- 최초 편집기 표시, 새 캡처 로드, 기록 전환, 설정 변경 후 `ApplyWindowSizingPolicy()` 한 경로만 호출한다.
- `MainWindow`의 직접 `WindowState.Normal` 대입을 제거하고 편집기 정책 메서드로 위임한다.
- `RememberLast`는 최초 적용 또는 다른 모드에서 전환될 때 저장된 크기를 적용한다. 같은 모드에서 새 이미지가 들어오면 외곽 크기를 바꾸지 않는다.
- `Maximized`는 표시·새 캡처마다 최대화를 보장한다.
- `FitToCapture`만 `SizeWindowToImage()`를 호출한다.
- 고정 모드와 최대화 모드에서 OCR 패널은 창 외곽을 키우지 않고 기존 내부 공간을 사용한다. `FitToCapture`에서만 기존 자동 확장을 유지한다.
- 이미지 확대율은 현재 `EditorZoomPolicy`를 그대로 사용한다. 창보다 큰 이미지는 기존 최소 자동 맞춤 비율과 스크롤 동작을 따른다.

### 4.4 사용자 크기 저장

일반 `SizeChanged`에서는 저장하지 않는다. 코드가 이미지나 OCR 패널 때문에 바꾼 크기를 사용자 선택으로 오인할 수 있기 때문이다.

- `SourceInitialized`에서 `HwndSource` hook을 연결한다.
- `WM_ENTERSIZEMOVE`/`WM_EXITSIZEMOVE` 경계를 추적한다.
- `WM_EXITSIZEMOVE`에서 모드가 `RememberLast`, 상태가 `Normal`, 정책 적용 중이 아니며 `RestoreBounds`가 유효할 때만 폭·높이를 저장한다.
- 마우스로 이동만 한 경우 같은 크기가 다시 저장되는 것은 허용한다. 위치는 저장하지 않는다.
- Win+화살표 스냅과 최대화/복원은 이번 저장 신호의 보장 범위가 아니다. 지속적인 최대화는 별도 `Maximized` 모드가 담당한다.
- hook은 실제 `Closed`/`ForceClose`에서 제거한다.

설정 저장은 최신 JSON을 다시 읽은 뒤 `Editor` 부분만 갱신하고, 창 드래그 한 번마다 전역 단축키를 재등록하지 않도록 `SettingsChanged` 이벤트를 발생시키지 않는 갱신 경로를 사용한다.

### 4.5 DPI와 모니터

- `MonitorFromWindow`와 `GetMonitorInfo`로 현재 모니터 작업 영역을 얻는다.
- `GetDpiForWindow` 기준으로 물리 픽셀을 DIP로 변환한다.
- 음수 좌표 모니터와 서로 다른 100/125/150/200% 배율을 허용한다.
- 저장 크기는 DIP이므로 모니터 이동 후에도 시각적 크기를 유지하고, 새 작업 영역을 넘으면 보정한다.
- 기존 `SizeWindowToImage()`와 OCR 자동 확장도 같은 모니터 서비스로 통일한다.

## 5. 트레이 플라이아웃

### 5.1 창과 수명주기

`TrayFlyoutWindow`는 한 번 만들고 재사용하는 `WindowStyle=None`, `ShowInTaskbar=false`, `ResizeMode=NoResize`, 불투명 배경의 WPF 창이다.

1. `NotifyIcon.ContextMenuStrip` 자동 연결을 제거한다.
2. 오른쪽 `MouseUp`에서 현재 설정과 단축키를 플라이아웃에 반영한다.
3. 창을 표시하고 레이아웃을 측정한 뒤, 커서가 있는 모니터의 물리 작업 영역에서 `SetWindowPos`로 배치한다.
4. 기본 위치는 커서 오른쪽에 정렬하되 아래 공간이 부족하면 위로, 좌우가 넘으면 작업 영역 안으로 보정한다. 위·아래·좌·우 작업 표시줄을 모두 같은 clamp 규칙으로 처리한다.
5. `SetForegroundWindow` 후 첫 액션에 키보드 포커스를 둔다.
6. 액션 실행, `Escape`, `Deactivated`에서 숨긴다.
7. 창 표시 중 예외가 나거나 `SystemParameters.HighContrast`이면 네이티브 텍스트 메뉴를 표시한다.

Windows 11에서는 DWM 둥근 모서리를 요청한다. 이를 지원하지 않는 환경에서는 일반 불투명 직사각형 창으로 안전하게 폴백하고, 내부 타일의 라운딩만 유지한다. 투명 레이어 창에 의존하지 않아 텍스트 렌더링과 그림자 성능을 보존한다.

### 5.2 정보 구조

플라이아웃 폭은 약 380 DIP, 일반 높이는 600 DIP 이하로 제한한다.

```text
┌ 신캡쳐                         준비됨 ┐
│ 편집기 크기 · 마지막 크기 유지        │
├───────────────────────────────────────┤
│ [ 영역 캡처                 PrtSc ]   │  주 액션
│ [ 창 캡처 ]        [ 전체 화면 ]     │
│ [ 스크롤 ]          [ 스마트 컷 ]     │
│ [ 지정 크기 ]       [ 자유형 ]        │
│ [ 단위 영역 ]       [ 텍스트 ]        │
│ [ 텍스트 + 번역               ]      │
├───────────────────────────────────────┤
│ [ 편집기 열기 ]     [ 저장 폴더 ]     │
│ 설정          도움말·정보        종료  │
└───────────────────────────────────────┘
```

- 이모지와 임의 유니코드 기호는 모두 동일 굵기의 벡터 line icon으로 교체한다.
- 주요 캡처는 2열 타일, 영역 캡처와 번역은 긴 타일로 배치한다.
- 사용자 지정 단축키를 설정에서 읽어 우측 또는 보조 텍스트로 표시한다. 하드코딩하지 않는다.
- 실제 WPF `Button`을 사용하고 `AutomationProperties.Name`, 명확한 포커스 링, 최소 40 DIP 클릭 영역을 제공한다.
- 종료는 일반 액션과 분리하고 호버에서만 위험 색을 사용한다.
- 현재 편집기 크기 모드는 헤더의 읽기 전용 상태 텍스트로 보여준다. 모드 변경은 설정창에서 한다.

### 5.3 명령 경계

`TrayMenuCatalog`는 캡처 모드, 라벨, 아이콘 키, 현재 단축키를 순수 데이터로 만든다. `TrayFlyoutWindow`는 표시와 입력만 담당하고 `CaptureRequested`/`CommandRequested` 이벤트를 낸다. 실제 캡처, 설정, 폴더, 종료 동작은 계속 `MainWindow`가 수행한다.

## 6. 브랜드와 설치 자산

### 6.1 시각 방향

- 복잡한 파란 그라데이션, 오렌지 펜 배지, 작은 기능 목록을 제거한다.
- 단색 인디고 기반의 스퀘어와 `S`를 연상시키는 캡처 프레임 마크를 사용한다.
- 보조색은 민트 한 색만 사용하고 장식보다 16px 식별성을 우선한다.
- 앱 아이콘, 트레이 아이콘, 설치 사이드바, 설치 헤더가 같은 마크 비율과 획 굵기를 공유한다.
- 설치 이미지 안에는 버전이나 기능 목록을 굽지 않는다. 버전과 설명은 접근 가능한 설치 텍스트가 담당한다.

### 6.2 생성 파이프라인

기존 두 생성 스크립트를 `tools/gen_brand_assets.py` 한 곳으로 통합하거나 같은 내부 모듈을 공유하게 한다. 결과는 결정적으로 재생성 가능해야 한다.

- `Assets/icon.ico`: 16, 20, 24, 32, 40, 48, 64, 128, 256px. 16/20/24px는 단순화하고 픽셀 경계에 맞춘다.
- `Assets/icon_preview.png`: 256px 검토용.
- 트레이 line icon PNG 또는 WPF Geometry 리소스: 동일한 viewBox와 획 굵기.
- `installer/wizard_sidebar.png`: 164:314 비율, 최소 534×1022.
- `installer/wizard_sidebar_dark.png`: 동적 다크 모드용.
- `installer/wizard_mark.png`, `wizard_mark_dark.png`: 정사각형, 최소 159×159.

Inno Setup 공식 문서는 `SetupIconFile`에 16/32/48/64/256px를 권장하고, 고DPI `WizardImageFile`은 202×386보다 큰 이미지, `WizardSmallImageFile`은 58×58보다 큰 정사각형 이미지를 권장한다. 이번 자산은 250% DPI 권장 크기까지 포함한다.

공식 문서:

- <https://jrsoftware.org/ishelp/topic_setup_setupiconfile.htm>
- <https://jrsoftware.org/ishelp/topic_setup_wizardimagefile.htm>
- <https://jrsoftware.org/ishelp/topic_setup_wizardsmallimagefile.htm>
- <https://jrsoftware.org/ishelp/topic_setup_wizardstyle.htm>

### 6.3 설치 스크립트

- 설치 환경의 Inno Setup 6.7.1을 기준으로 `WizardStyle=modern dynamic windows11 hidebevels includetitlebar` 계열을 사용한다.
- 라이트/다크 사이드바와 정사각형 헤더 마크를 각각 연결한다.
- 하드코딩된 사이드바 `v1.0.0`을 제거한다.
- 환영 페이지의 긴 7개 기능 목록을 세 개의 핵심 가치로 줄여 여백과 가독성을 확보한다.
- 완료 페이지의 긴 도구 나열도 빠른 시작 단축키와 트레이 상주 안내 중심으로 줄인다.
- 실제 연결이 없는 `quicklaunchicon` 작업은 제거한다.

## 7. 데이터 흐름

### 편집기

1. 설정 로드 → `EditorSettings` 기본값/저장값 확보.
2. 편집기 표시 또는 이미지 변경 → 현재 모드 조회.
3. `MonitorWorkAreaService` → 현재 모니터 작업 영역(DIP).
4. `EditorWindowSizingPolicy` → 적용 크기/상태 결정.
5. 창 정책 적용 → 레이아웃 측정 → 기존 초기 확대율 적용.
6. 사용자가 마우스 리사이즈 종료 → 유효한 `RestoreBounds`만 설정에 조용히 저장.

### 트레이

1. 우클릭 → 설정/단축키로 `TrayMenuCatalog` 갱신.
2. 고대비 여부 확인 → WPF 플라이아웃 또는 네이티브 폴백.
3. 커서 모니터/DPI로 위치 계산 → 플라이아웃 표시와 포커스.
4. 버튼 이벤트 → 플라이아웃 숨김 → `MainWindow` 명령 실행.

### 브랜드/설치

1. 단일 브랜드 생성 스크립트 실행.
2. ICO, 앱 프리뷰, 설치 라이트/다크 PNG 생성.
3. 프로젝트 리소스 및 `setup.iss`가 생성 결과를 참조.
4. 이미지 크기 검사 → 앱 publish → Inno Setup 컴파일.

## 8. 오류 처리와 폴백

- 설정 파일에 `editor`가 없거나 숫자가 잘못되면 1100×750 DIP 기본값을 사용한다.
- 작업 영역이 최소 크기보다 작으면 작업 영역 자체를 상한으로 삼아 화면 밖으로 나가지 않게 한다.
- 모니터/DPI Win32 호출이 실패하면 WPF 기본 작업 영역과 현재 DPI를 사용한다.
- 사용자 크기 저장 실패는 편집 흐름을 중단하지 않으며 다음 정상 종료/리사이즈 때 다시 저장할 수 있다.
- 트레이 WPF 창 생성, 포커스, 위치 지정 중 실패하면 즉시 네이티브 메뉴를 표시한다.
- 고대비 모드에서는 커스텀 색과 타일보다 시스템 메뉴를 우선한다.
- 브랜드 자산이 누락되면 빌드/설치 검증이 실패하게 하여 런타임에서 조용히 낡은 자산으로 돌아가지 않게 한다.

## 9. 테스트와 검증

### 자동 테스트

- `EditorWindowSizingPolicyTests`
  - 유효한 마지막 크기 보존
  - NaN/무한대/0/음수의 기본값 폴백
  - 최소 크기와 현재 모니터 작업 영역 상한 보정
  - 모드별 자동 맞춤/최대화/유지 결정
- `SettingsManagerTests`
  - `editor` 없는 이전 JSON의 기본값 마이그레이션
  - enum과 폭·높이 round-trip
  - 부분 갱신이 다른 설정을 보존
  - 조용한 갱신이 `SettingsChanged`를 발생시키지 않음
- `TrayMenuCatalogTests`
  - 모든 캡처 모드가 한 번씩 존재
  - 사용자 지정 단축키 반영
  - 주요/보조 액션 순서와 아이콘 키 존재

### 빌드 검증

- `dotnet test ShinCapture.sln`
- `dotnet build ShinCapture.sln -c Release`
- `dotnet publish src/ShinCapture/ShinCapture.csproj -c Release`
- 브랜드 생성 스크립트 실행 후 ICO 포함 크기와 PNG 픽셀 크기 검사
- Inno Setup 6.7.1 `ISCC.exe installer/setup.iss` 컴파일

### 수동 검증

- 100/125/150/200% 배율과 보조 모니터 음수 좌표에서 세 창 모드 확인
- 마우스 리사이즈 후 새 캡처, 숨김/재표시, 앱 재시작에서 크기 유지 확인
- 최대화 후 캡처와 기록 전환에서 최대화 유지 확인
- OCR 패널 열기/닫기가 고정 모드 외곽 크기를 바꾸지 않는지 확인
- 트레이 우클릭, 바깥 클릭, Escape, 키보드 탐색, 빠른 연속 열기 확인
- 위/아래/왼쪽/오른쪽 작업 표시줄에서 플라이아웃이 화면 안에 있는지 확인
- Windows 고대비에서 네이티브 폴백 확인
- 16/20/24px 앱·트레이 아이콘과 설치 100/150/200/250% DPI 육안 확인

## 10. 완료 기준

- 기본 설치/업그레이드 후 캡처 이미지 크기가 달라도 편집기 창은 마지막 사용자 크기를 유지한다.
- 설정에서 최대화 또는 기존 이미지 맞춤 동작을 선택할 수 있다.
- 모든 편집기 표시 경로와 기록 전환이 같은 크기 정책을 사용한다.
- 트레이 메뉴에 이모지가 없고, 모든 기존 명령과 누락됐던 스마트 컷/번역 진입점을 포함한다.
- 커스텀 플라이아웃 실패와 고대비 환경에서 기능을 잃지 않는다.
- 앱/설치 아이콘과 설치 이미지가 하나의 브랜드 문법을 공유하고 고DPI에서 흐려지지 않는다.
- 기존 100개 테스트와 신규 테스트가 모두 통과하며 Release publish와 설치 프로그램 컴파일이 성공한다.
