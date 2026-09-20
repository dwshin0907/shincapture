# v1.3.19 공개 배포 기록

2026-09-20, 유지관리자의 "공개배포하자" 지시에 따라 검증한 미서명 후보를 공개했다.

- [GitHub Release](https://github.com/dwshin0907/shincapture/releases/tag/v1.3.19): 23:35:30 KST 공개, Latest 지정, 초안·사전 릴리즈 아님.
- 릴리즈 태그의 소스 커밋: `ab450978122f355f753348bc0561a95aa7220383`. 앱 기능 변경은 `8e25c8f`, 이후 변경은 문서만 해당한다.
- [공식 홈페이지](https://wenvidia.com/shincapture/): 버전·EXE·ZIP·체크섬 링크와 변경사항 배너를 1.3.19로 갱신.
- 홈페이지 커밋: `11bbe965ce1a7747585767e8b5d49f29b7cf6cc9`.
- [Cloudflare 배포 실행](https://github.com/dwshin0907/wenvidia-homepage/actions/runs/35517096032): 성공, 23:38:47 KST `naverlandingpage` 배포 완료.
- Cloudflare Version ID: `c00f198f-a39f-45f5-9a09-1a6f35d6152d`.

## 공개 파일 검증

| 파일 | 크기 | SHA-256 |
| --- | ---: | --- |
| ShinCapture_Setup_v1.3.19.exe | 97,341,246 B | `f8fa35a7fa39f8b60ac98ecf07ed75389a297e2fe8a3f74d3a5f6bf29bfa474c` |
| ShinCapture_v1.3.19.zip | 96,809,618 B | `ef529ba8b9410b726249e38313b5a313f4dd5e314895a6eb4e125c2214c65283` |

설치 파일·ZIP·SHA256SUMS.txt를 인증 없는 공개 다운로드 URL에서 다시 내려받아 로컬 원본과 해시가 모두 일치하는 것을 확인했다. ZIP에는 동일 설치 파일 한 개만 들어 있으며 내부 파일 해시도 일치한다. 내려받은 설치 파일의 Authenticode 상태는 `NotSigned`다.

## 검증과 서명 상태

- 앱 검증: [성능 점검 보고서](PERFORMANCE_AUDIT_v1.3.19.md)의 테스트 330개와 배포 EXE 검증 결과 적용. 검증한 설치 파일을 재빌드하지 않고 그대로 배포했다.
- 서명 검증기 회귀 검사: `tests/ReleaseSigning.Tests.ps1`, 10개 통과.
- 홈페이지: `npm run lint`, `npm run build` 성공. 기존 App.tsx의 useEffect 의존성 경고 1개가 남아 있다.
- 모바일·태블릿·데스크톱 화면 확인, 가로 넘침 없음. 실제 홈페이지 HTTP 200, 1.3.19 표시와 모든 다운로드 URL 확인. 브라우저 콘솔 오류 없음.
- 홈페이지 작업 폴더의 `artifacts/design-qa/shincapture-v1.3.19/`에 전후 및 공개 화면 보존.
- [태그로 실행된 정식 서명 CI](https://github.com/dwshin0907/shincapture/actions/runs/35516834324)는 기존 `SIGNPATH_APP_ARTIFACT_CONFIG` 미설정 검사에서 중단됐다. 서명 CI는 변경하지 않았으며, 이번 공개 파일은 명시적으로 승인된 로컬 미서명 수동 배포본이다.
- 미서명·Foundation 승인 대기 상태를 릴리즈 노트와 홈페이지에 표시했다. 적용 결정은 [릴리즈 파이프라인](RELEASE_PIPELINE.md)과 [Code signing policy](CODE_SIGNING.md)에 기록했다.
