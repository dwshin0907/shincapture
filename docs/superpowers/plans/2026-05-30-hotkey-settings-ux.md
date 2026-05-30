# 단축키 설정 UX 개선 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 단축키 설정을 '키 누르기' 입력 + 실시간 충돌 감지/추천 + 자주쓰는·고급 그룹화로 개선하고, 시작 시 등록 실패를 사용자에게 알린다.

**Architecture:** 순수 로직(`HotkeyInput`, `HotkeyConflicts` — WPF/Win32 비의존, 단위테스트)과 UI/OS 계층(`HotkeyCaptureBox` 컨트롤, `HotkeyManager.IsAvailable` 프로브, `SettingsWindow` 행 생성, `MainWindow` 알림)을 분리. 백엔드 단축키 문자열 포맷("Ctrl+Shift+G")과 `ParseHotkeyString`/`Register` 경로는 유지 → 마이그레이션 없음.

**Tech Stack:** .NET 8 WPF, xUnit, Win32 RegisterHotKey, `System.Windows.Input`(Key/ModifierKeys).

참조 설계: `docs/superpowers/specs/2026-05-30-hotkey-settings-ux-design.md`

---

## File Structure

- Create: `src/ShinCapture/Services/Hotkeys/HotkeyInput.cs` — (수정자,키)→정규문자열, 유효성 (순수)
- Create: `src/ShinCapture/Services/Hotkeys/HotkeyConflicts.cs` — 정규화/내부충돌/대안추천 (순수)
- Create: `src/ShinCapture/Views/Controls/HotkeyCaptureBox.cs` — 키 캡쳐 입력 컨트롤
- Modify: `src/ShinCapture/Services/HotkeyManager.cs` — `IsAvailable` 프로브 추가
- Modify: `src/ShinCapture/Views/SettingsWindow.xaml` — 단축키 탭을 그룹 컨테이너로 교체
- Modify: `src/ShinCapture/Views/SettingsWindow.xaml.cs` — 행 생성/충돌배선/복원, 생성자에 HotkeyManager
- Modify: `src/ShinCapture/Views/MainWindow.xaml.cs` — 실패수집+트레이알림, 열기 전 UnregisterAll, HotkeyManager 전달
- Create: `tests/ShinCapture.Tests/Services/Hotkeys/HotkeyInputTests.cs`
- Create: `tests/ShinCapture.Tests/Services/Hotkeys/HotkeyConflictsTests.cs`

---

## Task 1: HotkeyInput (순수 로직)

**Files:**
- Create: `src/ShinCapture/Services/Hotkeys/HotkeyInput.cs`
- Test: `tests/ShinCapture.Tests/Services/Hotkeys/HotkeyInputTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`tests/ShinCapture.Tests/Services/Hotkeys/HotkeyInputTests.cs`:
```csharp
using System.Windows.Input;
using ShinCapture.Services.Hotkeys;

namespace ShinCapture.Tests.Services.Hotkeys;

public class HotkeyInputTests
{
    [Fact]
    public void Format_OrdersModifiersCtrlAltShift_ThenKey()
    {
        var s = HotkeyInput.Format(ModifierKeys.Shift | ModifierKeys.Control, Key.G);
        Assert.Equal("Ctrl+Shift+G", s);
    }

    [Fact]
    public void Format_DigitKey_UsesBareDigit()
    {
        Assert.Equal("Ctrl+1", HotkeyInput.Format(ModifierKeys.Control, Key.D1));
    }

    [Fact]
    public void Format_PrintScreen_NoModifier()
    {
        Assert.Equal("PrintScreen", HotkeyInput.Format(ModifierKeys.None, Key.PrintScreen));
    }

    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.System)]
    public void IsModifierKey_True(Key k) => Assert.True(HotkeyInput.IsModifierKey(k));

    [Fact]
    public void IsValid_BareLetter_Rejected()
    {
        Assert.False(HotkeyInput.IsValidGlobalHotkey(ModifierKeys.None, Key.G, out _));
    }

    [Fact]
    public void IsValid_PrintScreenAlone_Allowed()
    {
        Assert.True(HotkeyInput.IsValidGlobalHotkey(ModifierKeys.None, Key.PrintScreen, out _));
    }

    [Fact]
    public void IsValid_CtrlLetter_Allowed()
    {
        Assert.True(HotkeyInput.IsValidGlobalHotkey(ModifierKeys.Control, Key.G, out _));
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter "FullyQualifiedName~HotkeyInputTests"`
Expected: FAIL — `HotkeyInput` 형식을 찾을 수 없음 (빌드 에러).

- [ ] **Step 3: 구현**

`src/ShinCapture/Services/Hotkeys/HotkeyInput.cs`:
```csharp
using System.Collections.Generic;
using System.Windows.Input;

namespace ShinCapture.Services.Hotkeys;

/// <summary>키 입력을 백엔드 단축키 문자열로 변환하고 전역 단축키 유효성을 검사하는 순수 로직.</summary>
public static class HotkeyInput
{
    public static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.System or Key.LWin or Key.RWin;

    /// <summary>수정자 없이도 전역 등록이 자연스러운 키(기능키/PrintScreen).</summary>
    public static bool AllowsNoModifier(Key key) =>
        key == Key.PrintScreen || (key >= Key.F1 && key <= Key.F24);

    /// <summary>(수정자, 키) → "Ctrl+Shift+G". ParseHotkeyString이 그대로 해석 가능한 정규형.</summary>
    public static string Format(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(KeyToken(key));
        return string.Join("+", parts);
    }

    private static string KeyToken(Key key)
    {
        if (key >= Key.D0 && key <= Key.D9)
            return ((char)('0' + (key - Key.D0))).ToString();
        return key.ToString();
    }

    public static bool IsValidGlobalHotkey(ModifierKeys modifiers, Key key, out string? error)
    {
        if (key == Key.None || IsModifierKey(key))
        {
            error = "일반 키를 함께 눌러주세요.";
            return false;
        }
        if (modifiers == ModifierKeys.None && !AllowsNoModifier(key))
        {
            error = "Ctrl/Alt/Shift와 함께 눌러주세요.";
            return false;
        }
        error = null;
        return true;
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter "FullyQualifiedName~HotkeyInputTests"`
Expected: PASS (7 통과).

- [ ] **Step 5: 커밋**

```bash
git add src/ShinCapture/Services/Hotkeys/HotkeyInput.cs tests/ShinCapture.Tests/Services/Hotkeys/HotkeyInputTests.cs
git commit -m "feat: HotkeyInput — 키 입력 정규화/유효성 (순수 로직)"
```

---

## Task 2: HotkeyConflicts (순수 로직)

**Files:**
- Create: `src/ShinCapture/Services/Hotkeys/HotkeyConflicts.cs`
- Test: `tests/ShinCapture.Tests/Services/Hotkeys/HotkeyConflictsTests.cs`

`Normalize`는 기존 `HotkeyManager.ParseHotkeyString`(static)을 재사용해 (수정자,vk)로 비교한다.

- [ ] **Step 1: 실패하는 테스트 작성**

`tests/ShinCapture.Tests/Services/Hotkeys/HotkeyConflictsTests.cs`:
```csharp
using System.Collections.Generic;
using ShinCapture.Services.Hotkeys;

namespace ShinCapture.Tests.Services.Hotkeys;

public class HotkeyConflictsTests
{
    [Fact]
    public void Normalize_IgnoresModifierOrder()
    {
        Assert.Equal(HotkeyConflicts.Normalize("Ctrl+Shift+G"),
                     HotkeyConflicts.Normalize("Shift+Ctrl+G"));
    }

    [Fact]
    public void Normalize_EmptyOrInvalid_Null()
    {
        Assert.Null(HotkeyConflicts.Normalize(""));
        Assert.Null(HotkeyConflicts.Normalize("   "));
    }

    [Fact]
    public void FindInternalConflict_DetectsCollision_ExcludingSelf()
    {
        var bindings = new Dictionary<string, string>
        {
            ["영역지정"] = "Ctrl+Shift+C",
            ["전체화면"] = "Ctrl+Shift+A",
        };
        Assert.Equal("전체화면",
            HotkeyConflicts.FindInternalConflict("Shift+Ctrl+A", bindings, "영역지정"));
        Assert.Null(
            HotkeyConflicts.FindInternalConflict("Ctrl+Shift+A", bindings, "전체화면")); // 자기 자신
        Assert.Null(
            HotkeyConflicts.FindInternalConflict("Ctrl+Shift+X", bindings, "영역지정"));
    }

    [Fact]
    public void SuggestAlternative_ReturnsFirstAvailableVariant()
    {
        // Ctrl+Shift+W는 점유. Ctrl+Alt+W는 빈 것으로 가정.
        bool IsAvailable(string combo) => combo != "Ctrl+Shift+W";
        var alt = HotkeyConflicts.SuggestAlternative("Ctrl+Shift+W", IsAvailable);
        Assert.Equal("Ctrl+Alt+W", alt);
    }

    [Fact]
    public void SuggestAlternative_NoneAvailable_Null()
    {
        var alt = HotkeyConflicts.SuggestAlternative("Ctrl+Shift+W", _ => false);
        Assert.Null(alt);
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter "FullyQualifiedName~HotkeyConflictsTests"`
Expected: FAIL — `HotkeyConflicts` 없음.

- [ ] **Step 3: 구현**

`src/ShinCapture/Services/Hotkeys/HotkeyConflicts.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Windows.Input;
using ShinCapture.Services;

namespace ShinCapture.Services.Hotkeys;

/// <summary>단축키 충돌 탐지/대안 추천 (순수 로직, OS 가용성은 주입된 함수로).</summary>
public static class HotkeyConflicts
{
    /// <summary>순서/대소문자 무시 비교용 정규 키. 빈/무효는 null.</summary>
    public static string? Normalize(string? hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return null;
        HotkeyManager.ParseHotkeyString(hotkey, out uint mods, out uint vk);
        if (vk == 0) return null;
        return $"{mods}:{vk}";
    }

    /// <summary>candidate가 (selfKey 제외) bindings 중 무엇과 겹치면 그 키, 없으면 null.</summary>
    public static string? FindInternalConflict(
        string candidate, IReadOnlyDictionary<string, string> bindings, string selfKey)
    {
        var norm = Normalize(candidate);
        if (norm == null) return null;
        foreach (var kv in bindings)
        {
            if (kv.Key == selfKey) continue;
            if (Normalize(kv.Value) == norm) return kv.Key;
        }
        return null;
    }

    /// <summary>taken과 같은 키를 유지하며 수정자 조합을 바꿔 isAvailable인 첫 조합 반환. 없으면 null.</summary>
    public static string? SuggestAlternative(string taken, Func<string, bool> isAvailable)
    {
        HotkeyManager.ParseHotkeyString(taken, out _, out uint vk);
        if (vk == 0) return null;
        var key = KeyInterop.KeyFromVirtualKey((int)vk);

        var candidates = new[]
        {
            ModifierKeys.Control | ModifierKeys.Shift,
            ModifierKeys.Control | ModifierKeys.Alt,
            ModifierKeys.Alt | ModifierKeys.Shift,
            ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift,
        };
        foreach (var mods in candidates)
        {
            var combo = HotkeyInput.Format(mods, key);
            if (isAvailable(combo)) return combo;
        }
        return null;
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter "FullyQualifiedName~HotkeyConflictsTests"`
Expected: PASS (5 통과).

- [ ] **Step 5: 커밋**

```bash
git add src/ShinCapture/Services/Hotkeys/HotkeyConflicts.cs tests/ShinCapture.Tests/Services/Hotkeys/HotkeyConflictsTests.cs
git commit -m "feat: HotkeyConflicts — 충돌 탐지/대안 추천 (순수 로직)"
```

---

## Task 3: HotkeyManager.IsAvailable (OS 프로브)

**Files:**
- Modify: `src/ShinCapture/Services/HotkeyManager.cs`

OS 차원 점유 여부를 임시 등록으로 프로브. (단위테스트는 hwnd 필요해 생략 — Task 7 수동검증.)

- [ ] **Step 1: 메서드 추가**

`HotkeyManager` 클래스 안, `Unregister` 메서드 뒤에 추가:
```csharp
    /// <summary>이 조합을 지금 전역 등록 가능한지 프로브한다(성공 시 즉시 해제). UI 스레드에서 호출.</summary>
    public bool IsAvailable(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString)) return true;
        ParseHotkeyString(hotkeyString, out uint modifiers, out uint vk);
        if (vk == 0) return false;
        modifiers |= NativeMethods.MOD_NOREPEAT;
        const int probeId = 0x7000; // 활성 등록 id 범위(_nextId)와 충돌하지 않는 높은 값
        if (NativeMethods.RegisterHotKey(_hwnd, probeId, modifiers, vk))
        {
            NativeMethods.UnregisterHotKey(_hwnd, probeId);
            return true;
        }
        return false;
    }
```

- [ ] **Step 2: 빌드 확인**

Run: `dotnet build src/ShinCapture/ShinCapture.csproj -c Debug`
Expected: 오류 0개.

- [ ] **Step 3: 커밋**

```bash
git add src/ShinCapture/Services/HotkeyManager.cs
git commit -m "feat: HotkeyManager.IsAvailable — 전역 단축키 가용성 프로브"
```

---

## Task 4: HotkeyCaptureBox (키 캡쳐 컨트롤)

**Files:**
- Create: `src/ShinCapture/Views/Controls/HotkeyCaptureBox.cs`

읽기전용 TextBox 파생. 클릭→키 입력→`Value`("Ctrl+Shift+G"). Esc 취소, Backspace/Delete 비우기.

- [ ] **Step 1: 구현**

`src/ShinCapture/Views/Controls/HotkeyCaptureBox.cs`:
```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShinCapture.Services.Hotkeys;

namespace ShinCapture.Views.Controls;

/// <summary>클릭 후 키 조합을 눌러 단축키를 설정하는 박스. Value는 "Ctrl+Shift+G" 형식("" = 미설정).</summary>
public class HotkeyCaptureBox : TextBox
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(HotkeyCaptureBox),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>사용자가 값을 확정(또는 비움)했을 때.</summary>
    public event EventHandler? ValueCommitted;

    public HotkeyCaptureBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Cursor = Cursors.Hand;
        Padding = new Thickness(4, 3, 4, 3);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (HotkeyCaptureBox)d;
        if (!box.IsKeyboardFocused)
            box.Text = (string)e.NewValue ?? "";
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Text = "키를 누르세요…";
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        Text = Value ?? "";
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape) { Text = Value ?? ""; Keyboard.ClearFocus(); return; }
        if (key == Key.Back || key == Key.Delete)
        {
            Value = ""; Text = "";
            ValueCommitted?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (HotkeyInput.IsModifierKey(key)) return; // 일반 키가 올 때까지 대기

        var mods = Keyboard.Modifiers;
        if (!HotkeyInput.IsValidGlobalHotkey(mods, key, out _)) return; // 무효 조합 무시

        Value = HotkeyInput.Format(mods, key);
        Text = Value;
        ValueCommitted?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 2: 빌드 확인**

Run: `dotnet build src/ShinCapture/ShinCapture.csproj -c Debug`
Expected: 오류 0개.

- [ ] **Step 3: 커밋**

```bash
git add src/ShinCapture/Views/Controls/HotkeyCaptureBox.cs
git commit -m "feat: HotkeyCaptureBox — '키 누르기' 단축키 입력 컨트롤"
```

---

## Task 5: SettingsWindow 단축키 탭 재구성

**Files:**
- Modify: `src/ShinCapture/Views/SettingsWindow.xaml` (단축키 TabItem 내부)
- Modify: `src/ShinCapture/Views/SettingsWindow.xaml.cs`

11개 명명 TextBox를 제거하고, 정의 테이블에서 행을 코드로 생성(기본/고급). 각 행: 라벨 + `HotkeyCaptureBox` + 배지 + 비우기 + (🔴 시)추천.

- [ ] **Step 1: XAML — 단축키 탭 내부 교체**

`src/ShinCapture/Views/SettingsWindow.xaml`에서 `<!-- 단축키 탭 -->` `TabItem`의 **여는 `<Grid …>`부터 닫는 `</Grid>`까지 전체**(현재 row 0~14 그리드)를 아래로 교체:
```xml
            <!-- 단축키 탭 -->
            <TabItem Header="단축키">
                <ScrollViewer VerticalScrollBarVisibility="Auto">
                    <StackPanel Margin="12,16,12,0">
                        <TextBlock Text="자주 쓰는" Foreground="{DynamicResource TextSecondaryBrush}"
                                   FontSize="{DynamicResource FontSizeSmall}" Margin="0,0,0,6"/>
                        <StackPanel x:Name="BasicHotkeyPanel"/>

                        <Expander x:Name="AdvancedHotkeyExpander" Header="고급" Margin="0,8,0,0" IsExpanded="False">
                            <StackPanel x:Name="AdvancedHotkeyPanel" Margin="0,6,0,0"/>
                        </Expander>

                        <TextBlock Foreground="{DynamicResource TextSecondaryBrush}"
                                   FontSize="{DynamicResource FontSizeSmall}" Margin="0,8,0,4"
                                   Text="🟢 사용 가능   🟡 신캡쳐 내 다른 기능과 겹침   🔴 다른 앱이 사용 중"/>

                        <Separator Margin="0,4,0,10"/>

                        <CheckBox x:Name="ChkOverridePrintScreen"
                                  Content="PrintScreen 키를 신캡쳐가 독점 사용" Margin="0,0,0,10"
                                  ToolTip="Windows 11의 'PrtSc 키로 화면 캡쳐 열기' 설정을 자동으로 꺼서, PrintScreen 키가 신캡쳐로 전달되도록 합니다. 해제하면 Windows 기본 동작(캡쳐 도구 열기)으로 되돌립니다."/>

                        <Button x:Name="BtnResetHotkeys" Content="기본값으로 복원"
                                HorizontalAlignment="Left" Padding="12,4" Margin="0,0,0,12"
                                Click="OnResetHotkeys"/>

                        <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
                            <TextBlock Text="OCR 언어" VerticalAlignment="Center" Width="120"/>
                            <ComboBox x:Name="CmbOcrLanguage" Width="200"/>
                            <Button x:Name="BtnOcrLanguageHelp" Content="언어팩 설치" Margin="8,0,0,0"
                                    Padding="8,2" Click="OnOcrLanguageHelp"/>
                        </StackPanel>

                        <CheckBox x:Name="ChkOcrUpscale"
                                  Content="OCR 인식률 향상 전처리"
                                  ToolTip="OCR 실행 전에 작은 이미지 업스케일, 어두운 배경 자동 반전 등을 적용합니다. 원본 캡쳐 이미지에는 영향 없음. 보통 켜두는 것을 권장합니다."/>
                    </StackPanel>
                </ScrollViewer>
            </TabItem>
```

- [ ] **Step 2: 코드비하인드 — using/필드/행 모델 추가**

`src/ShinCapture/Views/SettingsWindow.xaml.cs` 상단 using 블록에 추가:
```csharp
using System.Windows.Controls;
using System.Windows.Media;
using ShinCapture.Services.Hotkeys;
using ShinCapture.Views.Controls;
```

`SettingsWindow` 클래스 필드 영역(`private readonly DpapiCredentialStore _aiStore = new();` 아래)에 추가:
```csharp
    private readonly HotkeyManager? _hotkeyManager;
    private readonly System.Collections.Generic.List<HotkeyRow> _hotkeyRows = new();

    private sealed class HotkeyDef
    {
        public string Label = "";
        public bool Advanced;
        public Func<HotkeySettings, string> Get = _ => "";
        public Action<HotkeySettings, string> Set = (_, _) => { };
    }

    private sealed class HotkeyRow
    {
        public HotkeyDef Def = null!;
        public HotkeyCaptureBox Box = null!;
        public TextBlock Badge = null!;
        public TextBlock Suggestion = null!;
        public string? SuggestedValue;
    }

    private static readonly HotkeyDef[] HotkeyDefs =
    {
        new() { Label = "영역지정",      Get = h => h.RegionCapture,     Set = (h, v) => h.RegionCapture = v },
        new() { Label = "영역지정 (보조)", Get = h => h.RegionCaptureAlt,  Set = (h, v) => h.RegionCaptureAlt = v },
        new() { Label = "전체화면",      Get = h => h.FullscreenCapture, Set = (h, v) => h.FullscreenCapture = v },
        new() { Label = "창 캡쳐",       Get = h => h.WindowCapture,     Set = (h, v) => h.WindowCapture = v },
        new() { Label = "스크롤",        Get = h => h.ScrollCapture,     Set = (h, v) => h.ScrollCapture = v },
        new() { Label = "스마트 컷",     Get = h => h.SmartCutCapture,   Set = (h, v) => h.SmartCutCapture = v },
        new() { Advanced = true, Label = "자유형",     Get = h => h.FreeformCapture,  Set = (h, v) => h.FreeformCapture = v },
        new() { Advanced = true, Label = "단위영역",   Get = h => h.ElementCapture,   Set = (h, v) => h.ElementCapture = v },
        new() { Advanced = true, Label = "지정사이즈", Get = h => h.FixedSizeCapture, Set = (h, v) => h.FixedSizeCapture = v },
        new() { Advanced = true, Label = "텍스트 캡쳐", Get = h => h.TextCapture,      Set = (h, v) => h.TextCapture = v },
        new() { Advanced = true, Label = "텍스트+번역", Get = h => h.TranslateCapture, Set = (h, v) => h.TranslateCapture = v },
    };
```

- [ ] **Step 3: 생성자 시그니처에 HotkeyManager 추가**

기존 생성자(20~31행)를 교체:
```csharp
    public SettingsWindow(SettingsManager settingsManager, HotkeyManager? hotkeyManager = null, int initialTabIndex = 0)
    {
        InitializeComponent();
        _settingsManager = settingsManager;
        _hotkeyManager = hotkeyManager;
        _settings = settingsManager.Load();
        BuildHotkeyRows();
        LoadSettings();
        if (initialTabIndex >= 0 && SettingsTabControl != null
            && initialTabIndex < SettingsTabControl.Items.Count)
        {
            SettingsTabControl.SelectedIndex = initialTabIndex;
        }
    }
```

- [ ] **Step 4: LoadSettings/ApplyToSettings의 단축키 부분 교체**

`LoadSettings()`에서 단축키 블록(현재 60~71행, `TxtHkRegion.Text … = …` 11줄 + `ChkOverridePrintScreen.IsChecked = …`)을 아래로 교체:
```csharp
        // 단축키 — 행 값 채우기 (행은 생성자에서 BuildHotkeyRows로 생성됨)
        foreach (var row in _hotkeyRows)
            row.Box.Value = row.Def.Get(_settings.Hotkeys);
        ChkOverridePrintScreen.IsChecked = _settings.Hotkeys.OverridePrintScreen;
        RefreshHotkeyConflicts();
```

`ApplyToSettings()`에서 단축키 블록(현재 154~166행, `_settings.Hotkeys.* = TxtHk*.Text` 11줄 + override)을 아래로 교체:
```csharp
        // 단축키
        foreach (var row in _hotkeyRows)
            row.Def.Set(_settings.Hotkeys, row.Box.Value);
        _settings.Hotkeys.OverridePrintScreen = ChkOverridePrintScreen.IsChecked == true;
```

- [ ] **Step 5: 행 생성/충돌검사/복원 메서드 추가**

`SettingsWindow` 클래스 끝(`OnHyperlinkRequestNavigate` 뒤, 클래스 닫는 `}` 앞)에 추가:
```csharp
    private void BuildHotkeyRows()
    {
        BasicHotkeyPanel.Children.Clear();
        AdvancedHotkeyPanel.Children.Clear();
        _hotkeyRows.Clear();

        foreach (var def in HotkeyDefs)
        {
            var container = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock { Text = def.Label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 0);

            var box = new HotkeyCaptureBox();
            Grid.SetColumn(box, 1);

            var badge = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(badge, 2);

            var clear = new Button
            {
                Content = "✕",
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = "비우기"
            };
            Grid.SetColumn(clear, 3);

            grid.Children.Add(label);
            grid.Children.Add(box);
            grid.Children.Add(badge);
            grid.Children.Add(clear);

            var suggestion = new TextBlock
            {
                Margin = new Thickness(124, 2, 0, 0),
                Foreground = Brushes.OrangeRed,
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.Hand
            };

            container.Children.Add(grid);
            container.Children.Add(suggestion);

            var row = new HotkeyRow { Def = def, Box = box, Badge = badge, Suggestion = suggestion };
            box.ValueCommitted += (_, _) => RefreshHotkeyConflicts();
            clear.Click += (_, _) => { box.Value = ""; RefreshHotkeyConflicts(); };
            suggestion.MouseLeftButtonUp += (_, _) =>
            {
                if (row.SuggestedValue != null) { box.Value = row.SuggestedValue; RefreshHotkeyConflicts(); }
            };

            _hotkeyRows.Add(row);
            (def.Advanced ? AdvancedHotkeyPanel : BasicHotkeyPanel).Children.Add(container);
        }
    }

    private void RefreshHotkeyConflicts()
    {
        var bindings = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var r in _hotkeyRows) bindings[r.Def.Label] = r.Box.Value;

        foreach (var row in _hotkeyRows)
        {
            var val = row.Box.Value;
            row.Suggestion.Visibility = Visibility.Collapsed;
            row.SuggestedValue = null;

            if (string.IsNullOrWhiteSpace(val))
            {
                row.Badge.Text = "";
                row.Badge.ToolTip = "미설정";
                continue;
            }

            var conflict = HotkeyConflicts.FindInternalConflict(val, bindings, row.Def.Label);
            if (conflict != null)
            {
                row.Badge.Text = "🟡";
                row.Badge.ToolTip = $"'{conflict}' 기능과 겹쳐요";
                continue;
            }

            bool available = _hotkeyManager?.IsAvailable(val) ?? true;
            if (!available)
            {
                row.Badge.Text = "🔴";
                row.Badge.ToolTip = "다른 프로그램이 사용 중";
                var alt = HotkeyConflicts.SuggestAlternative(val, c =>
                    HotkeyConflicts.FindInternalConflict(c, bindings, row.Def.Label) == null
                    && (_hotkeyManager?.IsAvailable(c) ?? true));
                if (alt != null)
                {
                    row.SuggestedValue = alt;
                    row.Suggestion.Text = $"↳ 추천: {alt}  (클릭하여 적용)";
                    row.Suggestion.Visibility = Visibility.Visible;
                }
            }
            else
            {
                row.Badge.Text = "🟢";
                row.Badge.ToolTip = "사용 가능";
            }
        }
    }

    private void OnResetHotkeys(object sender, RoutedEventArgs e)
    {
        var defaults = new HotkeySettings();
        foreach (var row in _hotkeyRows)
            row.Box.Value = row.Def.Get(defaults);
        ChkOverridePrintScreen.IsChecked = defaults.OverridePrintScreen;
        RefreshHotkeyConflicts();
    }
```

- [ ] **Step 6: 빌드 확인**

Run: `dotnet build src/ShinCapture/ShinCapture.csproj -c Debug`
Expected: 오류 0개. (경고 CS4014 — 기존, 무관)

- [ ] **Step 7: 커밋**

```bash
git add src/ShinCapture/Views/SettingsWindow.xaml src/ShinCapture/Views/SettingsWindow.xaml.cs
git commit -m "feat: 단축키 설정창 — 키 캡쳐 입력 + 충돌 배지/추천 + 그룹화/복원"
```

---

## Task 6: MainWindow — 시작 실패 알림 + 설정창 연동

**Files:**
- Modify: `src/ShinCapture/Views/MainWindow.xaml.cs`

- [ ] **Step 1: RegisterHotkeys — 실패 수집 + 트레이 알림**

`RegisterHotkeys()` 메서드 본문에서, 현재 11개 등록 호출(78~92행)을 아래로 교체. 라벨과 함께 실패를 모아 끝에서 한 번 알린다.
```csharp
        // PrintScreen 등록
        var failures = new System.Collections.Generic.List<string>();
        void Reg(string label, string hotkey, CaptureMode mode)
        {
            if (string.IsNullOrWhiteSpace(hotkey)) return;
            if (_hotkeyManager.Register(hotkey, () => StartCapture(mode)) < 0)
                failures.Add($"{label}({hotkey})");
        }

        Reg("영역지정", _settings.Hotkeys.RegionCapture, CaptureMode.Region);
        Reg("영역지정(보조)", _settings.Hotkeys.RegionCaptureAlt, CaptureMode.Region);
        Reg("자유형", _settings.Hotkeys.FreeformCapture, CaptureMode.Freeform);
        Reg("창 캡쳐", _settings.Hotkeys.WindowCapture, CaptureMode.Window);
        Reg("단위영역", _settings.Hotkeys.ElementCapture, CaptureMode.Element);
        Reg("전체화면", _settings.Hotkeys.FullscreenCapture, CaptureMode.Fullscreen);
        Reg("스크롤", _settings.Hotkeys.ScrollCapture, CaptureMode.Scroll);
        Reg("지정사이즈", _settings.Hotkeys.FixedSizeCapture, CaptureMode.FixedSize);
        Reg("텍스트 캡쳐", _settings.Hotkeys.TextCapture, CaptureMode.Text);
        Reg("텍스트+번역", _settings.Hotkeys.TranslateCapture, CaptureMode.Translate);
        Reg("스마트 컷", _settings.Hotkeys.SmartCutCapture, CaptureMode.SmartCut);

        if (failures.Count > 0)
        {
            _trayIcon.ShowBalloonTip(5000, "신캡쳐 — 단축키 충돌",
                $"다른 프로그램과 겹쳐 비활성된 단축키: {string.Join(", ", failures)}. 설정에서 변경하세요.",
                System.Windows.Forms.ToolTipIcon.Warning);
        }
```

주의: 이 교체는 `PrintScreenOverrideService.Apply(...)`(75행) **다음**부터 적용. `Apply` 호출 줄은 유지.

- [ ] **Step 2: OpenSettings — 열기 전 UnregisterAll + HotkeyManager 전달**

`OpenSettings()`(442~448행)를 교체:
```csharp
    private void OpenSettings()
    {
        // 설정창이 열려 있는 동안은 전역 단축키를 해제해야 OS 충돌 프로브가 자기 자신과 충돌하지 않는다.
        _hotkeyManager.UnregisterAll();
        var window = new SettingsWindow(_settingsManager, _hotkeyManager);
        window.Owner = this;
        window.ShowDialog();
        // 설정 반영 + 단축키 재등록
        _settings = _settingsManager.Load();
        RegisterHotkeys();
    }
```

- [ ] **Step 3: 빌드 확인**

Run: `dotnet build src/ShinCapture/ShinCapture.csproj -c Debug`
Expected: 오류 0개.

- [ ] **Step 4: 전체 테스트 (회귀 없음 확인)**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj -c Debug`
Expected: PASS — 기존 87 + 신규 12 = 99 통과.

- [ ] **Step 5: 커밋**

```bash
git add src/ShinCapture/Views/MainWindow.xaml.cs
git commit -m "feat: 단축키 등록 실패 트레이 알림 + 설정창 열림 중 전역키 해제"
```

---

## Task 7: 수동 검증 + 마무리

**Files:** 없음 (실행 검증)

- [ ] **Step 1: 릴리즈 빌드 실행**

Run: `dotnet build ShinCapture.sln -c Debug` → 오류 0개 확인 후 `src/ShinCapture/bin/Debug/.../ShinCapture.exe` 실행.

- [ ] **Step 2: 수동 검증 체크리스트**

- [ ] 트레이 → 설정 → 단축키 탭: 기본 6행(영역/영역보조/전체화면/창/스크롤/스마트컷) 보이고 '고급'은 접혀 있음.
- [ ] '고급' 펼치면 5행(자유형/단위영역/지정사이즈/텍스트/텍스트+번역).
- [ ] 행 클릭 → "키를 누르세요…" → `Ctrl+Shift+G` 누르면 그대로 표시. Esc=취소, ✕=비우기.
- [ ] 두 행에 같은 조합 입력 → 둘 중 하나에 🟡 + "…와 겹쳐요" 툴팁.
- [ ] 흔한 OS 점유 조합(예: `Alt+Tab`은 불가하니 `Ctrl+Alt+Delete` 대신, 다른 앱이 잡은 조합) 입력 → 🔴 + "↳ 추천: …" 클릭 시 적용.
- [ ] '기본값으로 복원' → 모든 행이 기본값으로.
- [ ] 저장 후 재시작 → 변경된 단축키 동작.
- [ ] 일부러 다른 앱이 쓰는 조합으로 저장하고 재시작 → 트레이 풍선 "단축키 충돌" 1회 표시.

- [ ] **Step 3: 메모리/버전 메모(선택)**

검증 완료 후 `project_shincapture.md` 메모리의 버전/기능 노트를 최신화(예: v1.4.0 단축키 UX 개선)할지 사용자와 확인.

---

## Self-Review

**Spec coverage:**
- 3.1 키 누르기 입력 → Task 4 (HotkeyCaptureBox) ✓
- 3.2 충돌 감지+추천 → Task 2(로직)+Task 3(프로브)+Task 5(배선) ✓
- 3.3 시작 실패 알림 → Task 6 Step 1 ✓
- 3.4 그룹화+복원 → Task 5 (기본/고급 패널, OnResetHotkeys) ✓
- 4 격리(순수↔UI/Win32) → Task 1/2 순수 + 나머지 얇게 ✓
- 6 데이터흐름(열림 시 UnregisterAll, 닫힘 시 재등록) → Task 6 Step 2 ✓
- 9 마이그레이션 없음 → 문자열 포맷·기본값·Register 경로 유지 ✓

**Placeholder scan:** 없음 — 모든 스텝에 실제 코드/명령/기대결과.

**Type consistency:** `Value`/`ValueCommitted`(HotkeyCaptureBox), `IsAvailable`(HotkeyManager), `Format`/`IsModifierKey`/`IsValidGlobalHotkey`(HotkeyInput), `Normalize`/`FindInternalConflict`/`SuggestAlternative`(HotkeyConflicts), `HotkeyDef.Get/Set`, `HotkeyRow.Box/Badge/Suggestion/SuggestedValue` — Task 간 일치 확인.

**SettingsWindow 생성자 호출처 (3곳, 검증됨):**
- `MainWindow.xaml.cs:442` (OpenSettings) → Task 6에서 `new SettingsWindow(_settingsManager, _hotkeyManager)`로 변경.
- `ApiKeyHelpWindow.xaml.cs:35` → `new SettingsWindow(_settingsManager, initialTabIndex: 5)` — **named 인자라 변경 불필요**. `hotkeyManager`는 기본 null.
- `EditorWindow.xaml.cs:1839` → `new SettingsWindow(_settingsManager, initialTabIndex: 3)` — **named 인자라 변경 불필요**. `hotkeyManager`는 기본 null.

새 시그니처 `(SettingsManager settingsManager, HotkeyManager? hotkeyManager = null, int initialTabIndex = 0)`. 뒤 두 호출처는 MainWindow가 `UnregisterAll`을 하지 않는 진입점이므로 `hotkeyManager=null`(OS 프로브 비활성, `IsAvailable ?? true`)이 **의도된 올바른 동작** — 내부충돌 배지는 정상, OS 자기충돌 오탐만 방지. 탭 순서(일반0·캡쳐1·저장2·단축키3·지정사이즈4·AI5)는 유지되므로 `initialTabIndex` 값 의미 불변.
