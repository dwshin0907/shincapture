# 신캡쳐 기본 릴리즈 파이프라인

이 프로젝트의 정식 릴리즈는 아래 순서를 기본값으로 사용한다.

## 원칙

- 기존 설치 파일과 Git 태그는 절대 덮어쓰지 않는다.
- 새 기능이 들어가면 `src/ShinCapture/ShinCapture.csproj`과 `installer/setup.iss`의 버전을 함께 올린다.
- `dist/ShinCapture_Setup_v<version>.exe`, `release/ShinCapture_Setup_v<version>.exe`, `v<version>` 태그 중 하나라도 이미 있으면 중단하고 다음 버전을 사용한다.
- 소스, GitHub Release, 홈페이지 다운로드 링크가 같은 버전을 가리킨 뒤 배포를 완료한다.

## SignPath 준비 상태 (2026-09-18)

- 계정 구독은 `Free trial subscription`이며 인증서는 `ShinCapture Test Cert`만 있다.
- `release-signing` 정책의 실제 용도도 `Test signing`이다. 이름만 보고 정식 인증서로 판단하지 않는다.
- 기존 GitHub API 토큰으로 실행한 시험 빌드에서 `Could not authorize against SignPath API`가 발생했다.
- 정식 무료 서명은 [SignPath Foundation 신청 및 승인](https://signpath.org/apply)이 필요하다.
- 2026-09-18 신청서를 제출했고 `Form submitted` 접수 완료 화면을 확인했다. 현재 재단 심사 대기이며 인증서 발급 완료가 아니다.
- 정식 서명 릴리즈는 승인과 인증서 연결 후 진행한다. 승인 전 공개 배포는 아래 v1.3.18 예외에 한정한다.
- [Code signing policy](CODE_SIGNING.md)와 [개인정보 처리 안내](PRIVACY.md)를 함께 제공한다.

## v1.3.18 미서명 배포 결정 (2026-09-20)

- 유지관리자가 승인 대기 상태를 확인한 뒤 "클라우드플레어 및 홈페이지에 배포해", "서명승인나면 재배포하면 됨"으로 이번 공개 배포를 지시했다. 이 명시적 지시는 기존의 승인 전 배포 보류를 v1.3.18에 한해 변경한다.
- 검증된 로컬 빌드의 미서명 설치 파일, 같은 설치 파일을 담은 ZIP, `SHA256SUMS.txt`를 수동으로 GitHub Release에 게시한다. 홈페이지와 릴리즈 노트에 미서명 및 Foundation 승인 대기 상태를 표시한다.
- 기존 서명 CI는 그대로 유지한다. 이번 수동 배포는 서명 실패 시 자동으로 미서명 파일을 배포하는 경로를 추가하지 않는다.
- 승인 후에는 앱과 설치 파일을 모두 서명·검증하여 새 버전과 새 태그로 배포한다. v1.3.18을 포함한 기존 파일과 태그는 덮어쓰지 않는다.
- 이 예외는 향후 버전의 미서명 배포를 자동으로 허용하지 않는다.

JEV에 사용자 확정 지시·기존 조항·수정 후보를 전달해 범위와 충돌을 실제 검토했다.
응답은 예외 범위를 v1.3.18로 분류했으나 기존 금지 조항과의 충돌 판단은 혼재했다.
최신 사용자 지시를 근거로 이번 예외의 우선관계를 명시하고, 기존 파일 보존과 정식 서명 CI 검증은 유지했다.
JEV의 확률을 실행 권한으로 해석하지 않았다.

## 최초 연결

1. Foundation 승인을 받아 정식 인증서를 제공받고, 수동 승인이 필요한 배포 정책을 연결한다.
2. 프로젝트에 다음 아티팩트 설정을 등록한다. GitHub upload-artifact가 ZIP으로 포장하므로 루트는 `zip-file`이다.
   - `installer/signpath-application.xml`: 앱 실행 파일, 권장 slug `application`.
   - `installer/signpath-installer.xml`: 설치 파일, 권장 slug `installer`.
3. GitHub Actions Variables에 `SIGNING_ENABLED=true`, `SIGNPATH_ORG_ID`, `SIGNPATH_PROJECT_SLUG`,
   `SIGNPATH_SIGNING_POLICY`, `SIGNPATH_APP_ARTIFACT_CONFIG`, `SIGNPATH_ARTIFACT_CONFIG`를 설정한다.
   두 아티팩트 변수는 각각 실제 등록한 앱/설치 파일 설정의 slug를 사용한다.
4. 해당 정책의 Submitter 역할을 가진 CI 사용자 토큰을 `SIGNPATH_API_TOKEN` Secret에 저장한다.
   토큰은 코드, 명령 기록, 로그, 문서에 기록하지 않는다. 기존 토큰이 거부되면 계정/권한을 확인한 뒤 교체한다.
5. GitHub Actions `Release`를 수동 실행하여 앱·설치 파일 서명과 검증을 시험한다.
   수동 실행은 공개 Release나 태그를 만들지 않으며 `signed-release-<version>` 아티팩트를 남긴다.

현재 남은 외부 작업은 Foundation 심사·승인, 정식 인증서/정책 연결, 앱·설치 파일 아티팩트 설정 등록이다.
`SIGNPATH_APP_ARTIFACT_CONFIG`는 아직 설정하지 않았다. 승인 후 실제 등록한 slug를 연결한다.
이전 인증 오류는 기존 v1 연동으로 확인했으며, 새 v3 연동의 실제 서명은 아직 실행하지 않았다.
승인 후 최신 연동으로 인증 상태를 다시 확인하고 필요한 경우에만 CI 사용자 토큰을 갱신한다.

## 정식 서명 릴리즈 순서

1. `dotnet test ShinCapture.sln -c Release --no-restore`
2. `pwsh -NoProfile -File tests/ReleaseSigning.Tests.ps1`
3. 프로젝트와 설치 설정의 버전을 함께 올리고, 같은 버전의 로컬 파일·태그·Release가 없는지 확인
4. `master`를 `origin`에 푸시
5. 새 `v<version>` 태그를 푸시해 `.github/workflows/release.yml` 실행
6. CI에서 앱 빌드 → 앱 서명·검증 → Inno Setup 패키징 → 설치 파일 서명·검증 → ZIP·SHA256SUMS 생성
7. SignPath가 요청하는 앱·설치 파일 서명 승인을 처리하고, CI의 서명 검증 성공을 확인
8. GitHub Release의 설치 파일·ZIP·SHA256SUMS를 확인하고, 내려받은 파일의 서명과 해시를 재검증
9. 홈페이지 저장소 `C:\AI\NPC\homepage\ai-landing-page`의 `public/shincapture/index.html` 버전과 다운로드 링크 갱신
10. 홈페이지 `npm run build` 후 `main` 푸시
11. 홈페이지의 `deploy-cloudflare-worker.yml`이 `naverlandingpage`를 배포하는지 확인
12. 실제 홈페이지와 다운로드 URL을 확인

Cloudflare 인증값은 홈페이지 저장소의 GitHub Actions Secrets에서만 사용한다. 토큰을 소스나 문서에 저장하지 않는다.

`scripts/Assert-ReleaseSignature.ps1`는 Windows의 `Valid` 상태, 내장 Authenticode 서명,
타임스탬프가 모두 있는 파일만 허용한다. 테스트 인증서를 신뢰 저장소에 설치해서 검사를 통과시키지 않는다.
정식 서명 CI에서 서명 실패 시 무서명 파일을 대신 배포하지 않는다. Inno Setup 제거 프로그램은 현재 별도 서명 대상에 포함되지 않는다.
