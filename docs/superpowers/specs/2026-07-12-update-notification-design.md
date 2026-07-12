# 설치 교체 및 GitHub 업데이트 알림 설계

## 목표

재설치 시작 시 실행 중인 신캡쳐 트레이 프로세스를 종료해 파일 교체를 보장하고, 실행 중인 사용자가 새 정식 GitHub Release를 알 수 있게 한다.

## 결정

- 설치 시작 때 레지스트리 등록 여부와 무관하게 `ShinCapture.exe`를 강제 종료한다. 그 다음 기존 설치가 있으면 현재의 무인 제거 과정을 실행한다.
- 앱은 시작 후 UI를 막지 않는 비동기 작업으로 `dwshin0907/shincapture`의 최신 Release를 조회하고, 트레이에 계속 실행 중인 사용자도 알 수 있도록 6시간마다 다시 확인한다.
- GitHub API의 `tag_name`에서 `v` 접두사를 제거해 현재 어셈블리 버전과 비교한다. 더 큰 정식 버전일 때만 Windows 트레이 풍선 알림을 표시한다.
- 알림을 클릭하면 해당 Release 페이지를 기본 브라우저로 연다. 같은 버전은 한 세션에 한 번만 알리고, 네트워크·파싱 오류, 초안·사전 릴리즈, 현재 버전 이하 응답은 사용자에게 메시지를 표시하지 않는다.
- API 호출에는 `User-Agent: ShinCapture`와 권장 JSON Accept 헤더를 넣는다. 공개 Release 조회에는 토큰을 사용하지 않는다.

## 구성

| 파일 | 책임 |
| --- | --- |
| `Services/GitHubReleaseUpdateService.cs` | 최신 Release JSON 조회와 버전 비교. UI에 독립적이라 단위 테스트 가능. |
| `Views/MainWindow.xaml.cs` | 앱 로드 후 서비스 실행, 풍선 알림 표시, 클릭 시 Release URL 열기. |
| `installer/setup.iss` | 설치 시작 즉시 실행 중 프로세스 종료. |
| `tests/.../GitHubReleaseUpdateServiceTests.cs` | 버전 비교·태그 파싱·신규 릴리즈 판단 회귀 테스트. |

## 오류와 안전성

- HTTP 요청은 5초 제한으로 실행하고 실패는 `null` 결과로 처리한다.
- 정식 Release의 `tag_name`이 SemVer로 해석되지 않으면 알리지 않는다.
- 다운로드·설치 자동 실행은 하지 않는다. 사용자가 풍선 알림을 눌렀을 때만 Release 페이지를 연다.
- 프로세스 종료는 설치 프로세스에 한정하며, 종료 실패도 기존 제거 흐름을 막지 않는다.

## 검증

- 새 버전/같은 버전/낮은 버전/잘못된 태그를 단위 테스트한다.
- 전체 테스트, 단일 파일 publish, Inno Setup 컴파일을 실행한다.
- 설치 파일의 버전과 새 서비스가 포함된 publish 산출물을 확인한다.
