# v1.3.20 공개 배포 기록

2026-11-27, 유지관리자의 "릴리즈하고 홈페이지 업데이트도 해야지", "해줘" 지시에 따라 검증한 미서명 설치 파일을 공개했다. 배포 결정은 [릴리즈 파이프라인](RELEASE_PIPELINE.md)의 v1.3.20 항목에 있다.

- [GitHub Release](https://github.com/dwshin0907/shincapture/releases/tag/v1.3.20): 16:07:09 KST 공개, Latest 지정, 초안·사전 릴리즈 아님.
- 릴리즈 태그의 소스 커밋: `b2a5f9c`. 앱 기능 변경은 `420e19a`, 버전 변경은 `87642c3`, 이후 변경은 문서만 해당한다.
- 변경 기능: [캡처 후 Ctrl+V와 카카오톡 자동 전환](CLIPBOARD_PASTE.md).
- [공식 홈페이지](https://wenvidia.com/shincapture/): `src/site.ts`의 버전·EXE·릴리스·체크섬 링크, 용량, 검색 설명, 페이지 갱신일을 1.3.20으로 바꾸고 카카오톡 붙여넣기 FAQ를 추가했다. 캡처 가이드의 기본 설정 기준 문구도 1.3.20으로 맞췄다(v1.3.20은 설정·단축키 코드 변경 없음).
- 홈페이지 커밋: `036108a701b0ceb7c998def14e2a83345a905421`.
- [Cloudflare 배포 실행](https://github.com/dwshin0907/wenvidia-homepage/actions/runs/45956774011): 성공, 16:13:04 KST `naverlandingpage` 배포 완료.
- Cloudflare Version ID: `1109427e-0675-4724-a4e7-9aecb03907f4`.

## 결정 기록

- D1. 재빌드하지 않고 2026-10-18 설치 파일을 그대로 게시했다. 이유: 유지관리자 PC에 설치된 EXE와 SHA-256이 같고 빌드 이후 소스 변경이 없어, 재빌드하면 오히려 실제 설치·실행한 파일과 달라진다. 비용: 빌드 시점의 로컬 환경에 의존한다. 탈출구: 문제가 생기면 새 버전으로 CI 또는 로컬 재빌드한다.
- D2. 태그 CI 실패는 그대로 두었다. 이유: 정식 서명 설정 검사가 의도대로 막은 것이며, 서명 CI를 약화하지 않는다는 정책을 따른다.
- D3. 홈페이지는 로컬 `main`(v1.3.12 시점) 대신 최신 `origin/main`에서 만든 `release/shincapture-v1.3.20` worktree로 수정했다.

## 공개 파일 검증

| 파일 | 크기 | SHA-256 |
| --- | ---: | --- |
| ShinCapture_Setup_v1.3.20.exe | 97,471,664 B | `188cf46cccf4698e181fa25564f8597d90960b0dd23e47d99803f6c416e7cdac` |
| ShinCapture_v1.3.20.zip | 96,922,463 B | `a813ed4ac9cecedd5710ca33401dfcaeb6d9c14363d3e2625e4e33631404784e` |

설치 파일·ZIP·SHA256SUMS.txt를 인증 없는 공개 다운로드 URL에서 다시 내려받아 해시가 모두 일치하는 것을 확인했다. ZIP에는 같은 설치 파일 한 개만 들어 있으며 내부 파일 해시도 일치한다. 설치 파일의 제품 버전은 1.3.20, Authenticode 상태는 `NotSigned`다.

## 검증과 서명 상태

- 앱 검증: 2026-11-27 같은 소스로 Release 전체 테스트 352개 통과. 배포 EXE 검증은 [로컬 재설치 기록](LOCAL_INSTALL_v1.3.20.md)을 적용했다.
- 서명 검증기 회귀 검사: `tests/ReleaseSigning.Tests.ps1`, 10개 통과.
- 홈페이지: `npm run lint`, `npm run build` 성공, `python scripts/verify-site.py` 966건 통과. 실제 `/`, `/shincapture/`, `/downloads/`, 캡처 가이드에서 HTTP 200과 1.3.20 표시, 1.3.19 문구 없음, EXE·체크섬·릴리스 URL 200을 확인했다. 변경이 문구·링크뿐이라 브라우저 화면 캡처 비교는 하지 않았다.
- [태그로 실행된 정식 서명 CI](https://github.com/dwshin0907/shincapture/actions/runs/45956248122)는 `Require production signing configuration` 단계에서 중단됐다. 서명 CI는 변경하지 않았으며, 이번 공개 파일은 명시적으로 승인된 로컬 미서명 수동 배포본이다.
- 카카오톡 입력창과 각 AI 터미널에서의 실제 붙여넣기 화면은 자동 검증 범위 밖이다.
