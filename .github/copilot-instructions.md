# Copilot Instructions for Client Tracker

## Project Overview
The **Client Tracker** project is a cross-platform application built using .NET MAUI, targeting Android, Windows, and MacCatalyst. The architecture is modular, with clear separations between models, views, and services, facilitating maintainability and scalability.

### Key Components
- **Models**: Located in the `Models/` directory, these classes represent the data structures used throughout the application (e.g., `Client.cs`, `Payment.cs`).
- **Views**: The `Pages/` directory contains XAML files that define the UI components, such as `ClientsPage.xaml` and `AddSalePage.xaml`.
- **ViewModels**: Implemented in the `ViewModels/` directory, these classes handle the logic for the views, following the MVVM pattern.
- **Services**: The `Services/` directory includes classes like `DatabaseService.cs`, which manage data access and business logic.

## Developer Workflows
### Building the Project
The project uses GitHub Actions for CI/CD, specifically defined in `.github/workflows/release-build.yml`. The build process is triggered on release events and can also be manually initiated.

- **Build Commands**:
  - **Android**:
    ```bash
    dotnet publish "ClientTracker/ClientTracker.csproj" -c "Release" -f net10.0-android -p:AndroidPackageFormat=apk
    ```
  - **Windows**:
    ```bash
    dotnet publish "$env:PROJECT_PATH" -c "$env:CONFIGURATION" -f net10.0-windows10.0.19041.0 -r win-x64 -p:WindowsPackageType=MSIX
    ```
  - **MacCatalyst**:
    ```bash
    dotnet publish "${PROJECT_PATH}" -c "${CONFIGURATION}" -f net10.0-maccatalyst -r maccatalyst-x64
    ```

### Testing and Debugging
While specific testing commands are not detailed in the current documentation, developers should follow the standard practices for unit testing in .NET. Ensure to check for any existing test projects or configurations.

## Project-Specific Conventions
- **Naming Conventions**: Follow PascalCase for class names and camelCase for method parameters.
- **File Structure**: Maintain the modular structure with clear separations between models, views, and services.

## Integration Points
- **Database**: The application uses SQLite for data storage, managed through the `DatabaseService.cs`.
- **External Dependencies**: Ensure to install the necessary workloads for MAUI development, as specified in the GitHub Actions workflow.

## Communication Patterns
- **MVVM Pattern**: The application follows the MVVM pattern, where ViewModels communicate with Views through data binding, and services handle data operations.

## Conclusion
This document serves as a foundational guide for AI coding agents to navigate the Client Tracker codebase effectively. For any unclear sections or additional details needed, please provide feedback for further iterations.

