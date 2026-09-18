# 신캡쳐 (ShinCapture)

Free, MIT-licensed screen capture and image annotation software for Windows.

신캡쳐는 영역·창·스크롤 캡처, 이미지 편집, 모자이크, 화살표, 워터마크,
Windows OCR과 선택적 AI 번역을 제공하는 Windows 데스크톱 프로그램입니다.

- [Download / 다운로드](https://github.com/dwshin0907/shincapture/releases)
- [Code signing policy](docs/CODE_SIGNING.md)
- [Privacy policy / 개인정보 처리 안내](docs/PRIVACY.md)
- [Build and release instructions](docs/RELEASE_PIPELINE.md)

## Code signing policy

SignPath Foundation production signing is being prepared. The current account only
has a test certificate; existing public releases are unsigned. A test certificate
does not remove Windows trust warnings. See the [code signing policy](docs/CODE_SIGNING.md)
for the intended provider, maintainer roles, and release verification requirements.

## Development

Requires Windows and the .NET 8 SDK.

```powershell
dotnet restore ShinCapture.sln
dotnet test ShinCapture.sln -c Release --no-restore
dotnet run --project src/ShinCapture/ShinCapture.csproj
```

Source code is distributed under the [MIT License](LICENSE).
