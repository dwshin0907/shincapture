# Editor Premium Content Card Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore one polished Naver Premium Content promotion in the editor without disrupting editing commands or overwriting existing release artifacts.

**Architecture:** Keep link copy and URL in a small editor catalog that can be unit-tested. Render one fixed card at the bottom of the history panel and expose the same action from the More menu; both route through one guarded URL-opening handler.

**Tech Stack:** C# 12, .NET 8, WPF XAML, xUnit, Inno Setup 6

---

### Task 1: Premium content catalog

**Files:**
- Create: `src/ShinCapture/Editor/EditorPremiumContentCatalog.cs`
- Create: `tests/ShinCapture.Tests/Editor/EditorPremiumContentCatalogTests.cs`

- [ ] **Step 1: Write the failing catalog test**

```csharp
using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public class EditorPremiumContentCatalogTests
{
    [Fact]
    public void DefinesStableNaverPremiumContentMetadata()
    {
        Assert.Equal("AI 실전 활용법", EditorPremiumContentCatalog.Title);
        Assert.Equal("네이버 프리미엄콘텐츠에서 보기", EditorPremiumContentCatalog.Description);
        Assert.Equal("https", EditorPremiumContentCatalog.ChannelUri.Scheme);
        Assert.Equal("contents.premium.naver.com", EditorPremiumContentCatalog.ChannelUri.Host);
    }
}
```

- [ ] **Step 2: Run the test and verify the missing type failure**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj -c Release --filter FullyQualifiedName~EditorPremiumContentCatalogTests`

Expected: FAIL because `EditorPremiumContentCatalog` does not exist.

- [ ] **Step 3: Implement the catalog**

```csharp
namespace ShinCapture.Editor;

public static class EditorPremiumContentCatalog
{
    public const string Title = "AI 실전 활용법";
    public const string Description = "네이버 프리미엄콘텐츠에서 보기";
    public static Uri ChannelUri { get; } = new("https://contents.premium.naver.com/market/ai");
}
```

- [ ] **Step 4: Run the focused test and verify it passes**

Run: `dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj -c Release --filter FullyQualifiedName~EditorPremiumContentCatalogTests`

Expected: 1 passed, 0 failed.

- [ ] **Step 5: Commit the catalog**

```powershell
git add src/ShinCapture/Editor/EditorPremiumContentCatalog.cs tests/ShinCapture.Tests/Editor/EditorPremiumContentCatalogTests.cs
git commit -m "feat: add editor premium content catalog"
```

### Task 2: History-panel promotion and fallback menu action

**Files:**
- Modify: `src/ShinCapture/Themes/LightTheme.xaml`
- Modify: `src/ShinCapture/Views/EditorWindow.xaml`
- Modify: `src/ShinCapture/Views/EditorWindow.xaml.cs`

- [ ] **Step 1: Add a focused card style**

Add this style in `LightTheme.xaml`:

```xml
<Style x:Key="PremiumContentCardButton" TargetType="Button">
    <Setter Property="Background" Value="#F0FBF4"/>
    <Setter Property="BorderBrush" Value="#8DE3AD"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="10"/>
    <Setter Property="MinHeight" Value="64"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="FocusVisualStyle" Value="{StaticResource EditorFocusVisual}"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="Card" Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="10" Padding="{TemplateBinding Padding}">
                    <ContentPresenter HorizontalAlignment="Stretch" VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Card" Property="Background" Value="#E2F8EA"/>
                        <Setter TargetName="Card" Property="BorderBrush" Value="#03C75A"/>
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter TargetName="Card" Property="Background" Value="#CFF2DC"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

- [ ] **Step 2: Add the fixed history footer card**

Add this fixed footer before the existing history `ScrollViewer`:

```xml
<Border DockPanel.Dock="Bottom" Padding="8" BorderBrush="{DynamicResource BorderLightBrush}"
        BorderThickness="0,1,0,0">
    <Button Style="{DynamicResource PremiumContentCardButton}"
            Click="OnPremiumContentClick"
            ToolTip="네이버 프리미엄콘텐츠 AI 실전 활용법 채널"
            AutomationProperties.Name="네이버 프리미엄콘텐츠 AI 실전 활용법 열기">
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="30"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <Border Width="26" Height="26" CornerRadius="7" Background="#03C75A">
                <TextBlock Text="N" Foreground="White" FontWeight="Bold"
                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <StackPanel Grid.Column="1" Margin="7,0,4,0">
                <TextBlock Text="AI 실전 활용법" FontSize="11" FontWeight="SemiBold"/>
                <TextBlock Text="프리미엄콘텐츠에서 보기" FontSize="9.5"
                           Foreground="{DynamicResource TextSecondaryBrush}"/>
            </StackPanel>
            <TextBlock Grid.Column="2" Text="→" Foreground="#03A94F"
                       VerticalAlignment="Center"/>
        </Grid>
    </Button>
</Border>
```

- [ ] **Step 3: Add the More-menu fallback**

Insert after the API guide item:

```xml
<Separator/>
<MenuItem Header="추천 콘텐츠 · AI 실전 활용법" Click="OnPremiumContentClick"
          AutomationProperties.Name="네이버 프리미엄콘텐츠 AI 실전 활용법 열기"/>
```

- [ ] **Step 4: Route both entry points through one guarded handler**

```csharp
private void OnPremiumContentClick(object sender, RoutedEventArgs e)
{
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = EditorPremiumContentCatalog.ChannelUri.AbsoluteUri,
            UseShellExecute = true
        });
    }
    catch
    {
        StatusText.Text = "추천 콘텐츠 페이지를 열 수 없습니다";
    }
}
```

- [ ] **Step 5: Build and visually inspect both layouts**

Run: `dotnet build src/ShinCapture/ShinCapture.csproj -c Release --no-restore`

Expected: 0 errors. Render or launch at 1600×900 and 1100×750; the footer must remain below the scrolling cards and the top command bar must remain one row.

- [ ] **Step 6: Commit the UI**

```powershell
git add src/ShinCapture/Themes/LightTheme.xaml src/ShinCapture/Views/EditorWindow.xaml src/ShinCapture/Views/EditorWindow.xaml.cs
git commit -m "feat: restore premium content promotion"
```

### Task 3: Versioned installer without overwrites

**Files:**
- Modify: `src/ShinCapture/ShinCapture.csproj`
- Modify: `installer/setup.iss`

- [ ] **Step 1: Bump both version declarations**

Change `<Version>1.3.10</Version>` and `MyAppVersion "1.3.10"` to `1.3.11`.

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test ShinCapture.sln -c Release --no-restore`

Expected: all tests pass with 0 failures.

- [ ] **Step 3: Publish the application**

Run: `dotnet publish src/ShinCapture/ShinCapture.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:Version=1.3.11 -o publish`

Expected: `publish/ShinCapture.exe` is generated.

- [ ] **Step 4: Refuse overwrite and compile a new installer**

Before compilation, fail if either `dist/ShinCapture_Setup_v1.3.11.exe` or `release/ShinCapture_Setup_v1.3.11.exe` already exists. Then run ISCC with `/DMyAppVersion=1.3.11`, copy the result to `release`, and confirm the two SHA-256 hashes match.

- [ ] **Step 5: Confirm older installers remain unchanged**

List `release/ShinCapture_Setup_v1.3.6.exe` through `v1.3.11.exe` and verify `v1.3.10` still exists with its previous SHA-256 hash.

- [ ] **Step 6: Commit the version bump**

```powershell
git add src/ShinCapture/ShinCapture.csproj installer/setup.iss
git commit -m "chore: bump version to 1.3.11"
```
