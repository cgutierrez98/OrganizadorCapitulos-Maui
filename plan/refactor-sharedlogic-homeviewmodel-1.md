---
goal: "Separar la lógica compartida y reducir responsabilidades de HomeViewModel"
version: "1.0"
date_created: "2026-11-02"
last_updated: "2026-11-02"
owner: ""
status: "Planned"
tags: ["refactor", "architecture", "testing"]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Este plan establece un límite de proyecto real para `SharedLogic` y divide los flujos de análisis y operaciones de archivos que hoy coordina `HomeViewModel`. Se preservan el comportamiento actual, las interfaces públicas usadas por la UI y el destino Windows existente.

## 1. Requirements & Constraints

- **REQ-001**: La aplicación y el proyecto de pruebas deben consumir `SharedLogic` mediante una referencia a proyecto, sin compilar copias de sus archivos fuente.
- **REQ-002**: La biblioteca compartida debe compilar para `net9.0` y no debe depender de tipos ni paquetes de MAUI.
- **REQ-003**: `HomeViewModel` debe conservar sus comandos y propiedades observables utilizados por `Components/**/*.razor`.
- **REQ-004**: La extracción de flujos debe conservar el comportamiento actual de análisis local/IA, renombrado, movimiento, cancelación, progreso, log y undo/redo.
- **REQ-005**: Añadir pruebas de caracterización para los flujos extraídos antes de trasladar su lógica fuera de `HomeViewModel`.
- **CON-001**: Mantener `net9.0-windows10.0.19041.0` para la aplicación y el proyecto de pruebas; habilitar otros destinos queda fuera del alcance.
- **CON-002**: No cambiar contratos JSON de exportación/importación ni el formato de nombres de archivo.
- **PAT-001**: Mantener `Core`, `Application` e `Infrastructure` dentro de la biblioteca inicial; no dividirlos en varios proyectos en esta iteración.
- **PAT-002**: La UI conserva la propiedad del estado observable; los nuevos flujos reportan resultados y progreso al ViewModel sin referenciar componentes Razor.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Crear un límite de ensamblado para `SharedLogic` y hacer que la aplicación y las pruebas usen esa biblioteca.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Crear `SharedLogic/OrganizadorCapitulos.SharedLogic.csproj` como biblioteca SDK con `TargetFramework` `net9.0`, `ImplicitUsings` y `Nullable` habilitados. Incluir automáticamente todos los archivos `.cs` bajo `SharedLogic` y no agregar referencias MAUI. | | |
| TASK-002 | En `OrganizadorCapitulos.Maui.csproj`, reemplazar la inclusión manual `Compile Include="SharedLogic\\**\\*.cs"` por una referencia a `SharedLogic/OrganizadorCapitulos.SharedLogic.csproj`. Mantener la exclusión de esos archivos del glob de compilación de la aplicación para evitar tipos duplicados. | | |
| TASK-003 | En `tests/OrganizadorCapitulos.Tests/OrganizadorCapitulos.Tests.csproj`, quitar los elementos `Compile Include` que enlazan archivos de `SharedLogic` y agregar una referencia a `SharedLogic/OrganizadorCapitulos.SharedLogic.csproj`. Conservar los enlaces de fuentes propios de MAUI que las pruebas aún necesiten. | | |
| TASK-004 | Registrar `SharedLogic/OrganizadorCapitulos.SharedLogic.csproj` en `OrganizadorCapitulos-Maui.sln` con configuraciones Debug y Release para Any CPU. | | |
| TASK-005 | Validar la fase ejecutando `dotnet build OrganizadorCapitulos-Maui.csproj -f net9.0-windows10.0.19041.0` y `dotnet test tests/OrganizadorCapitulos.Tests/OrganizadorCapitulos.Tests.csproj`. Corregir únicamente errores causados por la nueva referencia de proyecto. | | |

**Criterio de salida:** la solución compila; las pruebas pasan; el proyecto MAUI y el de pruebas tienen una referencia de proyecto a la biblioteca; ningún archivo de `SharedLogic` se compila directamente dentro de esos dos proyectos.

### Implementation Phase 2

- GOAL-002: Extraer los flujos de análisis y organización de archivos de `HomeViewModel`, conservando el ViewModel como adaptador de estado y comandos para la UI.
- **Dependencia:** completar Implementation Phase 1 antes de iniciar esta fase.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-006 | Añadir pruebas de caracterización en `tests/OrganizadorCapitulos.Tests/HomeViewModelTests.cs` para análisis local de patrones, análisis con IA y cancelación, confirmación dry-run, renombrado por estrategia y movimiento con destino duplicado. Reutilizar los fakes existentes y comprobar también estado final, progreso, undo/redo y log cuando aplique. | | |
| TASK-007 | Crear `Services/Workflows/FileAnalysisWorkflow.cs` para ejecutar el análisis local y con IA actualmente implementado por `LoadFoldersInternalAsync`, `AutoAnalyzeWithLocalParser` y `AnalyzeWithAIAsync` en `ViewModels/HomeViewModel.cs`. El workflow recibirá las dependencias explícitamente, aceptará `CancellationToken` y un callback de progreso, y devolverá resultados sin modificar propiedades observables de la UI. | | |
| TASK-008 | Crear `Services/Workflows/FileOrganizationWorkflow.cs` para encapsular la ejecución de renombrado y movimiento actualmente implementada por `ConfirmDryRunAsync`, `RenameFileAsync` y `MoveFilesAsync` en `ViewModels/HomeViewModel.cs`. El workflow recibirá dependencias explícitamente, aceptará `CancellationToken` y un callback de progreso, y devolverá los cambios/resultados necesarios para actualizar la UI, registrar operaciones y mantener undo/redo. | | |
| TASK-009 | Registrar `FileAnalysisWorkflow` y `FileOrganizationWorkflow` en `MauiProgram.cs` con ciclo de vida Transient. Inyectarlos en `HomeViewModel` y reemplazar allí la lógica trasladada por llamadas a los workflows, manteniendo los nombres y contratos de los comandos actuales. | | |
| TASK-010 | Completar las pruebas de unidad de los workflows en `tests/OrganizadorCapitulos.Tests/`, con fakes para IA, repositorio/organizador, estrategia y callbacks. Cubrir resultados correctos, errores por archivo y cancelación sin depender de componentes visuales. | | |
| TASK-011 | Ejecutar `dotnet test tests/OrganizadorCapitulos.Tests/OrganizadorCapitulos.Tests.csproj` y `dotnet build OrganizadorCapitulos-Maui.csproj -f net9.0-windows10.0.19041.0`. Corregir regresiones de las fases implementadas y verificar que los componentes Razor sigan consumiendo los mismos comandos y propiedades. | | |

**Criterio de salida:** `HomeViewModel` conserva el estado de presentación y los comandos públicos; los workflows poseen la lógica ejecutable de análisis y organización; las pruebas de caracterización y de unidad pasan; la app compila para el destino Windows actual.

## 3. Alternatives

- **ALT-001**: Mantener `SharedLogic` como fuentes enlazadas en cada proyecto. Se descarta porque duplica la configuración de compilación y no hace cumplir el límite de dependencias.
- **ALT-002**: Crear proyectos separados para `Core`, `Application` e `Infrastructure` desde el primer cambio. Se pospone para reducir el alcance y establecer primero una referencia compartida estable.
- **ALT-003**: Dividir `HomeViewModel` únicamente en archivos `partial`. Se descarta como solución principal porque organiza el archivo, pero no separa la ejecución de los flujos ni mejora su testabilidad independiente.
- **ALT-004**: Cambiar los destinos MAUI para activar Android, iOS o Mac Catalyst en esta misma iteración. Se excluye porque el proyecto solo declara Windows como destino activo y no hay un requisito multiplataforma confirmado.

## 4. Dependencies

- **DEP-001**: SDK de .NET 9 instalado, compatible con los proyectos existentes.
- **DEP-002**: No se requieren paquetes NuGet nuevos para `SharedLogic`; el código inspeccionado usa APIs estándar de .NET.
- **DEP-003**: El proyecto de pruebas actual usa xUnit y apunta a Windows; esta configuración se mantiene durante la extracción.

## 5. Files

- **FILE-001**: `SharedLogic/OrganizadorCapitulos.SharedLogic.csproj` — nuevo proyecto biblioteca `net9.0`.
- **FILE-002**: `OrganizadorCapitulos.Maui.csproj` — reemplazar compilación de fuentes compartidas por referencia de proyecto.
- **FILE-003**: `tests/OrganizadorCapitulos.Tests/OrganizadorCapitulos.Tests.csproj` — reemplazar enlaces de fuentes compartidas por referencia de proyecto.
- **FILE-004**: `OrganizadorCapitulos-Maui.sln` — agregar el proyecto compartido.
- **FILE-005**: `Services/Workflows/FileAnalysisWorkflow.cs` — nuevo flujo de análisis.
- **FILE-006**: `Services/Workflows/FileOrganizationWorkflow.cs` — nuevo flujo de renombrado y movimiento.
- **FILE-007**: `MauiProgram.cs` — registrar los nuevos workflows.
- **FILE-008**: `ViewModels/HomeViewModel.cs` — delegar ejecución de flujos y conservar estado/comandos.
- **FILE-009**: `tests/OrganizadorCapitulos.Tests/HomeViewModelTests.cs` — pruebas de caracterización de los comportamientos existentes.
- **FILE-010**: `tests/OrganizadorCapitulos.Tests/` — pruebas unitarias de los workflows extraídos.

## 6. Testing

- **TEST-001**: Ejecutar `dotnet build OrganizadorCapitulos-Maui.csproj -f net9.0-windows10.0.19041.0` tras cada fase.
- **TEST-002**: Ejecutar `dotnet test tests/OrganizadorCapitulos.Tests/OrganizadorCapitulos.Tests.csproj` tras cada fase.
- **TEST-003**: Confirmar que `dotnet list OrganizadorCapitulos.Maui.csproj reference` y `dotnet list tests/OrganizadorCapitulos.Tests/OrganizadorCapitulos.Tests.csproj reference` muestran la biblioteca compartida.
- **TEST-004**: Confirmar que la biblioteca compila aisladamente con `dotnet build SharedLogic/OrganizadorCapitulos.SharedLogic.csproj`.
- **TEST-005**: Verificar que no quedan elementos `Compile Include` que enlacen archivos de `SharedLogic` en los proyectos MAUI o de pruebas.
- **TEST-006**: Probar manualmente en Windows carga de carpetas, análisis local y por IA, previsualización, confirmación dry-run, renombrado, movimiento, cancelación, undo y redo después de la extracción de workflows.

## 7. Risks & Assumptions

- **RISK-001**: Los fakes y fuentes enlazadas del proyecto de pruebas pueden revelar dependencias implícitas en tipos compartidos; eliminar solo los enlaces de `SharedLogic` y conservar los enlaces de fuentes MAUI necesarios.
- **RISK-002**: Extraer lógica que hoy muta objetos de archivo y estado del ViewModel puede alterar notificaciones observables; mantener las mutaciones de UI en el ViewModel y probar explícitamente las actualizaciones de estado.
- **RISK-003**: El proyecto de pruebas apunta a Windows, por lo que la biblioteca podrá ser neutral `net9.0` pero la suite completa no quedará automáticamente habilitada en sistemas no Windows.
- **ASSUMPTION-001**: La plataforma objetivo sigue siendo Windows; este plan no cambia plataformas, manifiestos ni distribución.
- **ASSUMPTION-002**: Se mantienen las convenciones de namespace existentes durante el refactor para evitar cambios no relacionados.

## 8. Related Specifications / Further Reading

- `OrganizadorCapitulos.Maui.csproj`
- `tests/OrganizadorCapitulos.Tests/OrganizadorCapitulos.Tests.csproj`
- `ViewModels/HomeViewModel.cs`
- `MauiProgram.cs`
