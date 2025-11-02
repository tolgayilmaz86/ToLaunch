# Contributing to ToLaunch

First off, thank you for considering contributing to ToLaunch! It's people like you that make ToLaunch such a great tool.

## Code of Conduct

This project and everyone participating in it is governed by respect and professionalism. By participating, you are expected to uphold this code.

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check the existing issues as you might find out that you don't need to create one. When you are creating a bug report, please include as many details as possible:

* **Use a clear and descriptive title** for the issue to identify the problem.
* **Describe the exact steps which reproduce the problem** in as many details as possible.
* **Provide specific examples to demonstrate the steps**.
* **Describe the behavior you observed after following the steps** and point out what exactly is the problem with that behavior.
* **Explain which behavior you expected to see instead and why.**
* **Include screenshots** if possible.
* **Include your environment details**: Windows version, .NET version, etc.

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion, please include:

* **Use a clear and descriptive title** for the issue to identify the suggestion.
* **Provide a step-by-step description of the suggested enhancement** in as many details as possible.
* **Provide specific examples to demonstrate the steps** or point out the part of ToLaunch where the enhancement could be implemented.
* **Describe the current behavior** and **explain which behavior you expected to see instead** and why.
* **Explain why this enhancement would be useful** to most ToLaunch users.

### Pull Requests

* Fill in the required template
* Do not include issue numbers in the PR title
* Follow the C# coding style used throughout the project
* Include screenshots in your pull request whenever possible
* End all files with a newline
* Avoid platform-dependent code when possible

## Development Setup

### Prerequisites

* Visual Studio 2022 or JetBrains Rider
* .NET 8.0 SDK
* Git

### Setting Up Your Development Environment

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR-USERNAME/ToLaunch.git
   cd ToLaunch
   ```

3. Add the upstream repository:
   ```bash
   git remote add upstream https://github.com/ORIGINAL-OWNER/ToLaunch.git
   ```

4. Create a branch for your changes:
   ```bash
   git checkout -b feature/your-feature-name
   ```

5. Build the project:
   ```bash
   dotnet build
   ```

6. Run the application:
   ```bash
   dotnet run --project ToLaunch
   ```

### Making Changes

1. Make your changes in your feature branch
2. Add or update tests as necessary
3. Ensure your code follows the existing code style
4. Run the application and test your changes thoroughly
5. Commit your changes:
   ```bash
   git commit -m "Add brief description of your changes"
   ```

### Submitting Changes

1. Push your changes to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```

2. Open a Pull Request on GitHub
3. Wait for review and address any feedback

## Code Style Guidelines

### C# Conventions

* Use PascalCase for class names, method names, and public members
* Use camelCase for local variables and private fields
* Prefix private fields with underscore: `_fieldName`
* Use meaningful and descriptive names
* Keep methods focused and single-purpose
* Add XML documentation comments for public APIs

### XAML Conventions

* Use proper indentation (4 spaces)
* Group related properties together
* Use meaningful x:Name attributes
* Follow Avalonia naming conventions

### MVVM Pattern

* Keep ViewModels independent of Views
* Use data binding instead of code-behind manipulation
* Use Commands for user interactions
* Keep business logic in Services, not ViewModels

## Project Structure

```
ToLaunch/
├── Models/  # Data models (plain C# classes)
├── ViewModels/      # ViewModels (MVVM pattern)
├── Views/           # XAML views and code-behind
├── Services/      # Business logic and utilities
├── Converters/      # Value converters for data binding
└── Assets/          # Images, icons, and other resources
```

## Testing

Currently, ToLaunch doesn't have automated tests. If you'd like to contribute by adding tests, that would be greatly appreciated!

### Manual Testing Checklist

When testing changes, please verify:

- [ ] Main program selection works correctly
- [ ] Programs can be added, edited, and removed
- [ ] Profile switching works without data loss
- [ ] Process monitoring detects program startup
- [ ] Auto-start triggers correctly
- [ ] Enable/disable toggle works
- [ ] Start All / Stop All functionality
- [ ] Settings are persisted correctly
- [ ] Icons are extracted and displayed
- [ ] UI remains responsive

## Questions?

Feel free to open an issue with the label `question` if you have any questions about contributing!

## License

By contributing to ToLaunch, you agree that your contributions will be licensed under the MIT License.
