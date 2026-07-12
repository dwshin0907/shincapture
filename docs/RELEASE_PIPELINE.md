# 신캡쳐 기본 릴리즈 파이프라인

이 프로젝트의 정식 릴리즈는 아래 순서를 기본값으로 사용한다.

## 원칙

- 기존 설치 파일과 Git 태그는 절대 덮어쓰지 않는다.
- 새 기능이 들어가면 `src/ShinCapture/ShinCapture.csproj`과 `installer/setup.iss`의 버전을 함께 올린다.
- `dist/ShinCapture_Setup_v<version>.exe`, `release/ShinCapture_Setup_v<version>.exe`, `v<version>` 태그 중 하나라도 이미 있으면 중단하고 다음 버전을 사용한다.
- 소스, GitHub Release, 홈페이지 다운로드 링크가 같은 버전을 가리킨 뒤 배포를 완료한다.

## 순서

1. `dotnet test ShinCapture.sln -c Release --no-restore`
2. `dotnet publish`로 self-contained 단일 실행 파일 생성
3. Inno Setup으로 새 버전명의 설치 파일 생성
4. `dist`와 `release`의 설치 파일 SHA-256 일치 확인
5. 이전 버전 설치 파일의 SHA-256이 바뀌지 않았는지 확인
6. `master`를 `origin`에 푸시
7. 새 `v<version>` 태그를 푸시해 `.github/workflows/release.yml` 실행
8. GitHub Release의 설치 파일과 ZIP 생성 확인
9. 홈페이지 저장소 `C:\AI\NPC\homepage\ai-landing-page`의 `public/shincapture/index.html` 버전과 다운로드 링크 갱신
10. 홈페이지 `npm run build` 후 `main` 푸시
11. 홈페이지의 `deploy-cloudflare-worker.yml`이 `naverlandingpage`를 배포하는지 확인
12. 실제 홈페이지와 다운로드 URL을 확인

Cloudflare 인증값은 홈페이지 저장소의 GitHub Actions Secrets에서만 사용한다. 토큰을 소스나 문서에 저장하지 않는다.
