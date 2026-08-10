# Modern Glassmorphic Dashboard Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Overhaul the Avalonia Desktop (`LocalLLMServerManager`) and WebAssembly (`LocalLLMServerManager.Web`) dashboard UI/UX into a modern, responsive, glassmorphic experience featuring 1280x840 window stretch, Mica/AcrylicBlur OS transparency, a 3-card Hero telemetry header with stacked VRAM memory visual bar, and custom gradient pill TabControl navigation.

**Architecture:** Update `MainWindow.axaml` window chrome and dimensions; add custom `TabControl` / `TabItem` glass pill templates to `GlassmorphicTheme.axaml`; recreate the web dashboard's 3-card hero row in `TelemetryHeaderControl.axaml`; and refine sub-view controls across `LocalLLMServerManager.Shared`.

**Tech Stack:** C# .NET 10.0, Avalonia 12.1.1, Semi.Avalonia 12.1.0.1, XAML ResourceDictionaries.

## Global Constraints
- Retain 100% test pass rate across `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`.
- Retain clean build output across `LocalLLMServerManager.csproj`, `LocalLLMServerManager.Shared.csproj`, and `LocalLLMServerManager.Web.csproj`.
- Use atomic, descriptive commits per task on branch `feat/beautify-avalonia-dashboard`.

---

### Task 1: Update MainWindow Sizing & Modern Windows Chrome

**Files:**
- Modify: `Views/MainWindow.axaml:1-16`

**Interfaces:**
- Consumes: Avalonia Window properties (`TransparencyLevelHint`, `ExtendClientAreaToDecorationsHint`, `Width`, `Height`, `MinWidth`, `MinHeight`).
- Produces: Borderless glass window chrome with default `1280x840` dimensions.

- [ ] **Step 1: Update MainWindow.axaml window properties**

Update `Views/MainWindow.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:sharedViews="using:LocalLLMServerManager.Shared.Views"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d" d:DesignWidth="1280" d:DesignHeight="840"
        x:Class="LocalLLMServerManager.Views.MainWindow"
        Icon="avares://LocalLLMServerManager/Assets/app-icon.ico"
        Title="Local LLM Server Manager v3.3.0"
        Width="1280" Height="840"
        MinWidth="1024" MinHeight="700"
        WindowStartupLocation="CenterScreen"
        TransparencyLevelHint="Mica, AcrylicBlur, Transient"
        Background="Transparent"
        ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaTitleBarHeightHint="-1">

    <sharedViews:MainView />
</Window>
```

- [ ] **Step 2: Build LocalLLMServerManager.csproj to verify compilation**

Run: `dotnet build LocalLLMServerManager.csproj`
Expected: Build succeeds with 0 errors.

- [ ] **Step 3: Commit MainWindow sizing and window chrome update**

Run:
```bash
git add Views/MainWindow.axaml
git commit -m "feat(ui): update MainWindow sizing to 1280x840 with Mica/AcrylicBlur transparency"
```

---

### Task 2: Build Custom Glass TabControl & Pill Navigation Templates

**Files:**
- Modify: `LocalLLMServerManager.Shared/Styles/GlassmorphicTheme.axaml`

**Interfaces:**
- Consumes: `BgSurfaceBrush`, `PrimaryGradientBrush`, `TextMainBrush`, `TextMutedBrush`, `BorderGlowBrush`.
- Produces: Custom `TabControl` and `TabItem` control templates rendering pill navigation with gradient active state.

- [ ] **Step 1: Add TabControl and TabItem styles to GlassmorphicTheme.axaml**

Append to `LocalLLMServerManager.Shared/Styles/GlassmorphicTheme.axaml`:
```xml
    <!-- Glass TabControl Navigation Bar Style -->
    <Style Selector="TabControl">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="Padding" Value="0" />
    </Style>
    <Style Selector="TabControl /template/ ItemsPresenter#PART_ItemsPresenter">
        <Setter Property="Background" Value="{StaticResource BgSurfaceBrush}" />
        <Setter Property="CornerRadius" Value="12" />
        <Setter Property="Margin" Value="0,0,0,16" />
        <Setter Property="Padding" Value="6" />
    </Style>

    <!-- Glass TabItem Style -->
    <Style Selector="TabItem">
        <Setter Property="Foreground" Value="{StaticResource TextMutedBrush}" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="FontSize" Value="13" />
        <Setter Property="Padding" Value="16,10" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="Margin" Value="2,0" />
        <Setter Property="MinHeight" Value="38" />
    </Style>
    <Style Selector="TabItem:pointerover /template/ ContentPresenter#PART_ContentPresenter">
        <Setter Property="Background" Value="{StaticResource BgCardBrush}" />
        <Setter Property="Foreground" Value="{StaticResource TextMainBrush}" />
    </Style>
    <Style Selector="TabItem:selected">
        <Setter Property="Foreground" Value="#ffffff" />
        <Setter Property="FontWeight" Value="Bold" />
    </Style>
    <Style Selector="TabItem:selected /template/ ContentPresenter#PART_ContentPresenter">
        <Setter Property="Background" Value="{StaticResource PrimaryGradientBrush}" />
        <Setter Property="Foreground" Value="#ffffff" />
        <Setter Property="BoxShadow" Value="0 4 12 0 #408b5cf6" />
    </Style>
```

- [ ] **Step 2: Build project to verify XAML syntax**

Run: `dotnet build LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj`
Expected: Build succeeds with 0 errors.

- [ ] **Step 3: Commit TabControl templates**

Run:
```bash
git add LocalLLMServerManager.Shared/Styles/GlassmorphicTheme.axaml
git commit -m "feat(ui): add glassmorphic TabControl and TabItem control templates"
```

---

### Task 3: Recreate 3-Card Hero Telemetry Header & VRAM Visual Bar

**Files:**
- Modify: `LocalLLMServerManager.Shared/Views/Controls/TelemetryHeaderControl.axaml`

**Interfaces:**
- Consumes: `TelemetryViewModel` properties (`GpuName`, `VramUsedGb`, `VramTotalGb`, `VramPercentage`, `VramStatusText`, `OllamaStatus`, `ForgeStatus`, `ComfyStatus`, `ServiceModeText`).
- Produces: 3-card Hero telemetry row with services status indicators, visual VRAM bar, and orchestrator proxy state.

- [ ] **Step 1: Update TelemetryHeaderControl.axaml layout**

Update `LocalLLMServerManager.Shared/Views/Controls/TelemetryHeaderControl.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
              xmlns:vm="using:LocalLLMServerManager.Shared.ViewModels"
              x:Class="LocalLLMServerManager.Shared.Views.Controls.TelemetryHeaderControl"
              x:DataType="vm:TelemetryViewModel">

    <Grid ColumnDefinitions="*, *, *" Margin="16,16,16,12">
        <!-- CARD 1: Service Health Badges -->
        <Border Grid.Column="0" Classes="glass-card" Margin="0,0,8,0">
            <StackPanel Spacing="8">
                <TextBlock Text="⚡ Engine Health Status" FontSize="13" FontWeight="Bold" Foreground="{StaticResource TextMainBrush}"/>
                <UniformGrid Columns="3">
                    <StackPanel HorizontalAlignment="Center" Spacing="4">
                        <TextBlock Text="Ollama" FontSize="11" Foreground="{StaticResource TextMutedBrush}" HorizontalAlignment="Center"/>
                        <Border Classes="telemetry-pill">
                            <TextBlock Text="{Binding OllamaStatus}" FontSize="11" Foreground="{StaticResource OnlineBrush}" FontWeight="Bold"/>
                        </Border>
                    </StackPanel>
                    <StackPanel HorizontalAlignment="Center" Spacing="4">
                        <TextBlock Text="Forge SD" FontSize="11" Foreground="{StaticResource TextMutedBrush}" HorizontalAlignment="Center"/>
                        <Border Classes="telemetry-pill">
                            <TextBlock Text="{Binding ForgeStatus}" FontSize="11" Foreground="{StaticResource PrimaryBrush}" FontWeight="Bold"/>
                        </Border>
                    </StackPanel>
                    <StackPanel HorizontalAlignment="Center" Spacing="4">
                        <TextBlock Text="ComfyUI" FontSize="11" Foreground="{StaticResource TextMutedBrush}" HorizontalAlignment="Center"/>
                        <Border Classes="telemetry-pill">
                            <TextBlock Text="{Binding ComfyStatus}" FontSize="11" Foreground="{StaticResource AccentBrush}" FontWeight="Bold"/>
                        </Border>
                    </StackPanel>
                </UniformGrid>
            </StackPanel>
        </Border>

        <!-- CARD 2: Hero Stacked VRAM Visual Bar -->
        <Border Grid.Column="1" Classes="glass-card" Margin="4,0,4,0">
            <StackPanel Spacing="6">
                <Grid ColumnDefinitions="*, Auto">
                    <StackPanel Grid.Column="0">
                        <TextBlock Text="🎮 VRAM Memory Allocation" FontSize="13" FontWeight="Bold" Foreground="{StaticResource TextMainBrush}"/>
                        <TextBlock Text="{Binding GpuName}" FontSize="11" Foreground="{StaticResource TextMutedBrush}" Margin="0,2,0,0"/>
                    </StackPanel>
                    <TextBlock Grid.Column="1" Text="{Binding VramStatusText}" FontSize="12" Foreground="{StaticResource PrimaryBrush}" FontWeight="Bold" VerticalAlignment="Center"/>
                </Grid>
                <ProgressBar Value="{Binding VramPercentage}" Maximum="100" Classes="glass-progress" Height="12"/>
            </StackPanel>
        </Border>

        <!-- CARD 3: Orchestrator Proxy & Actions -->
        <Border Grid.Column="2" Classes="glass-card" Margin="8,0,0,0">
            <Grid ColumnDefinitions="*, Auto">
                <StackPanel Grid.Column="0" Spacing="4">
                    <TextBlock Text="🌐 Reverse Proxy Status" FontSize="13" FontWeight="Bold" Foreground="{StaticResource TextMainBrush}"/>
                    <Border Classes="telemetry-pill" HorizontalAlignment="Left">
                        <TextBlock Text="{Binding ServiceModeText}" FontSize="11" Foreground="{StaticResource SecondaryBrush}" FontWeight="SemiBold"/>
                    </Border>
                </StackPanel>
                <Button Grid.Column="1" Command="{Binding RefreshStatusCommand}" Classes="glass-secondary" VerticalAlignment="Center">
                    🔄 Refresh
                </Button>
            </Grid>
        </Border>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Run test suite to verify no breakage**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`
Expected: 135/135 tests pass.

- [ ] **Step 3: Commit Hero Telemetry Header**

Run:
```bash
git add LocalLLMServerManager.Shared/Views/Controls/TelemetryHeaderControl.axaml
git commit -m "feat(ui): recreate 3-card Hero telemetry row and VRAM visual bar"
```

---

### Task 4: Refine Sub-View Cards & Responsive Grids

**Files:**
- Modify: `LocalLLMServerManager.Shared/Views/Controls/OllamaModelsTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/EngineStudioTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/CivitaiTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/HuggingFaceTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`

**Interfaces:**
- Consumes: ViewModel bindings across sub-controls.
- Produces: Polished 2-column responsive card layouts with high-contrast typography and badges.

- [ ] **Step 1: Update OllamaModelsTabControl.axaml for 2-column card grid**

Update `OllamaModelsTabControl.axaml` to wrap model items in a responsive grid container with pill tags.

- [ ] **Step 2: Update EngineStudioTabControl.axaml for side-by-side engine studio cards**

Update `EngineStudioTabControl.axaml` with elevated cards and glowing launch buttons.

- [ ] **Step 3: Run full solution build & tests**

Run: `dotnet build LocalLLMServerManager.sln`
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`
Expected: Build succeeds with 0 errors; 135/135 tests pass.

- [ ] **Step 4: Commit sub-view card refinements**

Run:
```bash
git add LocalLLMServerManager.Shared/Views/Controls/*.axaml
git commit -m "feat(ui): apply responsive glass grid cards across all tab view controls"
```

---

## Verification Plan

### Automated Tests
- Run `dotnet build LocalLLMServerManager.sln`
- Run `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`

### Manual Verification
- Launch `LocalLLMServerManager` desktop executable.
- Verify 1280x840 window stretch with Mica/AcrylicBlur titlebar.
- Verify 3-card Hero telemetry header (Services health, VRAM visual bar, Proxy status).
- Verify gradient glass pill `TabControl` navigation across all 5 tabs.
