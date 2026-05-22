# SharpPyxis.Packages

A collection of small, focused .NET primitives with zero external dependencies.

Each package is independent — take only what you need.

| Package | Description | NuGet |
|---|---|---|
| `SharpPyxis.Results` | `Result<T>` and `Error` primitives for explicit error handling | [SharpPyxis.Results](https://www.nuget.org/packages/SharpPyxis.Results/) |
| `SharpPyxis.Guards` | Argument validation guards and `UserFacingException` | [SharpPyxis.Guards](https://www.nuget.org/packages/SharpPyxis.Guards/) |

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

## Contributing

Contributions welcome. Each package must remain independently usable with zero external dependencies (only the .NET SDK).

## License

MIT
