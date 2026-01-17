# Repository Guidelines

## Project Structure & Module Organization
- `Jellyfin.Plugin.TorrentDownloader/` contains the plugin source.
  - `Configuration/`, `Controllers/`, `Models/`, `Services/` hold the core logic.
  - `Web/` contains embedded UI assets (HTML/JS/CSS).
  - `Plugin.cs` is the main plugin entry point.
- `Jellyfin.Plugin.TorrentDownloader.Tests/` is the test project.
- `Jellyfin.Plugin.TorrentDownloader.sln` is the solution root.

## Build, Test, and Development Commands
- `dotnet restore` installs dependencies for the solution.
- `dotnet build --configuration Release` builds the plugin.
- `dotnet test` runs the xUnit test project (add tests as you implement them).
- `dotnet publish Jellyfin.Plugin.TorrentDownloader/Jellyfin.Plugin.TorrentDownloader.csproj --configuration Release --output ./bin/Release/net8.0/publish` produces a deployable plugin bundle.

## Coding Style & Naming Conventions
- Language: C# (.NET 8) with nullable reference types enabled.
- Keep types and members in PascalCase; locals in camelCase.
- Follow the existing folder conventions (e.g., controllers in `Controllers/`, services in `Services/`).
- Favor small, focused classes; log with `ILogger<T>` for operational events.

## Testing Guidelines
- Frameworks: xUnit with Moq and FluentAssertions.
- Place tests in `Jellyfin.Plugin.TorrentDownloader.Tests/` and name files `*Tests.cs`.
- There is no established coverage target yet; add unit tests for new services and controllers.

## Commit & Pull Request Guidelines
- Commits currently follow a conventional pattern (e.g., `feat(core): add initial Jellyfin torrent downloader plugin implementation`).
- PRs should include a concise summary, test command output (or note if manual testing was used), and any relevant configuration or API changes.
- If UI changes are made under `Web/`, include screenshots or a short GIF.

## Security & Configuration Notes
- Validate torrent inputs and sanitize file paths.
- Keep admin-only access checks in place for API endpoints.
