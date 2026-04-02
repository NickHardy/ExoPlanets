# N.I.N.A. ExoPlanets Plugin - Copilot Instructions

This is a C# WPF plugin for N.I.N.A. (Nighttime Imaging 'N' Astronomy) that enables exoplanet and variable star observation capabilities.

## Build & Test

**Build the project:**
```bash
dotnet build NINA.Plugin.ExoPlanets.csproj
```

**Build in Release mode:**
```bash
dotnet build NINA.Plugin.ExoPlanets.csproj -c Release
```

**Post-build deployment:**
The `.csproj` includes an automatic post-build target that copies the compiled DLL to `%LOCALAPPDATA%\NINA\Plugins\3.0.0\ExoPlanets\` (Windows only). This happens on successful builds via the `PostBuild` target.

**Note:** There are no unit tests in this repository. Testing is manual via the NINA application.

## Architecture

### Project Structure

- **Model/**: Data classes for exoplanet targets, variable stars, and detection results
  - `ExoPlanet.cs`, `ExoPlanetDeepSkyObject.cs`: Exoplanet data models
  - `VariableStar.cs`, `VSXObjects.cs`: Variable star data
  - `DetectedExoStar.cs`, `ComparisonStarChart.cs`: Detection and analysis results
  - `VoTable.cs`: VO-table parsing for catalog data

- **Sequencer/**: Plugin commands and sequencer instructions
  - **Container/**: Object containers for sequencer
    - `ExoPlanetObjectContainer.cs`: Container for exoplanet targets with retrieval UI
    - `VariableStarObjectContainer.cs`: Container for variable star targets
  - **Conditions/**: Sequencer conditions
    - `TransitCondition.cs`: Check if transit is in progress
  - **Utility/**: Sequencer instructions and helpers
    - `CalculateExposureTime.cs`: Auto-exposure calculation and star detection
    - `WaitForTransit.cs`: Wait for transit observation time
    - `StarDetection.cs`: Detect and annotate stars in images
    - `StarAnnotator.cs`: Mark comparison and variable stars on images

- **Astrometry/**: Astronomical calculations
  - `MoonRiseAndSet.cs`: Moon position calculations

- **Utility/**: General utilities
  - `HttpRequest.cs`: HTTP client for fetching catalog data

- **View/**: WPF UI components
  - `AltitudeChart.xaml.cs`: Visualization of target altitude
  - `Options.xaml.cs`: Plugin settings UI

### Data Flow

1. **Target Selection**: User selects exoplanet/variable star via object container
2. **Catalog Retrieval**: HTTP requests fetch live data from:
   - Exoplanet catalogs (astro.swarthmore.edu, exoclock.space)
   - Variable star catalog (AAVSO VSX)
   - Comparison star data (SIMBAD)
3. **Observation Planning**: Transit times, altitude charts, and exposure times calculated
4. **Image Analysis**: Sequencer instructions detect stars, calculate exposure, verify photometry

### MEF Composition

The plugin uses **Managed Extensibility Framework (MEF)** for composition with NINA:

- `ExoPlanets.cs` exports `IPluginManifest` interface (main plugin entry point)
- Sequencer containers and conditions export NINA interfaces via `[Export]` attributes
- NINA framework injects dependencies like `IProfileService` via `[ImportingConstructor]`

## Key Conventions

### Code Style

- **4-space indentation**, CRLF line endings (see `.editorconfig`)
- **PascalCase** for types, methods, properties; `camelCase` for local variables
- **Interface naming**: Begin with `I` (e.g., `IVariableBrightnessTargetContainer`)
- **Nullable reference types**: Use `#nullable enable` where appropriate
- **Copyright headers**: Include MPL 2.0 license header in each file (see `ExoPlanets.cs` for template)

### Namespace Organization

- All classes in `NINA.Plugin.ExoPlanets` or child namespaces (e.g., `NINA.Plugin.ExoPlanets.Sequencer`)
- Match file location to namespace hierarchy (e.g., `Sequencer/Container/ExoPlanetObjectContainer.cs` → `NINA.Plugin.ExoPlanets.Sequencer.Container`)

### MEF & Dependency Injection

- Use `[ImportingConstructor]` for required MEF dependencies
- Use `[ImportingConstructor]` + `Import` attributes for optional dependencies
- Example pattern:
  ```csharp
  [ImportingConstructor]
  public MyClass(IProfileService profileService, [Import(AllowDefault = true)] IOptionalService optional) { }
  ```

### WPF & XAML

- XAML files use `.xaml` + `.xaml.cs` pair
- Data models implement `INotifyPropertyChanged` for binding
- Use `RelayCommand` from GalaSoft.MvvmLight for command bindings

### Settings

- Plugin settings stored in `Properties/Settings.settings` and accessed via `Properties.Settings.Default`
- Use `CoreUtil.SaveSettings()` to persist changes
- Implement `ISettings` interface on `ExoPlanets` class to integrate with NINA settings UI

### External Data Integration

- HTTP requests via `HttpRequest` utility class
- Parse CSV files for variable star catalogs (required columns: `name`, `ra`, `dec`, `v`, `epoch`, `period`)
- Optional CSV columns: `drift`, `phase`
- VO-table XML format supported for catalog data

## Dependencies

- **NINA.Plugin** (v3.0.0.9001): Core plugin framework
- **.NET 8.0** (Windows 7.0+): Target framework
- **WPF**: For UI components
- **GalaSoft.MvvmLight**: For MVVM helpers

## Plugin Deployment

After building, the plugin DLL is automatically copied to:
```
%LOCALAPPDATA%\NINA\Plugins\3.0.0\ExoPlanets\
```

Restart NINA to load the updated plugin.

## Debugging

To debug the plugin:

1. Open `launchSettings.json` and use the "Profile 1" launch configuration
2. This launches NINA.exe directly from Program Files
3. Attach Visual Studio debugger or use the embedded debugger in Visual Studio

## Common Tasks

### Adding a new Sequencer Instruction

1. Create a new class in `Sequencer/Utility/` inheriting from `ISequenceItem`
2. Export via `[Export(typeof(ISequenceItem))]`
3. Implement required properties: `Name`, `Category`, `Description`
4. Add UI template in `Sequencer/Utility/Datatemplates.xaml`
5. Register `[ExportMetadata]` with NINA sequencer metadata

### Adding a new Data Model

1. Create class in `Model/` folder
2. Implement `INotifyPropertyChanged` if used in UI bindings
3. Follow naming: `ExoX.cs` for exoplanet models, `VariableX.cs` for variable star models

### Fetching External Catalog Data

1. Use `HttpRequest` utility for GET/POST requests
2. Parse responses (JSON or VO-table XML)
3. Handle timeouts and network errors gracefully
4. Store results in model objects
