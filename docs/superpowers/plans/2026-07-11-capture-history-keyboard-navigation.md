# Capture History Keyboard Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Up and Down move through the editor's right-side capture history while preserving canvas arrow-key movement outside that panel.

**Architecture:** A pure navigation policy owns index and boundary behavior. `EditorWindow` keeps its existing dynamic thumbnail cards, turns the selected card into a roving Tab stop, intercepts Up/Down only when history owns keyboard focus, reloads the selected capture, and restores focus after the card list is rebuilt.

**Tech Stack:** C# 12, .NET 8, WPF, xUnit

---

## File map

- Create `src/ShinCapture/Editor/CaptureHistoryNavigationPolicy.cs`: pure Up/Down index calculation.
- Create `tests/ShinCapture.Tests/Editor/CaptureHistoryNavigationPolicyTests.cs`: navigation and boundary contract.
- Modify `src/ShinCapture/Views/EditorWindow.xaml`: system-color focus visual for history cards.
- Modify `src/ShinCapture/Views/EditorWindow.xaml.cs`: roving focus, key routing, capture loading, and scroll restoration.

### Task 1: Pure capture-history navigation policy

**Files:**
- Create: `src/ShinCapture/Editor/CaptureHistoryNavigationPolicy.cs`
- Create: `tests/ShinCapture.Tests/Editor/CaptureHistoryNavigationPolicyTests.cs`

- [ ] **Step 1: Write the failing policy tests**

Create `tests/ShinCapture.Tests/Editor/CaptureHistoryNavigationPolicyTests.cs`:

```csharp
using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public class CaptureHistoryNavigationPolicyTests
{
    [Theory]
    [InlineData(2, 5, CaptureHistoryDirection.Up, 1)]
    [InlineData(2, 5, CaptureHistoryDirection.Down, 3)]
    [InlineData(0, 5, CaptureHistoryDirection.Up, 0)]
    [InlineData(4, 5, CaptureHistoryDirection.Down, 4)]
    public void CalculatesClampedVisualNeighbor(
        int current,
        int count,
        CaptureHistoryDirection direction,
        int expected)
    {
        Assert.Equal(
            expected,
            CaptureHistoryNavigationPolicy.GetTargetIndex(current, count, direction));
    }

    [Fact]
    public void EmptyHistoryHasNoTarget()
    {
        Assert.Equal(
            -1,
            CaptureHistoryNavigationPolicy.GetTargetIndex(
                0,
                0,
                CaptureHistoryDirection.Down));
    }

    [Theory]
    [InlineData(CaptureHistoryDirection.Down, 0)]
    [InlineData(CaptureHistoryDirection.Up, 4)]
    public void MissingSelectionStartsAtDirectionalEdge(
        CaptureHistoryDirection direction,
        int expected)
    {
        Assert.Equal(
            expected,
            CaptureHistoryNavigationPolicy.GetTargetIndex(-1, 5, direction));
    }
}
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter FullyQualifiedName~CaptureHistoryNavigationPolicyTests
```

Expected: compile failure because `CaptureHistoryNavigationPolicy` and `CaptureHistoryDirection` do not exist.

- [ ] **Step 3: Implement the minimal policy**

Create `src/ShinCapture/Editor/CaptureHistoryNavigationPolicy.cs`:

```csharp
using System;

namespace ShinCapture.Editor;

public enum CaptureHistoryDirection
{
    Up = -1,
    Down = 1
}

public static class CaptureHistoryNavigationPolicy
{
    public static int GetTargetIndex(
        int currentIndex,
        int itemCount,
        CaptureHistoryDirection direction)
    {
        if (itemCount <= 0)
            return -1;

        if (currentIndex < 0 || currentIndex >= itemCount)
            return direction == CaptureHistoryDirection.Down ? 0 : itemCount - 1;

        int offset = direction == CaptureHistoryDirection.Down ? 1 : -1;
        return Math.Clamp(currentIndex + offset, 0, itemCount - 1);
    }
}
```

- [ ] **Step 4: Run focused tests and commit**

```powershell
dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter FullyQualifiedName~CaptureHistoryNavigationPolicyTests
git add src/ShinCapture/Editor/CaptureHistoryNavigationPolicy.cs tests/ShinCapture.Tests/Editor/CaptureHistoryNavigationPolicyTests.cs
git commit -m "feat: 캡처 기록 방향키 이동 정책 추가"
```

Expected: all focused tests pass.

### Task 2: WPF focus routing and selection restoration

**Files:**
- Modify: `src/ShinCapture/Views/EditorWindow.xaml`
- Modify: `src/ShinCapture/Views/EditorWindow.xaml.cs`

- [ ] **Step 1: Add the system-color focus visual**

Add to `EditorWindow.xaml` before the root `Grid`:

```xml
<Window.Resources>
    <Style x:Key="HistoryItemFocusVisual">
        <Setter Property="Control.Template">
            <Setter.Value>
                <ControlTemplate>
                    <Rectangle Margin="1" RadiusX="4" RadiusY="4"
                               Stroke="{DynamicResource {x:Static SystemColors.HighlightBrushKey}}"
                               StrokeThickness="2" SnapsToDevicePixels="True"/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</Window.Resources>
```

- [ ] **Step 2: Route history Up/Down before canvas arrow movement**

Add a `_historyCards` dictionary keyed by `BitmapSource`. At the start of `OnEditorKeyDown`, before textbox handling, consume Up/Down whenever `HistoryPanel.IsKeyboardFocusWithin`. With no modifiers, call:

```csharp
private void NavigateCaptureHistory(Key key)
{
    BitmapSource currentImage =
        (Keyboard.FocusedElement as FrameworkElement)?.Tag as BitmapSource
        ?? _sourceImage;
    int currentIndex = _captureHistory.IndexOf(currentImage);
    CaptureHistoryDirection direction = key == Key.Up
        ? CaptureHistoryDirection.Up
        : CaptureHistoryDirection.Down;
    int targetIndex = CaptureHistoryNavigationPolicy.GetTargetIndex(
        currentIndex,
        _captureHistory.Count,
        direction);

    if (targetIndex >= 0 && targetIndex != currentIndex)
        LoadFromHistory(_captureHistory[targetIndex], focusHistoryItem: true);
}
```

Modified Up/Down is consumed in the history context but performs no canvas movement.

- [ ] **Step 3: Make the selected thumbnail a roving Tab stop**

In `BuildHistory`:

- clear `_historyCards` with the panel;
- set each card's `Tag` to its `BitmapSource` and `Focusable = true`;
- assign `HistoryItemFocusVisual`;
- set `KeyboardNavigation.IsTabStop` only for the selected capture;
- set an automation name containing its position and pixel dimensions;
- change left-click loading to `LoadFromHistory(localImg, focusHistoryItem: true)`;
- store each card in `_historyCards`.

Use:

```csharp
System.Windows.Automation.AutomationProperties.SetName(
    border,
    $"캡처 결과 {i + 1}, {img.PixelWidth} × {img.PixelHeight}");
```

- [ ] **Step 4: Restore focus and visibility after rebuilding cards**

Change the loader signature to:

```csharp
private void LoadFromHistory(BitmapSource image, bool focusHistoryItem = false)
```

After `BuildHistory`, schedule:

```csharp
private void ScheduleHistoryFocus(BitmapSource image)
{
    Dispatcher.BeginInvoke(new Action(() =>
    {
        if (!_historyCards.TryGetValue(image, out Border? card))
            return;

        _ = card.Focus();
        card.BringIntoView();
    }), System.Windows.Threading.DispatcherPriority.Input);
}
```

Call it only when `focusHistoryItem` is true. Existing context-menu and programmatic history loads retain their current focus behavior.

- [ ] **Step 5: Verify focused and full behavior**

```powershell
dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter FullyQualifiedName~CaptureHistoryNavigationPolicyTests -c Release
dotnet build ShinCapture.sln -c Release
dotnet test ShinCapture.sln -c Release
git diff --check
```

Expected: policy tests pass, WPF build succeeds, and the full suite passes.

- [ ] **Step 6: Commit integration**

```powershell
git add src/ShinCapture/Views/EditorWindow.xaml src/ShinCapture/Views/EditorWindow.xaml.cs
git commit -m "feat: 캡처 기록 방향키 탐색 연결"
```

### Task 3: Interaction QA

**Files:**
- Modify only the history navigation files if QA exposes a defect.

- [ ] **Step 1: Verify mouse-to-keyboard continuity**

Click a middle history thumbnail, then press Up and Down. The visible capture, selection border, keyboard focus, and saved editor objects must move together.

- [ ] **Step 2: Verify keyboard entry and boundaries**

Tab to the selected history card. Up at the first card and Down at the last card must remain on the same card without wrapping.

- [ ] **Step 3: Verify scrolling and canvas compatibility**

With enough history items to scroll, hold Down until later cards are selected and confirm each card comes into view. Return focus to the canvas and confirm arrow keys still move selected editor objects by the existing increments.

- [ ] **Step 4: Run final verification**

```powershell
dotnet test ShinCapture.sln -c Release
git diff --check
git status --short
```

Expected: all tests pass, no whitespace errors, and unrelated untracked user files remain untouched.
