# Bitwarden Manager

A comprehensive C# application for managing Bitwarden vault items through both CLI and API interfaces. This solution provides a unified backend that can integrate with the Bitwarden CLI while preparing for future API-based operations.

## Project Structure

- **BitwardenManager.Core** - Core models, interfaces, and shared functionality
- **BitwardenManager.CliWrapper** - Service implementation that wraps the Bitwarden CLI
- **BitwardenManager.ApiClient** - Placeholder for future direct API integration
- **BitwardenManager.CLI** - Command-line interface for interacting with Bitwarden
- **BitwardenManager.Api** - RESTful API providing unified access to Bitwarden functionality
- **BitwardenManager.Tests** - Unit tests for the solution

## Features

### Current Implementation
- ✅ CLI wrapper for Bitwarden operations
- ✅ Comprehensive vault item models (Login, SecureNote, Card, Identity)
- ✅ Authentication and vault management
- ✅ Interactive CLI application
- ✅ RESTful API with OpenAPI documentation
- ✅ Search and folder management
- ✅ Unit tests

### Planned Features
- 🔄 Direct Bitwarden API integration
- 🔄 Frontend UI (technology TBD)
- 🔄 Configuration management
- 🔄 Enhanced error handling and logging
- 🔄 Caching and performance optimizations

## Quick Start

### Prerequisites
- .NET 9.0 SDK
- Bitwarden CLI installed and available in PATH

### Building the Solution
```bash
dotnet build
```

### Running Tests
```bash
dotnet test
```

### Using the CLI Application
```bash
cd src/BitwardenManager.CLI
dotnet run

# Or with arguments
dotnet run -- status
dotnet run -- login user@example.com password
dotnet run -- list
dotnet run -- search "github"
```

### Running the API
```bash
cd src/BitwardenManager.Api
dotnet run
```

The API will be available at `https://localhost:5001` with OpenAPI documentation at the root URL.

## API Endpoints

- `GET /health` - Health check
- `GET /api/bitwarden/status` - Check authentication status
- `POST /api/bitwarden/login` - Login to Bitwarden
- `POST /api/bitwarden/unlock` - Unlock the vault
- `GET /api/bitwarden/items` - List all vault items
- `GET /api/bitwarden/search?query={query}` - Search vault items

## Architecture

The solution uses a clean architecture approach with:

1. **Core Layer** - Contains domain models and interfaces
2. **Service Layer** - Implements business logic (currently CLI wrapper)
3. **Presentation Layer** - CLI and API interfaces

This design allows for easy extension and replacement of services (e.g., switching from CLI to direct API calls) without affecting other layers.

## Development Notes

- The CLI wrapper handles all Bitwarden operations by executing the `bw` command
- JSON serialization uses camelCase naming for API compatibility
- Error handling includes logging and user-friendly messages
- CORS is enabled for development to support future frontend integration

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
