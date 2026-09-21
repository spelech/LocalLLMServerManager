# Avalonia.LayoutInspector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create `Avalonia.LayoutInspector`, a standalone C# library and NuGet package for automated layout inspection, boundary overflow detection, sibling collision auditing, touch target ergonomics validation, and responsive viewport sweeping, and integrate it into `LocalLLMServerManager` while deprecating the old WASM playwright inspector.

**Architecture:** 
1. Standalone multi-target library (`net8.0;net9.0;net10.0`) in `C:\Users\Alias\repos\Avalonia.LayoutInspector` built with modern `.slnx` and `Directory.Build.props`.
2. Core geometric coordinate engine (`VisualBoundsResolver`) mapping Avalonia visual bounds into root coordinate space.
3. Modular rules pipeline (`BoundaryOverflowRule`, `SiblingCollisionRule`, `TargetErgonomicsRule`, `TextClippingRule`) and 0-100 UX scoring engine.
4. Responsive viewport sweeper (`ResponsiveAuditRunner`) and fluent assertions (`ShouldHaveNoLayoutViolations`, `ShouldFitResponsiveBreakpoints`).
5. Live diagnostic adorner overlay (`LayoutInspectorOverlay`) and headless test suite with $\ge 80\%$ test coverage.
6. Integration into `LocalLLMServerManager.Tests` to identify UI layout issues and removal of `playwright-layout-inspector`.

**Tech Stack:** C# 13 / .NET 10 (multi-targeting net8.0, net9.0, net10.0), Avalonia UI 11.2, Avalonia.Headless.XUnit, xUnit.

## Global Constraints

- Repository target directory: `C:\Users\Alias\repos\Avalonia.LayoutInspector`.
- Core library must multi-target `net8.0;net9.0;net10.0`.
- Nullable reference types enabled (`<Nullable>enable</Nullable>`) and warnings as errors.
- Test coverage $\ge 80\%$ verified via `dotnet test`.
- All rules follow ASD-STE100 principles for reporting and diagnostics.
- Follow AgenticEngineeringToolbelt repository standards (AGENTS.md, Git Flow, atomic commits).

---

### Task 1: Scaffold `Avalonia.LayoutInspector` Repository & Solution

**Files:**
- Create: `C:\Users\Alias\repos\Avalonia.LayoutInspector/Directory.Build.props`
- Create: `C:\Users\Alias\repos\Avalonia.LayoutInspector/Avalonia.LayoutInspector.slnx`
- Create: `C:\Users\Alias\repos\Avalonia.LayoutInspector/AGENTS.md`
- Create: `C:\Users\Alias\repos\Avalonia.LayoutInspector/README.md`
- Create: `C:\Users\Alias\repos\Avalonia.LayoutInspector/LICENSE`
- Create: `C:\Users\Alias\repos\Avalonia.LayoutInspector/src/Avalonia.LayoutInspector/Avalonia.LayoutInspector.csproj`
- Create: `C:\Users\Alias\repos\Avalonia.LayoutInspector/tests/Avalonia.LayoutInspector.Tests/Avalonia.LayoutInspector.Tests.csproj`

**Interfaces:**
- Produces: Compiled multi-target solution ready for engine classes and test execution.

- [ ] **Step 1: Create repository directory and Directory.Build.props**

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>13.0</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Version>1.0.0</Version>
    <Authors>Steven T. Pelech</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/spelech/Avalonia.LayoutInspector</PackageProjectUrl>
    <RepositoryUrl>https://github.com/spelech/Avalonia.LayoutInspector</RepositoryUrl>
    <PackageTags>avalonia;layout;inspector;ui-testing;headless;ergonomics;responsive</PackageTags>
    <Description>Automated layout auditing, overflow detection, sibling collision checking, and responsive viewport inspection for Avalonia UI.</Description>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create Avalonia.LayoutInspector.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.2.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create Avalonia.LayoutInspector.Tests.csproj and TestAppBuilder**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.2.0" />
    <PackageReference Include="Avalonia.Headless.XUnit" Version="11.2.0" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.1">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Avalonia.LayoutInspector\Avalonia.LayoutInspector.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create solution and smoke test project build**

Run: `dotnet build Avalonia.LayoutInspector.slnx`
Expected: Build succeeded with 0 warnings and 0 errors.

- [ ] **Step 5: Initialize Git repository and commit baseline**

```bash
git init
git checkout -b main
git add .
git commit -m "chore: initial scaffold of Avalonia.LayoutInspector solution"
```

---

### Task 2: Core Models, Interfaces & VisualBoundsResolver

**Files:**
- Create: `src/Avalonia.LayoutInspector/Models/ViolationSeverity.cs`
- Create: `src/Avalonia.LayoutInspector/Models/LayoutViolation.cs`
- Create: `src/Avalonia.LayoutInspector/Models/AuditReport.cs`
- Create: `src/Avalonia.LayoutInspector/Models/AuditOptions.cs`
- Create: `src/Avalonia.LayoutInspector/Rules/ILayoutAuditRule.cs`
- Create: `src/Avalonia.LayoutInspector/Engine/IVisualBoundsResolver.cs`
- Create: `src/Avalonia.LayoutInspector/Engine/VisualBoundsResolver.cs`
- Test: `tests/Avalonia.LayoutInspector.Tests/Engine/VisualBoundsResolverTests.cs`

**Interfaces:**
- Produces: `VisualBoundsResolver` mapping any visual to root coordinates with layout pass execution and visibility filtering.

- [ ] **Step 1: Write failing unit test for VisualBoundsResolver**

```csharp
[AvaloniaFact]
public void ResolveBounds_NestedControl_ReturnsExpectedRootCoordinates()
{
    var resolver = new VisualBoundsResolver();
    var innerButton = new Button { Width = 100, Height = 40 };
    var container = new Border { Margin = new Thickness(20), Child = innerButton };
    var window = new Window { Content = container, Width = 400, Height = 300 };
    window.Show();

    var bounds = resolver.GetRootBounds(innerButton, window);
    Assert.True(bounds.HasValue);
    Assert.Equal(20, bounds.Value.X);
    Assert.Equal(20, bounds.Value.Y);
    Assert.Equal(100, bounds.Value.Width);
    Assert.Equal(40, bounds.Value.Height);
    window.Close();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~VisualBoundsResolverTests"`
Expected: FAIL (types not found)

- [ ] **Step 3: Implement VisualBoundsResolver and models**

```csharp
public class VisualBoundsResolver : IVisualBoundsResolver
{
    public Rect? GetRootBounds(Visual visual, Visual root)
    {
        if (!visual.IsVisible) return null;
        if (visual.Bounds.Width <= 0 || visual.Bounds.Height <= 0)
        {
            if (visual is Layoutable layoutable) layoutable.UpdateLayout();
        }
        var transform = visual.TransformToVisual(root);
        if (!transform.HasValue) return null;
        return new Rect(0, 0, visual.Bounds.Width, visual.Bounds.Height).TransformToAABB(transform.Value);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~VisualBoundsResolverTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "feat(engine): implement VisualBoundsResolver and core layout models"
```

---

### Task 3: Implement Rules Pipeline

**Files:**
- Create: `src/Avalonia.LayoutInspector/Rules/BoundaryOverflowRule.cs`
- Create: `src/Avalonia.LayoutInspector/Rules/SiblingCollisionRule.cs`
- Create: `src/Avalonia.LayoutInspector/Rules/TargetErgonomicsRule.cs`
- Create: `src/Avalonia.LayoutInspector/Rules/TextClippingRule.cs`
- Test: `tests/Avalonia.LayoutInspector.Tests/Rules/BoundaryOverflowRuleTests.cs`
- Test: `tests/Avalonia.LayoutInspector.Tests/Rules/SiblingCollisionRuleTests.cs`
- Test: `tests/Avalonia.LayoutInspector.Tests/Rules/TargetErgonomicsRuleTests.cs`
- Test: `tests/Avalonia.LayoutInspector.Tests/Rules/TextClippingRuleTests.cs`

**Interfaces:**
- Produces: Specialized `ILayoutAuditRule` implementations detecting overflow, collisions, target size violations, and text clipping.

- [ ] **Step 1: Write failing tests for BoundaryOverflowRule and SiblingCollisionRule**

```csharp
[AvaloniaFact]
public void BoundaryOverflowRule_ChildBleedingBeyondContainer_DetectsViolation()
{
    var child = new Border { Width = 500, Height = 100 };
    var parent = new Border { Width = 300, Height = 100, Child = child };
    var window = new Window { Content = parent, Width = 600, Height = 400 };
    window.Show();

    var rule = new BoundaryOverflowRule();
    var violations = rule.Evaluate(window, new VisualBoundsResolver(), new AuditOptions()).ToList();
    Assert.Contains(violations, v => v.RuleId == "LAYOUT001_OVERFLOW");
    window.Close();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~BoundaryOverflowRuleTests"`
Expected: FAIL

- [ ] **Step 3: Implement BoundaryOverflowRule, SiblingCollisionRule, TargetErgonomicsRule, TextClippingRule**

Implement container bounds checking, ScrollViewer exemptions, Grid row/column matching for collisions, interactive element hitbox auditing, and text trimming detection.

- [ ] **Step 4: Run all rule tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~RuleTests"`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "feat(rules): implement boundary overflow, sibling collision, ergonomics, and text clipping rules"
```

---

### Task 4: Implement LayoutAuditor & Scoring Engine

**Files:**
- Create: `src/Avalonia.LayoutInspector/Engine/ILayoutAuditor.cs`
- Create: `src/Avalonia.LayoutInspector/Engine/LayoutAuditor.cs`
- Create: `src/Avalonia.LayoutInspector/Engine/LayoutScoreCalculator.cs`
- Test: `tests/Avalonia.LayoutInspector.Tests/Engine/LayoutAuditorTests.cs`

**Interfaces:**
- Produces: `LayoutAuditor.Audit(visual, options)` returning an `AuditReport` with detailed violations, recommendations, and 0-100 UX score.

- [ ] **Step 1: Write failing test for LayoutAuditor end-to-end audit**

```csharp
[AvaloniaFact]
public void LayoutAuditor_CleanView_Returns100ScoreAndZeroViolations()
{
    var panel = new StackPanel
    {
        Children =
        {
            new Button { Content = "Save", Width = 100, Height = 32 },
            new Button { Content = "Cancel", Width = 100, Height = 32 }
        }
    };
    var window = new Window { Content = panel, Width = 400, Height = 300 };
    window.Show();

    var auditor = new LayoutAuditor();
    var report = auditor.Audit(window);
    Assert.True(report.IsClean);
    Assert.Equal(100, report.HealthScore);
    window.Close();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~LayoutAuditorTests"`
Expected: FAIL

- [ ] **Step 3: Implement LayoutAuditor and LayoutScoreCalculator**

Wire up the default rule pipeline, tree traversal, violation aggregation, and markdown table summary formatting (`ToDetailedReport()`).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~LayoutAuditorTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "feat(engine): implement LayoutAuditor and layout health scoring engine"
```

---

### Task 5: Implement Responsive Sweeper & Fluent Assertions

**Files:**
- Create: `src/Avalonia.LayoutInspector/Responsive/Breakpoint.cs`
- Create: `src/Avalonia.LayoutInspector/Responsive/StandardBreakpoints.cs`
- Create: `src/Avalonia.LayoutInspector/Responsive/ResponsiveAuditRunner.cs`
- Create: `src/Avalonia.LayoutInspector/Responsive/ResponsiveAuditReport.cs`
- Create: `src/Avalonia.LayoutInspector/Assertions/LayoutAssertExtensions.cs`
- Test: `tests/Avalonia.LayoutInspector.Tests/Responsive/ResponsiveAuditRunnerTests.cs`
- Test: `tests/Avalonia.LayoutInspector.Tests/Assertions/LayoutAssertExtensionsTests.cs`

**Interfaces:**
- Produces: `ShouldHaveNoLayoutViolations()`, `ShouldFitResponsiveBreakpoints()` extension methods and multi-breakpoint sweeper.

- [ ] **Step 1: Write failing test for responsive sweeper**

```csharp
[AvaloniaFact]
public void ShouldFitResponsiveBreakpoints_ResponsiveView_ExecutesAllBreakpoints()
{
    var window = new Window
    {
        Content = new Grid { Width = 300, Height = 200 }
    };
    var report = window.ShouldFitResponsiveBreakpoints(new[]
    {
        new Breakpoint("1080p", 1920, 1080),
        new Breakpoint("Mobile", 412, 915)
    });
    Assert.Equal(2, report.BreakpointResults.Count);
    Assert.True(report.AllPassed);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ResponsiveAuditRunnerTests"`
Expected: FAIL

- [ ] **Step 3: Implement ResponsiveAuditRunner and LayoutAssertExtensions**

Implement breakpoint sweeping, window resizing, layout pass execution, and fluent assertion exceptions.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ResponsiveAuditRunnerTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "feat(responsive): implement responsive viewport sweeper and fluent test assertions"
```

---

### Task 6: Implement In-App Diagnostic Overlay & HUD

**Files:**
- Create: `src/Avalonia.LayoutInspector/Overlay/LayoutInspectorOverlay.cs`
- Test: `tests/Avalonia.LayoutInspector.Tests/Overlay/LayoutInspectorOverlayTests.cs`

**Interfaces:**
- Produces: Lightweight adorner overlay drawing colored bounding box outlines for violations and on-screen HUD badge.

- [ ] **Step 1: Write failing test for LayoutInspectorOverlay**

```csharp
[AvaloniaFact]
public void LayoutInspectorOverlay_AttachToTopLevel_RendersWithoutErrors()
{
    var window = new Window { Width = 800, Height = 600 };
    window.Show();
    var overlay = LayoutInspectorOverlay.Attach(window);
    Assert.NotNull(overlay);
    window.Close();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~LayoutInspectorOverlayTests"`
Expected: FAIL

- [ ] **Step 3: Implement LayoutInspectorOverlay**

Build custom adorner canvas drawing collision boxes in red, overflow boxes in dashed orange, ergonomics in yellow, and HUD badge.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~LayoutInspectorOverlayTests"`
Expected: PASS

- [ ] **Step 5: Run full test suite and pack NuGet package**

Run: `dotnet test`
Run: `dotnet pack -c Release`
Expected: 100% tests pass, `Avalonia.LayoutInspector.1.0.0.nupkg` created.

- [ ] **Step 6: Commit**

```bash
git add src/ tests/
git commit -m "feat(overlay): implement in-app visual diagnostic overlay and pack release"
```

---

### Task 7: Integrate into LocalLLMServerManager & Clean Up Deprecated Tooling

**Files:**
- Modify: `C:\Users\Alias\repos\LocalLLMServerManager/package.json` (remove `playwright-layout-inspector` and `test:layout`)
- Delete: `C:\Users\Alias\repos\LocalLLMServerManager/playwright.config.ts`
- Delete: `C:\Users\Alias\repos\LocalLLMServerManager/tests/layout-inspector/`
- Modify: `C:\Users\Alias\repos\LocalLLMServerManager/LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj` (reference `Avalonia.LayoutInspector`)
- Create: `C:\Users\Alias\repos\LocalLLMServerManager/LocalLLMServerManager.Tests/AvaloniaLayoutAuditTests.cs`

**Interfaces:**
- Consumes: `Avalonia.LayoutInspector` to run native layout audits across `LocalLLMServerManager` controls and detect existing UI bugs.

- [ ] **Step 1: Remove deprecated playwright-layout-inspector from package.json**

Remove `playwright-layout-inspector` dependency and `test:layout` script. Delete `playwright.config.ts` and `tests/layout-inspector/`.

- [ ] **Step 2: Run npm lint and typecheck**

Run: `npm run lint` and `npx tsc --noEmit`
Expected: PASS

- [ ] **Step 3: Add reference to Avalonia.LayoutInspector in LocalLLMServerManager.Tests.csproj**

```xml
<ProjectReference Include="..\..\Avalonia.LayoutInspector\src\Avalonia.LayoutInspector\Avalonia.LayoutInspector.csproj" />
```

- [ ] **Step 4: Create AvaloniaLayoutAuditTests.cs**

Audit `MainWindow`, `CivitaiTabControl`, `HuggingFaceTabControl`, `OllamaModelsTabControl`, and `SettingsTabControl` across `StandardBreakpoints.AllStandard`.

- [ ] **Step 5: Run layout audit tests to capture UI issues**

Run: `dotnet test --filter "FullyQualifiedName~AvaloniaLayoutAuditTests"`
Record any detected layout overlaps, overflows, or clipping issues for diagnosis.

- [ ] **Step 6: Commit LocalLLMServerManager updates**

```bash
git add package.json LocalLLMServerManager.Tests/
git commit -m "chore(tooling): replace playwright-layout-inspector with native Avalonia.LayoutInspector test suite"
```
