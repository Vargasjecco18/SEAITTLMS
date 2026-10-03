# SEAITT-LMS

Blazor Web App (Interactive Server) on .NET 9, set up without a `dotnet new` template.

## Requirements
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- VS Code with the C# Dev Kit extension (recommended in `.vscode/extensions.json`)

## Run
```bash
dotnet run     # http://localhost:5000
dotnet watch   # with hot reload
```

In VS Code, press F5 to debug (uses `.vscode/launch.json`, or C# Dev Kit's own launch profile).

## Naming
The project and assembly are `SEAITT-LMS`. Hyphens are not valid in C# namespaces, so the root namespace is `SEAITT_LMS`.
