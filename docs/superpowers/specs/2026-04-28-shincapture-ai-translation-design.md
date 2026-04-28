# 신캡쳐 v1.2.0 — OCR 번역 + AI 인프라 설계

- **작성일**: 2026-04-28
- **대상 버전**: v1.2.0
- **선행 버전**: v1.1.0 (OCR 추가)
- **후속 계획**: v1.3.0 (AI 캡쳐 분석), v1.4.0 (배경 제거 등)

## 1. 목적

v1.1.0에서 추가한 OCR 결과를 OpenAI API로 번역하는 기능을 추가한다. 동시에 v1.3.0/v1.4.0에서 재사용할 **AI 인프라**(보안 키 저장 + OpenAI 호출 게이트웨이)를 함께 구축한다.

홈페이지 포지셔닝("AI 시대에 가장 어울리는, 광고 없는 캡쳐 프로그램")을 실제 기능으로 강화한다.

## 2. 제약 / 비기능 요구사항

- **BYOK (Bring Your Own Key)**: 사용자가 직접 OpenAI 키를 발급받아 입력. 우리 서버를 거치지 않음.
- **키는 PC에만 저장**: 평문 저장 금지, OS 수준 암호화 필수.
- **호환성**: TargetFramework 변경 없음 (`net8.0-windows10.0.19041.0` 유지).
- **기존 사용자 영향 없음**: AI 키 미설정 시 v1.1.0과 동일하게 동작.
- **YAGNI**: v1.2.0 범위는 OpenAI 1개 제공자, 번역 1개 기능만.

## 3. 분해 / 후속 계획

A(OCR 번역) + B(AI 캡쳐 분석) + D(편집기 AI 강화)는 모두 외부 API 호출이 필요하므로 공통 인프라(보안 키 저장 + AI 게이트웨이)를 공유한다. 한 사이클에 셋 다 담으면 범위가 폭주하므로 점진 분해한다.

| 버전 | 범위 |
|---|---|
| **v1.2.0 (본 spec)** | 보안 인프라 + OCR 번역 |
| v1.3.0 | AI 캡쳐 분석 (이미지 → 설명) |
| v1.4.0 | 편집기 AI (배경 제거 등) |

OpenAI 키 1개를 v1.2.0에서 받으면 v1.3.0/v1.4.0에서 재사용. 인프라(`OpenAiClient`, `IAiCredentialStore`)는 처음부터 다중 기능 호출이 가능하게 설계.

## 4. 모듈 구조

```
Services/Ai/
├── IAiCredentialStore.cs       (인터페이스 — 테스트 모킹용)
├── DpapiCredentialStore.cs     (DPAPI 구현, apikey.dat 입출력)
├── OpenAiClient.cs             (HTTP 게이트웨이, TLS+도메인 검증)
├── TranslationService.cs       (OCR 텍스트 → 번역문)
└── AiSettings.cs               (모델/대상언어/타임아웃 DTO)

UI/Settings/
└── AiSettingsTab.xaml          (설정창 신규 탭)
```

**책임 분리:**

- **`OpenAiClient`** = OpenAI 호출의 단일 입구. v1.3.0/v1.4.0에서 메서드(`PostVisionAsync`, `PostImageAsync`)만 추가.
- **`TranslationService`** = 번역 도메인 로직. HTTP 디테일은 `OpenAiClient`에 위임.
- **`IAiCredentialStore`** = 키 저장 추상화. 실제 DPAPI는 Windows 의존이라 단위 테스트엔 인메모리 fake 사용.
- **`apikey.dat`** = `settings.json`과 분리 저장. 설정 백업/내보내기 시 키가 따라가지 않음.

**기존 코드 영향:**

- `Services/OcrService.cs` — 변경 없음
- `MainWindow.xaml.cs` — `RunOcrAndNotify` 호출부에 번역 분기 추가, `Ctrl+Shift+L` 핸들러 추가
- `EditorWindow.xaml.cs` — OCR 패널에 번역 버튼/영역 추가
- `Services/AppSettings.cs` — `Ai` 하위 객체(대상 언어, 모델명, 활성화 여부) 추가
- `Services/HotkeyManager.cs` — 신규 단축키 등록

## 5. 데이터 흐름

### 5.1 시나리오 A: `Ctrl+Shift+L` (영역 → OCR → 번역 한 큐)

```
사용자: Ctrl+Shift+L
    → HotkeyManager → MainWindow.RunTextCaptureAndTranslate()
    → RegionCapture (기존) → Bitmap
    → OcrService.RecognizeAsync(bitmap) → "원문 텍스트"
    → [키 없음 체크] → 없으면 토스트 + 설정창 자동 열기 → 종료
    → TranslationService.TranslateAsync(text, targetLang)
        → DpapiCredentialStore.AcquireKey() (SecureString using-block)
        → OpenAiClient.PostChatAsync (TLS 1.2+, api.openai.com 화이트리스트)
        → 응답 파싱 → 번역문 반환
        → using 종료 → ZeroFreeBSTR로 키 메모리 0-fill
    → [같은 언어 감지] → "이미 {언어}입니다" 토스트 → 종료
    → 클립보드.SetText(번역문) + 토스트 (원문 미리보기 + 번역문)
```

### 5.2 시나리오 B: 편집기 OCR 패널의 "🌐 번역" 버튼

```
편집기 → OCR 버튼 → OcrService → 패널에 원문 표시 (기존 v1.1.0 흐름)
    → 사용자: "🌐 번역" 버튼 클릭
    → EditorWindow.TranslateOcrText()
    → (5.1과 동일한 TranslationService 호출 경로)
    → 패널 하단 "번역" 영역에 번역문 추가 (원문/번역문 둘 다 보임)
    → 복사 버튼 2개: "원문 복사", "번역문 복사"
```

### 5.3 키 메모리 라이프사이클

```csharp
using (var keyHandle = credentialStore.AcquireKey()) {  // SecureString 래퍼
    var response = await openAiClient.PostChatAsync(messages, model, keyHandle);
    // PostChatAsync 내부에서만 짧게 평문 변환, HttpClient 헤더 설정 후 즉시 폐기
}  // using 종료 → Marshal.ZeroFreeBSTR로 메모리 0-fill
```

키는 `TranslationService` 외부로 절대 새지 않고, 한 번의 HTTP 요청 라이프타임 안에서만 평문으로 존재한다.

### 5.4 대상 언어 결정 우선순위

1. 편집기 패널 드롭다운에서 임시 변경했으면 그 값
2. 아니면 `AppSettings.Ai.TargetLanguage` (기본 "ko")
3. 시스템 로케일 (fallback)

## 6. 보안 설계

| 항목 | 구현 |
|---|---|
| 디스크 암호화 | DPAPI/CurrentUser scope, `apikey.dat` (settings.json과 분리) |
| 메모리 보호 | `SecureString` + 사용 후 `Marshal.ZeroFreeBSTR` |
| 네트워크 | `HttpClient`에 `SslProtocols.Tls12 \| Tls13` 강제, `api.openai.com`만 화이트리스트 |
| 인증서 | OS 기본 검증 (root CA), 핀닝 안 함 (overkill) |
| 로깅 | 모든 로그/예외 출력에 키 마스킹 (`sk-***...***xyz`) |
| 프로세스 노출 | 환경 변수/명령줄 인자에 절대 키 안 담음 |
| 파일 권한 | `apikey.dat`은 사용자 프로필 폴더, OS 기본 ACL (다른 사용자 접근 불가) |
| 평문 잔재 | UI 입력 후 즉시 `SecureString` 전환, 평문 변수 보관 금지 |
| 유출 시 대응 | 설정창 "키 삭제" 1클릭 → 파일 삭제 + 메모리 폐기 |
| 사용자 고지 | 설정창 작은 글씨로 "동일 사용자 권한 멀웨어가 있다면 어떤 OS 암호화도 우회될 수 있음" 명시 |

**대안 검토 (모두 기각):**

- **Credential Manager**: DPAPI 기반이라 보안 동일, 다른 앱이 자격증명 목록에서 볼 수 있어 노출면↑
- **TPM 하드웨어 바인딩**: 가장 강하지만 복잡도/지원 PC 제약 큼, BYOK 도구 수준에선 과함
- **클라우드 키 보관 (우리 서버)**: BYOK 원칙 위배, 우리 서버가 표적이 됨

## 7. 에러 처리

| 상황 | HTTP 코드 | 처리 |
|---|---|---|
| 키 없음 (로컬 체크) | — | 토스트 "AI 키 필요" + 설정창 자동 열기 |
| 키 무효 | 401 | 토스트 "키가 유효하지 않습니다" + 설정창 |
| 한도 초과 | 429 | 토스트 "OpenAI 사용 한도 확인" (`Retry-After` 헤더 있으면 표시) |
| 모델 없음/오타 | 404 | 토스트 "모델명 오류, 설정 확인" + 설정창 |
| 인터넷 끊김 | `HttpRequestException` | 토스트 "네트워크 연결 확인" |
| 타임아웃 (15초) | `TaskCanceledException` | 토스트 "응답 지연, 다시 시도" |
| 빈 OCR 텍스트 | — | 번역 호출 안 함 (조용히 종료) |
| 같은 언어 감지 | — | 번역 스킵 + 토스트 "이미 {언어}입니다" |
| 응답 파싱 실패 | — | 토스트 "예상치 못한 응답" + 디버그 로그(키 마스킹) |
| OpenAI 5xx | 5xx | 토스트 "OpenAI 일시 장애, 다시 시도" |

**재시도 정책:**

- **재시도 안 함** (기본). 사용자가 단축키 다시 누르면 됨.
- 자동 재시도는 비용 폭주 위험 → 명시적 거부.
- 단, 429 응답에 `Retry-After`가 있으면 토스트에 그 시간을 안내.

**같은 언어 감지 방식:**

- OpenAI 시스템 프롬프트에 "원문이 {targetLang}이면 그대로 반환하라"는 지시 포함.
- 응답 == 입력이면 스킵 분기 진입.
- 비용은 1회 발생(불가피). 대신 클립보드를 변경하지 않아 사용자 흐름 방해 없음.

## 8. UI 변경

### 8.1 설정 > 신규 "AI" 탭

레이아웃:

```
┌─ AI 기능 ─────────────────────────────────────────┐
│  ☑ AI 기능 활성화                                  │
│                                                    │
│  ─ 제공자: OpenAI ─────────────────────────────    │
│  API 키:  [sk-***...***abc      ] [👁] [검증] [삭제] │
│           ✓ 키 유효 (마지막 검증: 방금 전)           │
│  → OpenAI 키 발급받기 (platform.openai.com/api-keys) │
│                                                    │
│  모델:    [gpt-4o-mini          ▼] (콤보박스, 자유입력 가능) │
│           예상 비용: 1회 ≈ $0.0001 (1만 회 ≈ $1)    │
│                                                    │
│  ─ 번역 ──────────────────────────────────────    │
│  대상 언어: [한국어 ▼]                             │
│                                                    │
│  ─ 보안 안내 ─────────────────────────────────    │
│  키는 이 PC의 Windows 사용자 계정에만 암호화 저장됩니다.│
│  파일을 다른 PC로 복사해도 사용할 수 없습니다.        │
│  ⚠ 같은 사용자 권한으로 실행되는 멀웨어가 있다면      │
│    어떤 OS 암호화도 우회될 수 있습니다.              │
└────────────────────────────────────────────────────┘
```

**상세 동작:**

- **`[👁]` 토글**: 평문 보기 (3초 후 자동 마스킹 복귀)
- **`[검증]` 버튼**: `GET /v1/models` 호출(비용 발생 없음, 인증만 확인) → ✓/✗ 표시 + 마지막 검증 시각 저장
- **`[삭제]` 버튼**: 확인 다이얼로그 → `apikey.dat` 삭제 + 메모리 0-fill + AI 활성화 토글 자동 OFF
- **AI 기능 활성화 OFF**: 단축키/버튼 다 비활성화(회색), 키 파일은 유지 (재활성화 시 그대로)

**지원 대상 언어:** 한국어, 영어, 일본어, 중국어 (간체) 4개 (v1.2.0 범위).

### 8.2 메인 윈도우 토스트

- `Ctrl+Shift+T` (기존 OCR): 토스트 변경 없음 — "OCR 결과를 클립보드에 복사했습니다"
- `Ctrl+Shift+L` (신규 OCR+번역): 새 토스트 — "번역 복사됨 (원문 → 번역문 미리보기)"
- **번역 버튼은 토스트에 넣지 않음** — `NotifyIcon.ShowBalloonTip` Windows 한계로 풍선 안에 버튼 삽입 불가. 번역은 신규 단축키 또는 편집기 패널 버튼으로만 트리거.

### 8.3 편집기 OCR 패널

- 기존: 원문 텍스트 + "복사" 버튼
- 변경: 추가로 "🌐 번역" 버튼 → 클릭 시 패널 하단에 "번역" 섹션 펼침
- 번역 섹션: 번역문 + 대상 언어 드롭다운(한/영/일/중 4개) + "번역문 복사" 버튼

### 8.4 단축키 탭

- 기존 8개 단축키 목록에 "텍스트 추출+번역 (`Ctrl+Shift+L`)" 1줄 추가
- 사용자 커스터마이징 가능 (기존 패턴 따라)

### 8.5 홈페이지 (v1.2.0 범위 외)

홈페이지 변경은 v1.2.0에서 제외. 코드/설치파일 검증 후 별도로 결정.

## 9. 테스트 전략

xUnit, 기존 29개 + 신규 추가.

| 대상 | 테스트 방식 | 케이스 |
|---|---|---|
| `TranslationService` | `IOpenAiClient` 모킹 | 정상 응답, 빈 텍스트(호출 안 함), 같은 언어 감지, 401/429/5xx, 타임아웃, 응답 파싱 실패 |
| `OpenAiClient` | `HttpMessageHandler` 모킹 | 헤더에 키 마스킹 검증, 도메인 화이트리스트, TLS 강제, 타임아웃, 재시도 안 함 검증 |
| `DpapiCredentialStore` | 실제 DPAPI 사용 (Windows 환경) | 저장→로드 라운드트립, 파일 없음 시 null. 다른 사용자 시뮬레이션은 환경 의존이라 조건부 스킵 |
| `IAiCredentialStore` | 인메모리 fake | 키 라이프사이클(SecureString 0-fill 검증), 미설정 상태 |
| 키 마스킹 미들웨어 | 단위 테스트 | 다양한 sk- 패턴, 짧은 키, 토큰 형태 변경 대응 |
| 같은 언어 감지 | 단위 테스트 | 번역 응답 == 원문일 때 스킵 분기 |
| 통합 테스트 | OpenAI 실제 호출 | CI 기본 비활성, 환경 변수에 `OPENAI_API_KEY` 있을 때만 조건부 실행 |

## 10. v1.2.0 범위

### 10.1 포함

- ✅ `Services/Ai/*` 모듈 (인프라)
- ✅ DPAPI 키 저장/로드/삭제/검증
- ✅ OCR 결과 → OpenAI 번역
- ✅ `Ctrl+Shift+L` 새 단축키 (영역 → OCR → 번역)
- ✅ OCR 토스트/편집기 패널에 번역 버튼
- ✅ 설정 > AI 탭 신설
- ✅ 한국어/영어/일본어/중국어(간체) 4개 언어 지원

### 10.2 의도적 제외 (YAGNI)

- ❌ 다중 제공자 (DeepL, Papago 등) → v1.3.0 이후
- ❌ AI 캡쳐 분석 (이미지 → GPT 설명) → v1.3.0
- ❌ 편집기 AI (배경 제거 등) → v1.4.0
- ❌ 사용자별 프롬프트 커스터마이징
- ❌ 번역 히스토리 영구 저장
- ❌ 오프라인 번역 모델
- ❌ 키 동기화/클라우드 백업 (보안 원칙 위배)
- ❌ **홈페이지 변경** (v1.2.0 범위 외, 코드 검증 후 별도 결정)

### 10.3 호환성

- TargetFramework 변경 없음 (`net8.0-windows10.0.19041.0` 그대로)
- `apikey.dat`이 없으면 AI 기능 비활성, 기존 OCR 흐름 그대로 동작
- v1.1.0 사용자 → v1.2.0 업데이트 시 추가 작업 없음 (키 입력은 본인이 원할 때)

## 11. 후속 버전 확장 시 인프라 활용

| 버전 | 추가 기능 | `Services/Ai`에 추가될 것 |
|---|---|---|
| v1.3.0 | 이미지 → GPT 설명 | `OpenAiClient.PostVisionAsync`, `ImageAnalysisService` |
| v1.4.0 | 배경 제거 등 | `OpenAiClient.PostImageAsync` (또는 별도 서비스), `ImageEditService` |

`IAiCredentialStore`, `OpenAiClient`의 기본 구조는 그대로 유지. 키 1개로 모든 기능 동작.

## 12. 릴리즈 / 배포 메모

- v1.1.0과 동일한 GitHub Actions 자동 빌드 흐름 사용 (`v*` 태그 푸시 → 빌드 → Release)
- 코드 서명: SignPath Foundation 승인 후 활성화 (이 spec 범위 외)
- **로컬 테스트 검증 후 push** — spec 작성 시점엔 push 보류
