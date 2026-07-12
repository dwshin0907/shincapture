# History Card Drag Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make each right-side capture-history card export its fully edited image as a temporary PNG when dragged with the left mouse button.

**Architecture:** A testable `DragExportService` owns atomic PNG creation and cache cleanup. A small pure gesture policy decides click versus drag, while `EditorWindow` composites the selected history image with its saved editor objects and supplies both `FileDrop` and bitmap data to WPF drag-and-drop.

**Tech Stack:** C# 12, .NET 8, WPF, System.Drawing, xUnit

---

### Task 1: Temporary drag-export service

**Files:**
- Create: `src/ShinCapture/Services/DragExportService.cs`
- Create: `tests/ShinCapture.Tests/Services/DragExportServiceTests.cs`

- [ ] **Step 1: Write failing tests for PNG creation and cleanup**

Create tests that instantiate the service with a test directory and small limits. Verify `CreatePng` returns a decodable PNG, leaves no `.tmp` file, and creates unique names. Create files with controlled timestamps and sizes, then verify `Cleanup` removes files older than 24 hours and removes the oldest files until count and byte limits are satisfied.

```csharp
var service = new DragExportService(_tempDir, TimeSpan.FromHours(24), 2, 10);
using var bitmap = new Bitmap(8, 8);
string path = service.CreatePng(bitmap, now);
Assert.True(File.Exists(path));
Assert.Empty(Directory.GetFiles(_tempDir, "*.tmp"));
using var decoded = Image.FromFile(path);
Assert.Equal(8, decoded.Width);
```

- [ ] **Step 2: Run the focused tests and verify the missing-type failure**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj -c Release --filter FullyQualifiedName~DragExportServiceTests`

Expected: FAIL because `DragExportService` does not exist.

- [ ] **Step 3: Implement `DragExportService`**

Use production defaults of `%LOCALAPPDATA%\ShinCapture\Temp\DragDrop`, 24 hours, 100 files, and 250 MiB. `CreatePng(Bitmap, DateTimeOffset?)` must call `Cleanup`, write PNG bytes to `<final>.tmp`, move without overwrite, and delete a leftover temporary file in `finally`. `Cleanup` should consider `ShinCapture_*.png`, ignore individual I/O failures, delete expired files first, and then delete oldest files until both limits are met.

```csharp
public sealed class DragExportService
{
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromHours(24);
    public const int DefaultMaxFiles = 100;
    public const long DefaultMaxBytes = 250L * 1024 * 1024;

    public string CreatePng(Bitmap bitmap, DateTimeOffset? now = null);
    public void Cleanup(DateTimeOffset? now = null);
}
```

- [ ] **Step 4: Run focused tests and verify they pass**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj -c Release --filter FullyQualifiedName~DragExportServiceTests`

Expected: all `DragExportServiceTests` pass.

- [ ] **Step 5: Commit the service**

```powershell
git add src/ShinCapture/Services/DragExportService.cs tests/ShinCapture.Tests/Services/DragExportServiceTests.cs
git commit -m "feat: add temporary drag export service"
```

### Task 2: Click-versus-drag policy

**Files:**
- Create: `src/ShinCapture/Editor/HistoryCardDragPolicy.cs`
- Create: `tests/ShinCapture.Tests/Editor/HistoryCardDragPolicyTests.cs`

- [ ] **Step 1: Write the failing gesture-policy test**

```csharp
[Theory]
[InlineData(3, 3, 4, 4, false)]
[InlineData(4, 0, 4, 4, true)]
[InlineData(0, -5, 4, 4, true)]
public void StartsOnlyAfterSystemDragThreshold(
    double deltaX, double deltaY, double horizontal, double vertical, bool expected)
{
    Assert.Equal(expected,
        HistoryCardDragPolicy.ShouldStart(deltaX, deltaY, horizontal, vertical));
}
```

- [ ] **Step 2: Run the focused test and verify the missing-type failure**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj -c Release --filter FullyQualifiedName~HistoryCardDragPolicyTests`

Expected: FAIL because `HistoryCardDragPolicy` does not exist.

- [ ] **Step 3: Implement the minimal pure policy**

```csharp
public static bool ShouldStart(
    double deltaX, double deltaY, double minimumHorizontal, double minimumVertical) =>
    Math.Abs(deltaX) >= minimumHorizontal || Math.Abs(deltaY) >= minimumVertical;
```

- [ ] **Step 4: Run focused tests and commit**

Expected: all `HistoryCardDragPolicyTests` pass.

```powershell
git add src/ShinCapture/Editor/HistoryCardDragPolicy.cs tests/ShinCapture.Tests/Editor/HistoryCardDragPolicyTests.cs
git commit -m "feat: add history card drag gesture policy"
```

### Task 3: Edited-image compositing and history-card drag

**Files:**
- Modify: `src/ShinCapture/Views/EditorWindow.xaml.cs`

- [ ] **Step 1: Generalize final-image rendering**

Replace the body of `RenderFinalImage` with a call to a shared method:

```csharp
private Bitmap RenderFinalImage() => RenderCompositeImage(_sourceImage, _objects);

private static Bitmap RenderCompositeImage(
    BitmapSource sourceImage,
    IEnumerable<EditorObject> objects)
{
    int width = sourceImage.PixelWidth;
    int height = sourceImage.PixelHeight;
    var visual = new DrawingVisual();
    using (DrawingContext dc = visual.RenderOpen())
    {
        dc.DrawImage(sourceImage, new Rect(0, 0, width, height));
        foreach (EditorObject editorObject in objects.Where(item => item.IsVisible))
            editorObject.RenderWithTransform(dc);
    }

    var target = new RenderTargetBitmap(
        width, height, 96, 96, PixelFormats.Pbgra32);
    target.Render(visual);
    return BitmapHelper.ToBitmap(target);
}
```

- [ ] **Step 2: Add drag state and export service**

Add one `DragExportService` field and card-local mouse state. On left-button down, record the point, capture the mouse, and defer opening the card. On mouse move, call `HistoryCardDragPolicy.ShouldStart` with `SystemParameters.MinimumHorizontalDragDistance` and `MinimumVerticalDragDistance`. On button up before the threshold, release capture and call `LoadFromHistory`.

- [ ] **Step 3: Export the dragged card’s edited composite**

Before rendering, call `SaveCurrentObjects()`. Resolve the dragged image’s object list from `_captureObjects`, falling back to an empty list. Render, save via `DragExportService`, and populate a WPF `DataObject`:

```csharp
data.SetData(DataFormats.FileDrop, new[] { path });
data.SetData(DataFormats.Bitmap, BitmapHelper.ToBitmapSource(rendered));
DragDrop.DoDragDrop(card, data, DragDropEffects.Copy);
```

Keep the temporary file after the operation. Set card opacity while `DoDragDrop` is active, restore it in `finally`, and show a status message for copied, cancelled, or failed operations.

- [ ] **Step 4: Preserve existing card behavior and improve discoverability**

Update the thumbnail tool tip to `좌클릭: 열기 | 드래그: 다른 앱으로 보내기 | 우클릭: 메뉴 | ↑↓: 이동`. Add equivalent `AutomationProperties.HelpText` to the card. Do not alter context-menu or keyboard-navigation handlers.

- [ ] **Step 5: Compile WPF event wiring**

Run: `dotnet build src/ShinCapture/ShinCapture.csproj -c Release --no-restore`

Expected: 0 errors.

- [ ] **Step 6: Commit the editor integration**

```powershell
git add src/ShinCapture/Views/EditorWindow.xaml.cs
git commit -m "feat: drag edited captures from history"
```

### Task 4: Full verification

**Files:** None

- [ ] **Step 1: Run the full suite**

Run: `dotnet test ShinCapture.sln -c Release --no-restore`

Expected: all tests pass with 0 failures.

- [ ] **Step 2: Verify repository hygiene**

Run: `git diff --check` and `git status --short`.

Expected: no whitespace errors and no feature-related uncommitted files.

- [ ] **Step 3: Perform Windows interoperability checks**

Verify a short click opens the card; a left drag past the threshold does not open it; edited annotations appear in the exported PNG; terminal drops insert a path; KakaoTalk drops attach an image; right-click and arrow navigation remain functional; files older than 24 hours are cleaned on the next export.
