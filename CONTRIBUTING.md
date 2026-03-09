# Contributing to Invoicetronic Desk

Thank you for your interest in contributing! This document explains how to get started.

## Reporting Issues

- Search [existing issues](https://github.com/invoicetronic/desk/issues) before opening a new one.
- For bugs, include: steps to reproduce, expected vs actual behavior, and your environment (OS, .NET version, Docker version if applicable).
- For feature requests, describe the use case and why it would benefit the project.

## Development Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (only for Playwright E2E tests)

### Getting started

```bash
git clone https://github.com/invoicetronic/desk.git
cd desk
cp src/desk.yml.example src/desk.yml   # edit with your API key
dotnet run --project src/
```

The app will be available at `http://localhost:5100`.

### Running tests

```bash
# Unit and integration tests
dotnet test tests/Unit/

# E2E tests (requires Playwright browsers)
pwsh tests/E2E/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/E2E/
```

All tests must pass before submitting a pull request.

## Pull Requests

1. Fork the repository and create a branch from `main`.
2. Keep changes focused — one concern per PR.
3. Follow the existing code style (see below).
4. Add or update tests for any new or changed behavior.
5. Run `dotnet test` and make sure all tests pass.
6. Write a clear PR description explaining *what* and *why*.

### Code Style

- C# 13 / .NET 10 idioms: collection expressions, target-typed `new`, pattern matching.
- Remove unused `using` directives.
- Use `[ConfigurationKeyName]` for YAML config binding (snake_case keys).
- Localize all user-facing strings via `.resx` files (`Resources/SharedResource.{it,en}.resx`).
- Prefer `async`/`await` throughout — never use `.Result` or `.Wait()`.

### Commit Messages

Use a short prefix: `fix:`, `feat:`, `test:`, `docs:`, `ci:`, `refactor:`.

```
fix: prevent deadlock in dashboard data loading
feat: add CSV export option
test: add unit tests for EmailService
```

## Project Structure

```
src/                    Application source code
  Pages/                Razor Pages (UI)
  Areas/Identity/       ASP.NET Core Identity pages
  Resources/            Localization .resx files
  Artifacts/            Email templates
  wwwroot/              Static assets (CSS, JS)
tests/
  Unit/                 Unit and integration tests (xUnit)
  E2E/                  Playwright end-to-end tests
```

## License

By contributing, you agree that your contributions will be licensed under the [Apache License 2.0](LICENSE).
