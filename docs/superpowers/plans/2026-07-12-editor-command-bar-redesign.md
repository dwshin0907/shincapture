# Editor Command Bar Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize ShinCapture's editor chrome into a stable, accessible command bar with consistent vector icons, adaptive density, a cleaner history panel, and a passive status bar.

**Architecture:** Move editor-tool metadata and width breakpoints into small testable presentation policies. Keep drawing tools and commands unchanged; `EditorWindow` consumes the policies to construct WPF controls, while shared theme resources provide consistent focus, hover, menu, and icon treatment.

**Tech Stack:** .NET 8, WPF XAML, C#, xUnit

---

### Task 1: Adaptive editor chrome policy

**Files:**
- Create: `tests/ShinCapture.Tests/Editor/EditorChromeLayoutPolicyTests.cs`
- Create: `src/ShinCapture/Editor/EditorChromeLayoutPolicy.cs`

- [ ] **Step 1: Write the failing breakpoint tests**

```csharp
[Theory]
[InlineData(1320, EditorChromeMode.Comfortable, true, true, 180)]
[InlineData(1100, EditorChromeMode.Compact, false, true, 180)]
[InlineData(849, EditorChromeMode.Narrow, false, false, 0)]
public void ResolvesStableEditorChrome(
    double width,
    EditorChromeMode mode,
    bool showLabels,
    bool showHistory,
    double historyWidth)
{
    EditorChromeLayout layout = EditorChromeLayoutPolicy.Resolve(width);
    Assert.Equal(mode, layout.Mode);
    Assert.Equal(showLabels, layout.ShowToolLabels);
    Assert.Equal(showHistory, layout.ShowHistoryByDefault);
    Assert.Equal(historyWidth, layout.HistoryWidth);
}
```

- [ ] **Step 2: Run the test and verify RED**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter EditorChromeLayoutPolicyTests`

Expected: build failure because `EditorChromeLayoutPolicy` does not exist.

- [ ] **Step 3: Implement the minimal policy**

```csharp
public enum EditorChromeMode { Narrow, Compact, Comfortable }

public readonly record struct EditorChromeLayout(
    EditorChromeMode Mode,
    bool ShowToolLabels,
    bool ShowHistoryByDefault,
    double HistoryWidth);

public static class EditorChromeLayoutPolicy
{
    public const double ComfortableWidth = 1320;
    public const double CompactWidth = 850;

    public static EditorChromeLayout Resolve(double width) => width switch
    {
        >= ComfortableWidth => new(EditorChromeMode.Comfortable, true, true, 180),
        >= CompactWidth => new(EditorChromeMode.Compact, false, true, 180),
        _ => new(EditorChromeMode.Narrow, false, false, 0)
    };
}
```

- [ ] **Step 4: Run the focused test and verify GREEN**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter EditorChromeLayoutPolicyTests`

Expected: all focused tests pass.

### Task 2: Editor tool catalog and vector icon coverage

**Files:**
- Create: `tests/ShinCapture.Tests/Editor/EditorToolbarCatalogTests.cs`
- Create: `src/ShinCapture/Editor/EditorToolbarCatalog.cs`
- Modify: `tests/ShinCapture.Tests/Views/TrayIconGeometryConverterTests.cs`
- Modify: `src/ShinCapture/Views/Controls/TrayIconGeometryConverter.cs`

- [ ] **Step 1: Write failing catalog and geometry tests**

```csharp
[Fact]
public void DefinesFourteenUniqueToolsWithAccessibleMetadata()
{
    IReadOnlyList<EditorToolDescriptor> tools = EditorToolbarCatalog.Tools;
    Assert.Equal(14, tools.Count);
    Assert.Equal(tools.Count, tools.Select(tool => tool.Name).Distinct().Count());
    Assert.All(tools, tool =>
    {
        Assert.False(string.IsNullOrWhiteSpace(tool.IconKey));
        Assert.False(string.IsNullOrWhiteSpace(tool.ToolTip));
        Assert.All(tool.IconKey, character => Assert.True(character <= 127));
    });
}
```

Extend `TrayIconGeometryConverterTests` so every `EditorToolbarCatalog.Tools` icon key plus `ai`, `undo`, `redo`, `copy`, `save-as`, `save`, `more`, `history`, `zoom-out`, `zoom-in`, and `fit` must resolve to non-empty finite geometry.

- [ ] **Step 2: Run the focused tests and verify RED**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter "EditorToolbarCatalogTests|TrayIconGeometryConverterTests"`

Expected: build failure for the missing catalog or missing geometry coverage.

- [ ] **Step 3: Add the catalog and 24×24 vector paths**

```csharp
public sealed record EditorToolDescriptor(
    string Name,
    string IconKey,
    string Group,
    string Shortcut,
    string ToolTip);

public static class EditorToolbarCatalog
{
    public static IReadOnlyList<EditorToolDescriptor> Tools { get; } =
    [
        new("선택", "cursor", "select", "V", "선택 (V)"),
        new("펜", "pen", "draw", "P", "펜 (P)"),
        new("형광펜", "highlighter", "draw", "H", "형광펜 (H)"),
        new("도형", "shape", "draw", "U", "도형 (U)"),
        new("화살표", "arrow", "draw", "A", "화살표 (A)"),
        new("텍스트", "text", "text", "T", "텍스트 (T)"),
        new("말풍선", "balloon", "text", "B", "말풍선 (B)"),
        new("모자이크", "mosaic", "effect", "M", "모자이크 (M)"),
        new("블러", "blur", "effect", "", "블러"),
        new("번호", "number", "effect", "N", "번호 (N)"),
        new("이미지", "image", "insert", "I", "이미지 (I)"),
        new("색상추출", "eyedropper", "insert", "", "색상 추출"),
        new("크롭", "crop", "edit", "C", "크롭 (C)"),
        new("지우개", "eraser", "edit", "E", "지우개 (E)")
    ];
}
```

- [ ] **Step 4: Run focused tests and verify GREEN**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter "EditorToolbarCatalogTests|TrayIconGeometryConverterTests"`

Expected: all catalog and geometry tests pass.

### Task 3: Command bar, styles, and adaptive behavior

**Files:**
- Modify: `src/ShinCapture/Themes/LightTheme.xaml`
- Modify: `src/ShinCapture/Views/EditorWindow.xaml`
- Modify: `src/ShinCapture/Views/EditorWindow.xaml.cs`

- [ ] **Step 1: Replace wrapping toolbar chrome**

Use a two-column `Grid`: a horizontally scrollable `StackPanel` named `ToolbarPanel` on the left and persistent file buttons on the right. Remove editor advertising hosts. Add `HistoryBorder`, `HistoryToggleBtn`, and named zoom controls.

- [ ] **Step 2: Add editor command styles**

Create `EditorFocusVisual`, `EditorCommandButton`, `EditorCommandButtonActive`, `EditorIconButton`, and themed `ContextMenu`/`MenuItem` styles. Use 36 DIP minimum command targets, a 20 DIP vector icon, 8 DIP spacing rhythm, and a 2px visible keyboard focus ring.

- [ ] **Step 3: Consume catalog and icons**

`BuildToolbar()` must iterate `EditorToolbarCatalog.Tools`, render every icon through `TrayIconGeometryConverter`, register accessible names, group separators, and label references. Replace the three purple AI buttons with one `AI 도구` button and a three-item themed context menu. Keep existing click handlers.

- [ ] **Step 4: Apply layout policy on resize**

```csharp
private void UpdateChromeLayout()
{
    EditorChromeLayout layout = EditorChromeLayoutPolicy.Resolve(ActualWidth);
    foreach (TextBlock label in _toolLabels.Values)
        label.Visibility = layout.ShowToolLabels ? Visibility.Visible : Visibility.Collapsed;
    HistoryBorder.Width = layout.HistoryWidth;
    HistoryBorder.Visibility = layout.ShowHistoryByDefault || _historyPanelPinnedOpen
        ? Visibility.Visible
        : Visibility.Collapsed;
}
```

Preserve the user's explicit history toggle until the window crosses into another layout mode.

- [ ] **Step 5: Compile the WPF XAML**

Run: `dotnet build src/ShinCapture/ShinCapture.csproj -c Release`

Expected: build succeeds with no new warnings.

### Task 4: Property bar, history, OCR, and zoom polish

**Files:**
- Modify: `src/ShinCapture/Views/EditorWindow.xaml`
- Modify: `src/ShinCapture/Views/EditorWindow.xaml.cs`
- Modify: `src/ShinCapture/Themes/LightTheme.xaml`

- [ ] **Step 1: Increase property control targets**

Change color controls to keyboard-focusable buttons with 18 DIP swatches inside 28 DIP hit targets. Apply `AutomationProperties.Name` using the color hex value or action name.

- [ ] **Step 2: Redesign history cards**

Use a 148×124 card containing the thumbnail and a footer with item number and pixel dimensions. Rename header actions to `전체 저장` and `기록 비우기`; keep arrow navigation and context actions.

- [ ] **Step 3: Simplify OCR and status chrome**

Remove structural emoji, use the app font for source and translation text, and apply consistent command styles to copy, translate, and close. Keep only status text and zoom controls in the status bar.

- [ ] **Step 4: Wire zoom buttons**

Zoom out and in by a 1.1 factor, reset to device-correct 100%, and call `Canvas.ApplyInitialZoom()` for fit. Give every button a tooltip and automation name.

- [ ] **Step 5: Run the full verification suite**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj`

Expected: all tests pass.

Run: `dotnet build src/ShinCapture/ShinCapture.csproj -c Release`

Expected: build succeeds with no new warnings.

- [ ] **Step 6: Review the completed diff**

Run: `git diff --check` and `git status -sb`.

Expected: no whitespace errors and only the planned editor UI, policy, tests, spec, and plan files are changed.
