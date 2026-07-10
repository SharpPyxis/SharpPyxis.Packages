# SharpPyxis.Packages

A collection of small, focused .NET primitives with zero external dependencies.

Each package is independent — take only what you need.

| Package | Description | NuGet |
|---|---|---|
| `SharpPyxis.Results` | `Result<T>` and `Error` primitives for explicit error handling | [SharpPyxis.Results](https://www.nuget.org/packages/SharpPyxis.Results/) |
| `SharpPyxis.Guards` | Argument validation guards and `UserFacingException` | [SharpPyxis.Guards](https://www.nuget.org/packages/SharpPyxis.Guards/) |
| `SharpPyxis.UnitOfWork` | À-la-carte Unit of Work over ADO.NET — repositories resolved on demand | [SharpPyxis.UnitOfWork](https://www.nuget.org/packages/SharpPyxis.UnitOfWork/) |

**Target frameworks:** `net8.0`, `net10.0`

---

## SharpPyxis.Results

Represent operation outcomes without throwing exceptions. Model success and failure explicitly.

### Installation

```
dotnet add package SharpPyxis.Results
```

### Usage

```csharp
using SharpPyxis.Results;

// Return a result from a service
public Result<User> FindUser(Guid id)
{
    var user = _db.Find(id);
    return user is null
        ? Result.Failure<User>(Error.NotFound("User.NotFound", $"User {id} was not found."))
        : Result.Success(user);
}

// Consume it
var result = FindUser(id);

if (result.IsFailure)
    return result.Error; // Error.Code, Error.Message, Error.Type

var user = result.Value;
```

### Error factory methods

```csharp
Error.NotFound("User.NotFound", "User was not found.")
Error.Conflict("Email.Conflict", "Email is already in use.")
Error.Validation("Name.Required", "Name is required.")
Error.Unauthorized("Auth.Expired", "Token has expired.")
Error.Forbidden("Role.Missing", "Insufficient permissions.")
Error.Unexpected("Order.Failed", "An unexpected error occurred.")
```

---

## SharpPyxis.Guards

Argument validation guards that throw developer-facing exceptions on invalid input.
Parameter names are captured automatically — no `nameof()` required at call sites.

### Installation

```
dotnet add package SharpPyxis.Guards
```

### Usage

```csharp
using SharpPyxis.Guards;

public UserService(IUserRepository repository)
{
    _repository = Guard.NotNull(repository);
}

public Task<User> CreateAsync(string name, string email, Guid tenantId)
{
    Guard.NotWhiteSpace(name);
    Guard.NotEmpty(email);
    Guard.NotEmpty(tenantId);
    Guard.Satisfies(name, n => n.Length <= 100, "Name must be 100 characters or fewer.");

    // ...
}
```

### UserFacingException

For cases where returning a `Result<T>` is impractical and the error message is safe to surface to the end user:

```csharp
throw new UserFacingException("Invoice amount must be greater than zero.");

// With a custom HTTP status code:
throw new UserFacingException("Resource has been locked.", statusCode: 423);
```

---

## SharpPyxis.UnitOfWork

An à-la-carte Unit of Work over ADO.NET. The unit of work owns the connection and transaction and
hands out repositories **on demand** through a typed factory registry — instead of bundling a fixed
set of repository properties that every operation drags along. No reflection: repositories are built
from explicit factories, and `Repo<T>()` is a generic method, so callers keep full IntelliSense and
never cast.

Works with any ADO.NET provider (Npgsql, SqlClient, SQLite, …) because it only ever touches
`System.Data.Common`.

### Installation

```
dotnet add package SharpPyxis.UnitOfWork
```

### The idea

The classic Unit of Work bundles a **fixed** set of repositories. That set is too big for most
operations, and the useful subset changes with context. Here the unit of work owns the connection and
transaction, but exposes repositories through a factory instead of a frozen property list:

- `IUnitOfWork` — `BeginAsync` / `CommitAsync` / `RollbackAsync` / `IAsyncDisposable`, plus the
  generic `TRepo Repo<TRepo>()`.
- `Repo<TRepo>()` returns the requested repository, built lazily and cached, passing it the unit of
  work's connection and a late-bound accessor to the current transaction.
- Repositories are registered once at startup by **explicit factories** — no reflection.

**Compile-time vs run-time, by design:** the **type** returned by `Repo<T>()` is checked at compile
time; the **availability** of a repository (whether its factory was registered) is checked at run
time — an unregistered `Repo<T>()` fails fast with `RepositoryNotRegisteredException`. Add a startup
health check over `RepositoryRegistry.RegisteredTypes` to turn that into a boot-time error.

### Registering repositories

Repositories stay "blind": they receive the connection and a late-bound transaction accessor, and
forward whatever transaction is current to each command — without knowing whether a transaction is
active. This is what lets a **cached** repository work for a transaction-less read, and across several
begin/commit cycles, with the same instance.

```csharp
using System.Data.Common;
using SharpPyxis.UnitOfWork.Ado;

// Build the catalog once at startup, then reuse it for every unit of work.
var registry = new RepositoryRegistry()
    .Add<IPartiesRepository>((conn, currentTx) => new PartiesRepository(conn, currentTx))
    .Add<IOrdersRepository>((conn, currentTx) => new OrdersRepository(conn, currentTx));

// A blind repository forwards the current transaction to each command:
internal sealed class PartiesRepository(DbConnection connection, Func<DbTransaction?> currentTransaction)
    : IPartiesRepository
{
    // e.g. with Dapper: new CommandDefinition(sql, args, transaction: currentTransaction(), ...)
}
```

### Using it — single, fixed database

Supply the connection yourself (for example a DI-scoped one) and drive the transaction from your
service layer:

```csharp
await using var uow = new AdoUnitOfWork(connection, registry, ownsConnection: false);

await uow.BeginAsync(ct);
await uow.Repo<IPartiesRepository>().CreateAsync(party, ct);
await uow.Repo<IOrdersRepository>().CreateAsync(order, ct);
await uow.CommitAsync(ct);
```

Reads need no transaction — resolve a repository and query directly (the connection must be open):

```csharp
await using var uow = new AdoUnitOfWork(openConnection, registry, ownsConnection: false);
var parties = await uow.Repo<IPartiesRepository>().GetAllAsync(ct); // HasActiveTransaction == false
```

### Using it — connection resolved per request

When the connection is resolved per request (e.g. a tenant-scoped connection chosen from the route),
open it through a provider. `beginTransaction` defaults to `false` — you keep control of the
transaction:

```csharp
await using var uow = await AdoUnitOfWork.OpenAsync(
    ct => tenantFactory.CreateConnectionAsync("primary", ct),
    registry,
    beginTransaction: true,
    cancellationToken: ct);

await uow.Repo<IPartiesRepository>().CreateAsync(party, ct);
await uow.CommitAsync(ct);
```

For a fixed connection source, register `AdoUnitOfWorkFactory` (e.g. as a singleton) and create a
unit of work per operation with `CreateAsync(ct)` or `CreateAndBeginAsync(ct)`.

### Startup health check

Because availability is a run-time concern, assert it once at startup:

```csharp
Type[] expected = [typeof(IPartiesRepository), typeof(IOrdersRepository)];
var missing = expected.Where(t => !registry.RegisteredTypes.Contains(t)).ToArray();
if (missing.Length > 0)
    throw new InvalidOperationException(
        $"Missing repository factories: {string.Join(", ", missing.Select(t => t.Name))}.");
```

---

## Contributing

Contributions welcome. Each package must remain independently usable with zero external dependencies (only the .NET SDK).

## License

MIT
