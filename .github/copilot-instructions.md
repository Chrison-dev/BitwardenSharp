# GitHub Copilot Instructions - Codebase

This document provides GitHub Copilot with architectural patterns and coding conventions for the codebase. Focus on generating code that follows these established patterns for consistency and maintainability.

Note: I am the sole developer of this codebase; the guidance below assumes a single-maintainer workflow and conservative package upgrades.

## Architecture Overview

I follow **Clean Architecture/Onion Architecture** principles with strict layer separation, tuned for desktop applications:

```
src/
├── Domain/          # Core business logic, entities, specifications
├── Application/     # CQRS handlers, DTOs, validators, use cases
├── Infrastructure/  # OS integrations, local storage, logging
├── Persistence/     # Entity Framework contexts, repositories (SQLite/local DB)
└── Presentation/    # Desktop apps (WPF, WinForms, MAUI, Blazor Desktop)
```

## Core Technology Stack (desktop-first)

- **.NET 10** (desktop apps target `net10.0`)
- **Entity Framework Core** (use SQLite or local DB providers for offline-first storage)
- **MediatR** for CQRS pattern implementation (see NuGet Packages for pinned version guidance)
- **FluentValidation** for input validation
- **Autofac** (or Microsoft DI) for dependency injection
- **Mapperly** for object mapping (preferred over AutoMapper for new code)
- UI frameworks: **Blazor Desktop** for web-like UI
- Local telemetry/logging (File/Seq/Serilog) instead of cloud-only Application Insights by default
- **NUnit** for testing with **FluentAssertions**

## Nuget Packages (pinned versions)
To keep dependencies stable and avoid accidental upgrades to components that switched to different licenses, pin the following package versions where appropriate. Prefer exact versions in project files.

- `MediatR` = 10.0.1  # conservative pin — review license before upgrading
- `FluentValidation` = 11.5.3
- `Autofac` = 6.5.0 (or use Microsoft.Extensions.DependencyInjection as lightweight alternative)
- `Mapperly` = 1.3.0
- `Microsoft.EntityFrameworkCore` = 7.0.0 (use SQLite provider for local DB)
- `Microsoft.Extensions.Hosting` = 7.0.0 (use HostBuilder for DI in desktop apps)
- `NUnit` = 3.13.3
- `FluentAssertions` = 6.11.0
- `Moq` = 4.18.4  # for mocking in tests

Notes:
- I intentionally pinned `MediatR` to a conservative version (10.0.1). If you prefer another version, review its license and changelog before upgrading.
- Use exact versions (not floating ranges) in your NuGet package references to prevent automatic upgrades during CI builds.


## CQRS with MediatR Pattern

### Query Handler Pattern (Read Operations)
Queries should use specific DbContext interfaces via constructor injection for read-only operations:

```csharp
// Query pattern
public record GetEntityByIdQuery(int EntityId) : IRequest<EntityDto>;

public class GetEntityByIdQueryHandler : IRequestHandler<GetEntityByIdQuery, EntityDto>
{
    private readonly IEntityDbContext _context;

    public GetEntityByIdQueryHandler(IEntityDbContext context)
    {
        _context = context;
    }

    public async Task<EntityDto> Handle(GetEntityByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Entities
            .Include(e => e.RelatedEntities)
            .FirstOrDefaultAsync(e => e.Id == request.EntityId, cancellationToken);
            
        return entity?.ToDto();
    }
}
```

### Command Handler Pattern (Write Operations)
Commands should use `ITransactionWrapper` with writable DbContext interfaces for data modifications:

```csharp
// Command pattern
public record UpdateEntityCommand(int EntityId, string PropertyValue) : IRequest<Unit>;

public class UpdateEntityCommandHandler : IRequestHandler<UpdateEntityCommand, Unit>
{
    private readonly ITransactionWrapper _transactionWrapper;
    private readonly IValidator<UpdateEntityCommand> _validator;

    public UpdateEntityCommandHandler(
        ITransactionWrapper transactionWrapper,
        IValidator<UpdateEntityCommand> validator)
    {
        _transactionWrapper = transactionWrapper;
        _validator = validator;
    }

    public async Task<Unit> Handle(UpdateEntityCommand request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        
        return await _transactionWrapper.RunTransactionAsync<IWritableEntityDbContext, Unit>(
            async context =>
            {
                var entity = await context.Entities.FindAsync(request.EntityId, cancellationToken);
                if (entity == null)
                    throw new EntityNotFoundException($"Entity {request.EntityId} not found");
                    
                entity.UpdateProperty(request.PropertyValue);
                await context.SaveChangesAsync(cancellationToken);
                
                return Unit.Value;
            }, cancellationToken);
    }
}
```

### Multiple DbContext Usage in Commands
For operations requiring multiple contexts:

```csharp
public class ComplexEntityCommandHandler : IRequestHandler<ComplexEntityCommand, Result>
{
    private readonly ITransactionWrapper _transactionWrapper;

    public ComplexEntityCommandHandler(ITransactionWrapper transactionWrapper)
    {
        _transactionWrapper = transactionWrapper;
    }

    public async Task<Result> Handle(ComplexEntityCommand request, CancellationToken cancellationToken)
    {
        return await _transactionWrapper.RunTransactionAsync<IWritableEntityDbContext, IWritableAuditDbContext, Result>(
            async (entityContext, auditContext) =>
            {
                var entity = Entity.Create(request.Name, request.Description);
                await entityContext.Entities.AddAsync(entity, cancellationToken);
                
                var auditEntry = AuditEntry.Create("Entity Created", entity.Id);
                await auditContext.AuditEntries.AddAsync(auditEntry, cancellationToken);
                
                await entityContext.SaveChangesAsync(cancellationToken);
                await auditContext.SaveChangesAsync(cancellationToken);
                
                return Result.Success(entity.Id);
            }, cancellationToken);
    }
}
```

## DbContext Interface Hierarchy

### Interface Structure
DbContext interfaces follow a specific hierarchy:

```csharp
// Base writable interface
public interface IWritableDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

// Specific domain contexts inherit from writable interface
public interface IWritableEntityDbContext : IWritableDbContext
{
    DbSet<Entity> Entities { get; }
    DbSet<RelatedEntity> RelatedEntities { get; }
}

// Read-only contexts for queries (don't inherit from IWritableDbContext)
public interface IEntityDbContext
{
    DbSet<Entity> Entities { get; }
    DbSet<RelatedEntity> RelatedEntities { get; }
}
```

## Object Mapping with Mapperly

### Preferred Mapping Pattern
Use Mapperly for compile-time safe mapping:

```csharp
[Mapper]
public partial class EntityResponseMapper : IEntityResponseMapper
{
    // Simple mapping
    public partial EntityResponse MapFrom(EntityDto source);
    
    // Custom property mapping
    [MapProperty(nameof(EntityDto.PropertyName), nameof(EntityResponse.DifferentPropertyName))]
    public partial EntityResponse MapFromWithCustomMapping(EntityDto source);
    
    // Custom mapping method for complex transformations
    public List<NestedResponse> MapNestedItems(List<NestedDto> items)
    {
        return items.Select(item => new NestedResponse
        {
            Id = item.Id,
            ComputedValue = item.Value * 100,
            FormattedDate = item.Date.ToString("yyyy-MM-dd")
        }).ToList();
    }
}

// Interface definition
public interface IEntityResponseMapper
{
    EntityResponse MapFrom(EntityDto source);
    EntityResponse MapFromWithCustomMapping(EntityDto source);
}
```

## Validation with FluentValidation

Every command/query should have corresponding validators:

```csharp
public class CreateEntityCommandValidator : AbstractValidator<CreateEntityCommand>
{
    public CreateEntityCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotNull()
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Name is required and must be 100 characters or less.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description must be 500 characters or less.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
            .WithMessage("Valid category is required.");
    }
}
```

## Domain Entity Patterns

### Entity Base Structure
```csharp
public class Entity : BaseEntity
{
    public string Name { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public DateTime? ModifiedDate { get; private set; }

    // Private constructor for EF
    private Entity() { }

    // Factory method for creation
    public static Entity Create(string name)
    {
        ValidateName(name);
        
        return new Entity
        {
            Name = name,
            CreatedDate = DateTime.UtcNow
        };
    }

    // Business methods
    public void UpdateName(string newName)
    {
        ValidateName(newName);
        Name = newName;
        ModifiedDate = DateTime.UtcNow;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name cannot be empty");
    }
}
```

## Specification Pattern

Use specifications for reusable query logic:

```csharp
public class EntityIsActiveSpecification : Specification<Entity>
{
    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return entity => entity.IsActive && entity.DeletedDate == null;
    }
}

public class EntityBelongsToUserSpecification : Specification<Entity>
{
    private readonly int _userId;

    public EntityBelongsToUserSpecification(int userId)
    {
        _userId = userId;
    }

    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return entity => entity.CreatedByUserId == _userId;
    }
}

// Usage in repositories or queries
public async Task<List<Entity>> GetActiveEntitiesForUserAsync(int userId)
{
    var activeSpec = new EntityIsActiveSpecification();
    var userSpec = new EntityBelongsToUserSpecification(userId);
    var combinedSpec = activeSpec.And(userSpec);
    
    return await _context.Entities
        .Where(combinedSpec.ToExpression())
        .ToListAsync();
}
```

## Desktop App Pattern

When building a local desktop application prefer the following patterns:

- Use a single Host (HostBuilder) for DI and configuration even in desktop apps. The host can wire logging, MediatR, EF DbContexts, and application services.
- Keep presentation logic thin: view models (MVVM for WPF/MAUI) should use MediatR or application services to perform business operations.
- Use a local, embedded database (SQLite) for offline-first behavior and sync strategies.
- For packaging/distribution use platform-appropriate formats:
  - Windows: MSIX or installer (WiX/NSIS)
  - macOS: .pkg or Developer-signed app
  - Linux: AppImage or distro packages

Example: registering services in a WPF App using a Host

```csharp
public partial class App : Application
{
    public IHost AppHost { get; }

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<MyDbContext>(options => options.UseSqlite("Data Source=my.db"));
                services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(App).Assembly));
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await AppHost.StartAsync();
        var mw = AppHost.Services.GetRequiredService<MainWindow>();
        mw.Show();
    }
}
```

## Testing Patterns

### Test Structure with TestingContext Base
```csharp
[TestFixture]
public class GetEntityQueryHandlerTests : TestingContext<GetEntityQueryHandler>
{
    private EntityContext _context;
    private TransactionWrapperStub _transactionWrapper;

    [SetUp]
    public void BeforeEachTest()
    {
        base.Setup();
        _context = SetupContext<EntityContext>(TestContext.CurrentContext.Test.Name);
        _transactionWrapper = new TransactionWrapperStub(_context);
        
        Stub<IEntityDbContext>(_context);
        Stub<IWritableEntityDbContext>(_context);
        Stub<ITransactionWrapper>(_transactionWrapper);
    }

    [Test]
    public async Task Handle_ValidId_ReturnsEntity()
    {
        // Arrange
        var entity = await new EntityFixture(_context)
            .WithName("Test Entity")
            .AsActive()
            .SaveAsync();

        var query = new GetEntityQuery(entity.Id);

        // Act
        var result = await ClassUnderTest.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Entity");
        result.IsActive.Should().BeTrue();
    }
}
```

### Fixture Pattern for Test Data
```csharp
public class EntityFixture
{
    private readonly EntityContext _context;
    private string _name = "Default Entity";
    private bool _isActive = true;
    private DateTime? _createdDate;

    public EntityFixture(EntityContext context)
    {
        _context = context;
    }

    public EntityFixture WithName(string name)
    {
        _name = name;
        return this;
    }

    public EntityFixture AsActive()
    {
        _isActive = true;
        return this;
    }

    public EntityFixture AsInactive()
    {
        _isActive = false;
        return this;
    }

    public EntityFixture CreatedOn(DateTime date)
    {
        _createdDate = date;
        return this;
    }

    public async Task<Entity> SaveAsync()
    {
        var entity = Entity.Create(_name);
        if (!_isActive) entity.Deactivate();
        if (_createdDate.HasValue) entity.SetCreatedDate(_createdDate.Value);
        
        await _context.Entities.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }
}
```

## Entity Framework Configuration

### DbContext Pattern
```csharp
public class EntityContext : DbContext, IEntityDbContext, IWritableEntityDbContext
{
    public DbSet<Entity> Entities { get; set; }
    public DbSet<RelatedEntity> RelatedEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EntityContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

// Entity configuration
public class EntityConfiguration : IEntityTypeConfiguration<Entity>
{
    public void Configure(EntityTypeBuilder<Entity> builder)
    {
        builder.ToTable("Entities");
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.HasIndex(e => e.Name)
            .HasDatabaseName("IX_Entities_Name");
            
        builder.Property(e => e.CreatedDate)
            .IsRequired();
    }
}
```

## Dependency Injection with Autofac

### Module Pattern
```csharp
public class ApplicationModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Register MediatR handlers
        builder.RegisterAssemblyTypes(typeof(ApplicationModule).Assembly)
            .AsClosedTypesOf(typeof(IRequestHandler<,>))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        // Register validators
        builder.RegisterAssemblyTypes(typeof(ApplicationModule).Assembly)
            .Where(t => t.IsClosedTypeOf(typeof(IValidator<>)))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        // Register DbContexts (typically from NuGet packages)
        builder.RegisterType<EntityContext>()
            .As<IEntityDbContext>()
            .As<IWritableEntityDbContext>()
            .InstancePerLifetimeScope();
    }
}
```

## Namespace Conventions

- **Domain**: `{RootNamespace}.Domain.{Area}`
- **Application**: `{RootNamespace}.Application.{Area}.{Commands|Queries|DTOs|Validators}`
- **Infrastructure**: `{RootNamespace}.Infrastructure.{Concern}`
- **Persistence**: `{RootNamespace}.Persistence.{Context}`
- **Presentation**: `{RootNamespace}.Presentation.{Type}`

## Error Handling

### Custom Exceptions
```csharp
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public IEnumerable<string> Errors { get; }
    
    public ValidationException(IEnumerable<ValidationFailure> failures) 
        : base("Validation failed")
    {
        Errors = failures.Select(f => f.ErrorMessage);
    }
}
```

## Performance & Best Practices (desktop-focused)

1. **Prefer async/await** for IO and long-running operations to keep UI responsive.
2. **Pass CancellationToken** to all async operations and cancel on window close or navigation.
3. **Use SQLite** with appropriate indexing and use EF Core's AsNoTracking for read-heavy UI views.
4. **Keep UI thread free** — offload heavy CPU work to background tasks and marshal results to the UI thread.
5. **Prefer Mapperly** for mapping DTOs and ViewModels for compile-time safety.
6. **Validate inputs** with FluentValidation on view models before invoking commands.
7. **Local logging**: use Serilog with rolling files and optionally Ship logs to remote collector only when opted-in.

Security & secrets (local-first)
- Use platform secret stores for credentials and tokens:
    - Windows: DPAPI / Windows Credential Manager
    - macOS: Keychain
    - Linux: libsecret / GNOME Keyring or KDE Wallet
- For development you can use user-scoped DPAPI (ProtectedData) or `dotnet user-secrets` (dev only).
- For CI or shared servers prefer environment variables or vault-backed secrets; avoid plaintext on disk.

When integrating cloud resources later, keep secrets in a vault and use managed identities; however for a desktop-first app design for offline-first local storage and local OS-backed secret management.

## Common Anti-Patterns to Avoid

1. **Don't bypass MediatR** - Always use `IMediator.Send()` for business operations
2. **Don't use `new()` for entities** - Use factory methods with validation
3. **Don't forget validation** - Every input should be validated
4. **Don't ignore cancellation tokens** - Always pass them through
5. **Don't expose public setters on entities** - Use business methods
6. **Don't catch and swallow exceptions** - Let FunctionRunner handle in APIs
7. **Don't use magic strings** - Define constants for recurring values
8. **Don't mix mapping libraries** - Prefer Mapperly for consistency
9. **Don't use TransactionWrapper in queries** - Queries should only inject specific DbContext interfaces for read operations
10. **Don't modify data in queries** - Commands handle writes, queries handle reads only

---

When generating code, focus on these architectural patterns and conventions to ensure consistency with the existing codebase. Use the `{RootNamespace}` placeholder in examples and replace it with your chosen project namespace when scaffolding. Prioritize clean architecture principles, MVVM for presentation, offline-first persistence (SQLite), local OS secret stores, and thorough unit and UI testing.