**OrganizadorCapitulos.Maui**

![build](https://img.shields.io/badge/build-local-lightgrey) ![license](https://img.shields.io/badge/license-MIT-blue)

![App screenshot](Resources/Images/organizadorcapitulos.png)

OrganizadorCapitulos.Maui is a Windows desktop application (Blazor embedded in .NET MAUI) that helps you organize, rename and move episode/chapter video files quickly and safely. The app pairs a modern UI with reusable core logic in `SharedLogic` and an auxiliary Python script for filename cleaning.

Short description:
- What it does: scans folders, detects video files, proposes cleaned filenames (optionally assisted by AI), renames and moves files while avoiding collisions.
- UI: built with .NET MAUI + BlazorWebView (Razor components) for a modern, responsive experience.
- Architecture: core logic lives in `SharedLogic` (services, strategies, entities) while the MAUI layer handles orchestration and the user interface.

Key features
- Drag & drop and folder browser for loading files.
- Filename analysis and cleaning (local algorithms + Python helper: `Python/ai_service.py`).
- AI suggestion hook — infrastructure prepared to call a Python/external service (a stub exists at `SharedLogic/Infrastructure/Services/PythonAIService.cs`).
- Safe rename & move with collision detection and unique name generation (`SharedLogic/Application/Services/FileOrganizerService.cs`).
- Undo/Redo support via `UndoRedoService`.
- TMDB lookup integration for metadata.

Technologies
- Target platform: Windows only (project configured to build for `net9.0-windows10.0.19041.0`).
- Runtime / UI: .NET MAUI (BlazorWebView) on Windows
- Libraries: CommunityToolkit.Maui, CommunityToolkit.Mvvm
- Optional helper script: Python 3 (`Python/ai_service.py`)

Relevant files
- Project file: [OrganizadorCapitulos.Maui.csproj](OrganizadorCapitulos.Maui.csproj#L1)
- File organization logic: [SharedLogic/Application/Services/FileOrganizerService.cs](SharedLogic/Application/Services/FileOrganizerService.cs#L1)
- Main ViewModel / UI flow: [ViewModels/HomeViewModel.cs](ViewModels/HomeViewModel.cs#L1)
- Main Blazor page: [Components/Pages/Home.razor](Components/Pages/Home.razor#L1)
- Filename cleaning script: [Python/ai_service.py](Python/ai_service.py#L1)

Requirements
- .NET 9 SDK and the MAUI workloads installed (Visual Studio with MAUI workload or `dotnet workload install maui`).
- Python 3 if you want to run or develop the filename cleaning helper.

Quick start (Windows / development)
1. Open the project in Visual Studio (with MAUI workload) or use the CLI.
2. Restore packages and build:

```
dotnet restore
dotnet build
```

3. Run the app (CLI example):

```
dotnet run --project OrganizadorCapitulos.Maui.csproj
```

Publishing (Windows)

Publish a Release build for Windows 10+:

```
dotnet publish -f net9.0-windows10.0.19041.0 -c Release -r win10-x64 --self-contained false -o ./publish/windows
```

To create an MSIX package and publish to the Microsoft Store, use Visual Studio: `Publish` → `Create App Packages` and follow the wizard. See the official MAUI documentation for more details.

How to use the Python helper (optional)

```
python Python/ai_service.py --help
```

The project copies `Python/ai_service.py` to the output directory to simplify external calls (see `OrganizadorCapitulos.Maui.csproj`).

Extensibility and integration points

- `IAIService` / `PythonAIService` (`SharedLogic/Infrastructure/Services`): implement `IAIService` to connect to the Python script or another external AI service. The current stub returns `null` and `IsAvailable()` = `false`.
- `IMetadataService` (implementations under `OrganizadorCapitulos.Maui/Services`): used for TMDB metadata. Swap or extend to integrate other providers.
- `RenameStrategyFactory` and strategies (`SharedLogic/Application/Strategies`): add or modify renaming rules.
- `FileOrganizerService` (`SharedLogic/Application/Services/FileOrganizerService.cs`): main flow for loading, renaming and moving files — a good place to add instrumentation and validation.

Python integration recommendations

- Call `Python/ai_service.py` from .NET using `System.Diagnostics.Process` or by running a small local HTTP server in Python and communicating over HTTP/JSON.
- Ensure robust error handling and timeouts; use `IsAvailable()` as a gate for UI availability.

Contributing

- Open issues or propose improvements.
- For changes to renaming logic, edit `SharedLogic` services/strategies and add tests.
- To add a real AI integration, implement `IAIService` and update the UI to reflect availability and settings.

License

- This project is released under the MIT License — see `LICENSE`.
