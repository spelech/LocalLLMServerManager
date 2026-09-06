# Dependabot Setup, NuGet Updates, and MCP 2026-07-28 Spec Migration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Configure GitHub Dependabot for automatic dependency updates across all package ecosystems, perform a sweep of NuGet package version updates, migrate MCP documentation and tests to the 2026-07-28 stateless specification, and submit the work as a pull request on a dedicated feature branch.

**Architecture:** Automated dependency scanning is enabled via `.github/dependabot.yml` covering `nuget`, `npm`, `github-actions`, and `docker`. Outdated .NET dependencies across all 4 solution projects are brought up to current stable patch releases with version bump to 3.12.1. The MCP endpoint and documentation are updated to adhere to the official 2026-07-28 stateless MCP specification with test coverage validating sessionless JSON-RPC 2.0 communication.

**Tech Stack:** .NET 10 LTS, C# 13, Avalonia 12.1.2, ModelContextProtocol.AspNetCore 2.2.0, xUnit v3, GitHub Actions, Dependabot, TypeScript, ESLint.

## Global Constraints
- Always run linting (`npm run lint`) and typechecking (`npx tsc --noEmit`) after making code changes.
- All unit and integration tests must pass cleanly.
- Changes must be implemented on a dedicated feature branch, pushed to origin, and submitted via PR.

---

### Task 1: Setup GitHub Dependabot Configuration

**Files:**
- Create: `.github/dependabot.yml`

- [ ] **Step 1: Create `.github/dependabot.yml` with multi-ecosystem coverage**
- [ ] **Step 2: Validate yaml syntax and structure**

---

### Task 2: Perform NuGet Package Updates Sweep & Version Bump

**Files:**
- Modify: `LocalLLMServerManager.csproj`
- Modify: `LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj`
- Modify: `LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj`
- Modify: `LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`

- [ ] **Step 1: Update package references in all 4 .csproj files to latest stable versions**
- [ ] **Step 2: Bump project version from 3.12.0 to 3.12.1 in project files**
- [ ] **Step 3: Restore dependencies and build solution**

---

### Task 3: Migrate to 2026-07-28 MCP Specification

**Files:**
- Modify: `README.md`
- Modify: `docs/REQUIREMENTS.md`
- Modify: `Endpoints/McpEndpoints.cs`
- Modify: `LocalLLMServerManager.Tests/McpServerIntegrationTests.cs`

- [ ] **Step 1: Update MCP documentation to reference the 2026-07-28 stateless specification**
- [ ] **Step 2: Add test cases verifying 2026-07-28 stateless MCP interaction and `_meta` request payloads**
- [ ] **Step 3: Run MCP test suite to verify all test cases pass**

---

### Task 4: Full Verification Suite

**Files:**
- Verify entire repository

- [ ] **Step 1: Execute `dotnet test` on the test suite**
- [ ] **Step 2: Run `npm run lint` and `npx tsc --noEmit`**

---

### Task 5: Commit, Push, and Create Pull Request

- [ ] **Step 1: Stage all changed files and commit**
- [ ] **Step 2: Push feature branch to origin**
- [ ] **Step 3: Create pull request using `gh pr create`**
