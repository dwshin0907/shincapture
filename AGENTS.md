# Repository Guidelines

## Project Structure & Module Organization

`ShinCapture.sln` contains the Windows desktop application and its tests. Production code lives in `src/ShinCapture/`: capture logic is under `Capture/`, editor tools and objects under `Editor/`, reusable services under `Services/`, models under `Models/`, and WPF windows and controls under `Views/`. Theme resources and application artwork are in `Themes/` and `Assets/`. Tests mirror these areas in `tests/ShinCapture.Tests/`. Installer configuration is in `installer/setup.iss`; release automation is in `.github/workflows/release.yml`. Design notes and release procedures belong in `docs/`.

## Build, Test, and Development Commands

- `dotnet restore ShinCapture.sln` — restore NuGet dependencies.
- `dotnet build ShinCapture.sln -c Release` — compile the WPF application and tests.
- `dotnet test ShinCapture.sln -c Release --no-restore` — run the complete xUnit suite.
- `dotnet run --project src/ShinCapture/ShinCapture.csproj` — launch a development build on Windows.
- `dotnet publish src/ShinCapture/ShinCapture.csproj -c Release -r win-x64 --self-contained -o publish` — create the distributable application.

Use `build.bat` only on machines matching its local SDK path. Follow `docs/RELEASE_PIPELINE.md` for installers, tags, and website deployment.

## Coding Style & Naming Conventions

Use four-space indentation in C# and preserve the existing XAML formatting. Nullable reference types are enabled. Use `PascalCase` for types, methods, properties, and named XAML controls; use `camelCase` for locals and parameters; prefix private fields with `_`. Keep UI event handlers descriptive, for example `OnHistorySaveAll`. Prefer focused classes in the existing feature folders over expanding window code unnecessarily.

## Testing Guidelines

Tests use xUnit. Name files `<Subject>Tests.cs` and test methods by observable behavior, such as `DefinesStableNaverPremiumContentMetadata`. Add regression tests for behavior changes and run focused tests first with `--filter FullyQualifiedName~TypeName`, followed by the full suite. UI changes also require a Release build and screenshots at representative compact and wide window sizes.

## Commit & Pull Request Guidelines

Follow the repository’s conventional prefixes: `feat:`, `fix:`, `docs:`, and `chore:`. Keep commits small and intentional. Pull requests should describe user-visible behavior, list verification commands, link relevant issues, and include before/after screenshots for WPF UI changes. Do not include unrelated local or generated files.

## Security & Release Safety

Never commit API keys, Cloudflare tokens, or user settings. Do not overwrite existing installers or Git tags; bump versions in both the project file and `setup.iss`, then create a newly versioned artifact.
