# Privacy policy / 개인정보 처리 안내

Updated: 2026-09-18. This describes the current ShinCapture source code and is
provided for users and code-signing reviewers.

## Captures, OCR, and local files

Screen capture, image annotation, and Windows OCR run on the user's computer.
Capture images, saved images, clipboard export files, application settings, and
diagnostic logs may be stored locally. Images are not automatically uploaded to
a ShinCapture server. Files that the user copies, drags, or pastes into another
application are handled by that destination application.

캡처·편집·Windows OCR은 PC에서 처리합니다. 이미지, 설정, 임시 파일과
진단 로그는 로컬에 저장될 수 있습니다. 다른 프로그램에 복사하거나
붙여넣은 자료는 해당 프로그램의 처리 방식을 따릅니다.

## Automatic update checks

At startup and every six hours while running, ShinCapture requests the latest
public release metadata from
`https://api.github.com/repos/dwshin0907/shincapture/releases/latest`.
The request includes a `ShinCapture/1.0` User-Agent header. GitHub receives the
network request, including ordinary connection information such as the client's
IP address. Capture images, OCR text, and API keys are not included in update
checks. The installed version is compared with the returned release locally.
The current application does not expose an update-check opt-out setting.

앱 시작 시와 실행 중 6시간마다 GitHub에 새 버전 정보를 요청합니다.
이 요청에는 캡처 이미지, OCR 텍스트, API 키가 포함되지 않습니다.
현재 버전에는 업데이트 확인을 끄는 설정이 없습니다.

## Optional AI translation

When the user configures an OpenAI API key and requests AI translation, the
source text, translation instructions, target language, and selected model are
sent directly to `api.openai.com`. The user's API key is sent to OpenAI for
authentication. Key validation also contacts the OpenAI API. The translation
implementation sends text, not the captured image. Avoid including information
that you do not want to send to the API provider. Standard capture and local OCR
do not require configuring or using AI translation.

API keys saved by the app are protected using Windows DPAPI for the current
Windows user. This does not protect against every form of local account compromise.

AI 번역을 설정하고 실행하면 번역할 텍스트와 요청 정보가 OpenAI로 직접
전송됩니다. API 키는 인증에 사용되며, 로컬 저장 시 현재 Windows 사용자
기준의 DPAPI로 보호합니다. 일반 캡처와 로컬 OCR에는 AI 번역이 필요하지 않습니다.

## External services and contact

Service providers apply their own policies to requests they receive:

- [GitHub privacy statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement)
- [OpenAI privacy policy](https://openai.com/policies/privacy-policy/)

Questions or corrections can be raised through
[the project repository](https://github.com/dwshin0907/shincapture/issues).
Do not include private capture content or API keys in a public issue.
