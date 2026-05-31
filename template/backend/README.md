# Ambev Developer Evaluation — Sales API

This document describes the **Sales API** implementation for the Developer Evaluation challenge, covering architectural decisions, technology choices, API contract, and how to run the project.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Architectural Decisions](#architectural-decisions)
- [Tech Stack](#tech-stack--libraries)
- [Project Structure](#project-structure)
- [Business Rules](#business-rules)
- [API Endpoints](#api-endpoints)
- [Domain Events](#domain-events--event-sourcing)
- [How to Run](#how-to-run)
- [Running Tests](#running-tests)

---

## Architecture Overview

The solution follows a **Clean Architecture** approach combined with **Domain-Driven Design (DDD)** and **CQRS with Event Sourcing** for the Sales domain, organized in concentric layers with strict dependency rules:

```
┌──────────────────────────────────────────────────────────────────┐
│                     WebApi (Presentation)                        │
│             Minimal APIs · Controllers · Middleware              │
├──────────────────────────────────────────────────────────────────┤
│                      Application Layer                           │
│           Commands · Queries · Handlers · Validators            │
├──────────────────────────────────────────────────────────────────┤
│                       Domain Layer                               │
│       Entities · IDomainEvent · IEventStoreRepository            │
│              Business Rules · Domain Events                      │
├──────────────┬───────────────────────────────────────────────────┤
│  PostgreSQL  │                   MongoDB                         │
│  Write Model │               Event Store                         │
│  (EF Core)   │   (MongoEventStoreRepository — sale_events)       │
└──────────────┴───────────────────────────────────────────────────┘
```

---

## Architectural Decisions

### 1. Domain-Driven Design (DDD)

- **Entities** (`Sale`, `SaleItem`) encapsulate their own invariants. `SaleItem.ApplyBusinessRules()` applies discount logic directly on the entity, keeping business rules close to the data they govern.
- **External Identities pattern**: cross-domain references (Customer, Branch) are stored as IDs + denormalized names (`CustomerId`/`CustomerName`, `BranchId`/`BranchName`). This avoids tight coupling between bounded contexts without requiring cross-context queries.
- **Domain Events** (`SaleCreated`, `SaleModified`, `SaleCancelled`, `ItemCancelled`) are defined in the Domain layer and logged by handlers, keeping the door open for future message broker integration (e.g., via Rebus).

### 2. CQRS with Event Sourcing

> **True CQRS** is implemented here: separate write model (PostgreSQL) and event store (MongoDB), with domain events persisted as an immutable log.

**Command Query Responsibility Segregation (CQRS):**

| Type    | Class suffix | Responsibility                      | Store         |
|---------|-------------|--------------------------------------|---------------|
| Command | `*Command`  | Mutates state, returns `OperationResult<T>` | PostgreSQL (EF Core) |
| Query   | `*Query`    | Reads state, returns `OperationResult<T>` | PostgreSQL (EF Core) |

Commands: `CreateSaleCommand`, `UpdateSaleCommand`, `DeleteSaleCommand`, `CancelSaleCommand`, `CancelSaleItemCommand`

Queries: `GetSaleQuery`, `GetSalesQuery`

**Event Sourcing:**

Every mutating command also appends an immutable event document to MongoDB's `sale_events` collection via `IEventStoreRepository`. This creates an audit log of everything that happened to a Sale:

```json
{
  "eventId": "3fa85f64-...",
  "eventType": "SaleCreated",
  "aggregateId": "sale-guid",
  "aggregateType": "Sale",
  "occurredAt": "2024-01-15T10:30:00Z",
  "payload": { "saleNumber": "SALE-001", "customerName": "Acme Corp", ... }
}
```

The event store is **append-only** — events are never updated or deleted. You can replay all events for a given `aggregateId` to reconstruct the full history of any Sale.

**MediatR** acts as the mediator pipeline, decoupling the API from application logic and enabling cross-cutting pipeline behaviors (e.g., validation).

### 3. Notification Pattern (instead of exceptions for business errors)

Rather than throwing exceptions for expected domain errors (not found, validation failure, business rule violations), handlers return `OperationResult<T>`:

```csharp
// Success
OperationResult<CreateSaleResult>.Success(data)

// Business rule violation (e.g., >20 items)
OperationResult<CreateSaleResult>.FailureBusinessRule("Cannot sell more than 20 identical items.")

// Not found
OperationResult<GetSaleResult>.FailureNotFound($"Sale {id} not found.")

// Validation
OperationResult<T>.FailureValidation(new[] { "SaleNumber is required." })
```

`OperationResult<T>` carries a list of `Notification` objects, each with a `Type` that maps to an HTTP status code:

| NotificationType | HTTP Status | When to use |
|-----------------|------------|-------------|
| `Validation`    | 400        | Missing/malformed fields |
| `NotFound`      | 404        | Resource doesn't exist |
| `Conflict`      | 409        | State conflict (e.g., already cancelled) |
| `BusinessRule`  | 422        | Valid request, but violates domain rules |

> **Why 422 for business rules and not 400?**
> `400 Bad Request` signals the *request itself* is malformed — wrong format, missing fields. `422 Unprocessable Entity` signals the request is well-formed and understood, but the server cannot process it due to business semantics. "Cannot sell 25 units" is semantically invalid, not structurally invalid — 422 is the precise and correct status.

> Exceptions are still used for truly unexpected/infrastructure errors, which bubble up through the `ValidationExceptionMiddleware`.

### 4. Minimal APIs (Sales Feature)

The Sales feature uses ASP.NET Core **Minimal APIs** (`SalesEndpoints.cs`) instead of Controllers:

- Registered via an extension method: `app.MapSalesEndpoints()`
- Grouped under `/api/sales` with OpenAPI tag `"Sales"`
- Each handler method is private and static, keeping the endpoint file focused and lean
- Controllers (MVC) are still used for the existing Users/Auth features to preserve backward compatibility

### 5. FluentValidation Pipeline Behavior

`ValidationBehavior<TRequest, TResponse>` intercepts all MediatR requests. For Sales, validation errors are handled at two levels:
1. **Endpoint level** — request DTOs validated before dispatch (e.g., `CreateSaleRequestValidator`)
2. **Handler level** — domain-level validation returns `OperationResult` with `Validation` notifications

### 6. Redis Caching (Cache-Aside Pattern)

The `GetSale` endpoint uses a **cache-aside** pattern to avoid redundant database queries on frequently-accessed sales:

- **Interface**: `ISaleCacheService` (Application layer — respects Clean Architecture)
- **Implementation**: `RedisSaleCacheService` (IoC layer — wraps `IDistributedCache`)
- **Key pattern**: `sale:{guid}`
- **TTL**: 5 minutes absolute expiration
- **Cache invalidation**: on every write operation (Update, Cancel, CancelItem, Delete), the entry is removed via `RemoveAsync`
- **Fallback**: if `RedisConnection` is absent from config, the DI registers `AddDistributedMemoryCache()` automatically — no runtime failure without Redis

```csharp
// Cache-first in GetSaleHandler:
var cached = await _cache.GetAsync(query.Id, cancellationToken);
if (cached is not null)
{
    return OperationResult<GetSaleResult>.Success(cached);
}
// ... DB query, then SetAsync
```

> The interface lives in the Application layer and depends only on `GetSaleResult` (Application-layer type). The implementation lives in IoC, which already references Application. This preserves the inward dependency rule.

### 7. API Documentation (Swagger / OpenAPI)

The API is documented using **Swashbuckle** (`Swashbuckle.AspNetCore`) via Minimal API metadata:

- **Swagger UI**: `https://localhost:{port}/swagger`
- **OpenAPI spec**: `https://localhost:{port}/swagger/v1/swagger.json`
- Each endpoint declares `.WithName()`, `.WithSummary()`, and `.Produces<T>()` for accurate schema generation
- JWT Bearer security is declared globally — the Swagger UI shows the "Authorize" button

```csharp
app.MapPost("/api/sales", CreateSale)
    .WithName("CreateSale")
    .WithSummary("Create a new sale")
    .Produces<ApiDataResponse<CreateSaleResult>>(StatusCodes.Status201Created)
    .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
    .Produces<ApiResponse>(StatusCodes.Status422UnprocessableEntity);
```

### 8. C# Modern Language Features (.NET 8)

The codebase deliberately demonstrates modern C# idioms to keep the code expressive and concise:

| Feature | Usage |
|---------|-------|
| **Records** (`record`) | Immutable DTOs: all `*Result`, `*Query` (simple), `*Command` (simple), `Notification`, domain events |
| **Positional records** | Single/few-property types: `record CancelSaleCommand(Guid Id)`, `record DeleteSaleResult(bool Success)` |
| **Records with `init`** | Multi-property DTOs: `record CreateSaleResult { public Guid Id { get; init; } ... }` |
| **Collection expressions** | `[]` instead of `new List<T>()` / `Array.Empty<T>()` |
| **Pattern matching** | `Quantity switch { >= 10 => ..., >= 4 => ..., _ => 0m }` for discount tiers |
| **`is not null`** | Null check: `if (cancelError is not null) { return ...; }` |
| **Primary constructors** (records) | Domain events: `public record SaleCreatedEvent(Sale Sale) : IDomainEvent` |

> Commands with mutable list properties (`CreateSaleCommand`, `UpdateSaleCommand`) remain as classes to support HTTP model binding and test data building patterns.

### 8. Early Return + Explicit Braces

All conditional paths use early return to avoid nesting, and every `if` always uses `{ }`:

```csharp
public async Task<OperationResult<UpdateSaleResult>> Handle(UpdateSaleCommand command, ...)
{
    var validationResult = await validator.ValidateAsync(command, cancellationToken);
    if (!validationResult.IsValid)
    {
        return OperationResult<UpdateSaleResult>.FailureValidation(...);
    }

    var sale = await _saleRepository.GetByIdAsync(command.Id, cancellationToken);
    if (sale == null)
    {
        return OperationResult<UpdateSaleResult>.FailureNotFound(...);
    }

    if (sale.IsCancelled)
    {
        return OperationResult<UpdateSaleResult>.FailureConflict(...);
    }
    // ... happy path
}
```

---

## Tech Stack & Libraries

| Layer       | Technology                | Role |
|-------------|--------------------------|------|
| Runtime     | .NET 8.0 / C# 12         | Target framework |
| Web         | ASP.NET Core Minimal APIs | HTTP routing for Sales |
| ORM         | Entity Framework Core 8   | Write model persistence (PostgreSQL) |
| Database    | PostgreSQL 13             | Relational write store |
| NoSQL       | MongoDB 8.0               | Event Store — append-only `sale_events` collection |
| Cache       | Redis 7.4                 | Distributed cache for Sale read queries (`ISaleCacheService`) |
| Mediator    | **MediatR 12**            | Decouples handlers from API; enables pipeline behaviors |
| Mapping     | **AutoMapper 13**         | Domain → DTO mapping for Users (Sales uses manual mapping) |
| Validation  | **FluentValidation 11**   | Strongly-typed validators for Commands and WebApi Requests |
| Messaging   | **Rebus**                 | Pluggable message bus — swap `MongoEventStoreRepository` for a `RebusEventStoreRepository` |
| Logging     | **Serilog**               | Structured logging with enrichers and sinks |
| Auth        | JWT Bearer                | Authentication for all endpoints |

### Testing Libraries

| Tool                | Purpose                              |
|---------------------|--------------------------------------|
| **xUnit**           | Test runner / test discovery         |
| **NSubstitute**     | Mock/substitute for interfaces       |
| **Bogus / Faker**   | Realistic fake test data generation  |
| **FluentAssertions**| Readable, chainable assertions       |
| **EF Core InMemory**| In-memory DB for handler/functional tests |
| **Mvc.Testing**     | `WebApplicationFactory` for functional endpoint tests |

> **Why manual mapping instead of AutoMapper for Sales?**  
> The Sales feature returns `OperationResult<T>` with strongly-typed result records. Using AutoMapper here would require extra profile configuration with little gain. Explicit mapping in handlers is clearer, type-safe, and easier to debug in a code review context.

---

## Project Structure

```
template/backend/
├── src/
│   ├── Ambev.DeveloperEvaluation.Domain/
│   │   ├── Entities/          # Sale, SaleItem
│   │   ├── Events/            # Domain events (SaleCreated, SaleCancelled, …)
│   │   └── Repositories/      # ISaleRepository, IEventStoreRepository (interfaces)
│   │
│   ├── Ambev.DeveloperEvaluation.Application/
│   │   └── Sales/
│   │       ├── Common/        # OperationResult<T>, Notification, NotificationType, ISaleCacheService
│   │       ├── CreateSale/    # CreateSaleCommand + Handler + Validator + Result
│   │       ├── UpdateSale/    # UpdateSaleCommand + Handler + Validator + Result
│   │       ├── DeleteSale/    # DeleteSaleCommand + Handler + Result
│   │       ├── CancelSale/    # CancelSaleCommand + Handler + Result
│   │       ├── CancelSaleItem/# CancelSaleItemCommand + Handler + Result
│   │       ├── GetSale/       # GetSaleQuery + Handler + Result
│   │       └── GetSales/      # GetSalesQuery + Handler + Result (paginated)
│   │
│   ├── Ambev.DeveloperEvaluation.ORM/
│   │   ├── Repositories/      # SaleRepository (EF Core impl with dynamic ordering)
│   │   └── Mapping/           # SaleConfiguration, SaleItemConfiguration
│   │
│   ├── Ambev.DeveloperEvaluation.IoC/
│   │   ├── Cache/             # RedisSaleCacheService (IDistributedCache impl of ISaleCacheService)
│   │   └── ModuleInitializers/# InfrastructureModuleInitializer (registers MongoDB, Redis, repos)
│   │
│   └── Ambev.DeveloperEvaluation.WebApi/
│       └── Features/Sales/
│           └── SalesEndpoints.cs   # Minimal API route registrations
│
└── tests/
    ├── Ambev.DeveloperEvaluation.Unit/
    │   ├── Application/Sales/ # CreateSaleHandlerTests, GetSaleHandlerTests, …
    │   └── Domain/Entities/   # SaleTests (business rules)
    ├── Ambev.DeveloperEvaluation.Integration/
    │   └── Sales/             # SaleHandlerIntegrationTests (real EF InMemory + mocked event store/cache)
    └── Ambev.DeveloperEvaluation.Functional/
        └── Sales/             # SalesEndpointsTests (WebApplicationFactory + InMemory DB)
```

---

## Business Rules

| Quantity       | Discount |
|---------------|---------|
| < 4 items     | 0%      |
| 4–9 items     | 10%     |
| 10–20 items   | 20%     |
| > 20 items    | ❌ Not allowed (returns 422) |

Rules are enforced in `SaleItem.ApplyBusinessRules()` (domain layer) and surfaced through the Notification Pattern in handlers.

---

## API Endpoints

Base URL: `/api/sales`

| Method   | Path                                    | Description           |
|----------|-----------------------------------------|-----------------------|
| `POST`   | `/api/sales`                            | Create a sale         |
| `GET`    | `/api/sales/{id}`                       | Get sale by ID        |
| `GET`    | `/api/sales?_page=1&_size=10&_order=...`| List sales (paginated)|
| `PUT`    | `/api/sales/{id}`                       | Update a sale         |
| `DELETE` | `/api/sales/{id}`                       | Delete a sale         |
| `PATCH`  | `/api/sales/{id}/cancel`                | Cancel a sale         |
| `PATCH`  | `/api/sales/{saleId}/items/{itemId}/cancel` | Cancel a sale item |

### Ordering examples

```
GET /api/sales?_order=saleDate desc
GET /api/sales?_order=customerName asc, totalAmount desc
GET /api/sales?_page=2&_size=5&_order=saleNumber asc
```

### Error Response Format

```json
{
  "success": false,
  "message": "Sale not found",
  "errors": [
    {
      "error": "NotFound",
      "detail": "Sale 3fa85f64-5717-4562-b3fc-2c963f66afa6 not found."
    }
  ]
}
```

---

## Domain Events & Event Sourcing

Every mutating operation on a Sale appends an event to MongoDB's `sale_events` collection **and** emits a structured log. The event store is **append-only** and provides a complete audit trail.

**Event document structure** in MongoDB:
```json
{
  "_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventType": "SaleCreated",
  "aggregateId": "sale-guid",
  "aggregateType": "Sale",
  "occurredAt": "2024-01-15T10:30:00Z",
  "payload": { ... full event data ... }
}
```

| Event              | Trigger                          | Log tag             |
|--------------------|----------------------------------|---------------------|
| `SaleCreatedEvent` | Sale successfully created        | `[Event:SaleCreated]`   |
| `SaleModifiedEvent`| Sale successfully updated        | `[Event:SaleModified]`  |
| `SaleCancelledEvent`| Sale cancelled via PATCH        | `[Event:SaleCancelled]` |
| `ItemCancelledEvent`| Sale item cancelled via PATCH   | `[Event:ItemCancelled]` |

> The spec states: *"it's not required to actually publish to any Message Broker."* Events are persisted to MongoDB (durable, queryable) and logged via Serilog. A future integration with Rebus/RabbitMQ requires only implementing a `RebusEventStoreRepository` and swapping the DI registration.

---

## How to Run

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) + Docker Compose

### Using Docker Compose

```bash
cd template/backend
docker-compose up -d
```

This starts:
- **API** on `http://localhost:8080`
- **PostgreSQL** on port `5432`
- **MongoDB** on port `27017`
- **Redis** on port `6379`

### Running locally

```bash
cd template/backend

# Restore dependencies
dotnet restore

# Apply EF Core migrations
dotnet ef database update --project src/Ambev.DeveloperEvaluation.ORM --startup-project src/Ambev.DeveloperEvaluation.WebApi

# Run
dotnet run --project src/Ambev.DeveloperEvaluation.WebApi
```

Swagger UI available at: `https://localhost:{port}/swagger`

---

## Running Tests

The solution has **three test suites**, all in-memory — no external services needed:

| Suite         | Project                                     | Coverage |
|---------------|---------------------------------------------|----------|
| **Unit**      | `Ambev.DeveloperEvaluation.Unit`            | Domain business rules, CQRS handlers (mocked repo + event store + cache) |
| **Integration**| `Ambev.DeveloperEvaluation.Integration`   | Handlers + real EF Core InMemory repository (mocked event store + cache) |
| **Functional**| `Ambev.DeveloperEvaluation.Functional`      | Full HTTP stack via `WebApplicationFactory` (InMemory DB, mocked MongoDB/Redis) |

```bash
# Run all tests at once
cd template/backend
dotnet test

# Run individual suites
dotnet test tests/Ambev.DeveloperEvaluation.Unit
dotnet test tests/Ambev.DeveloperEvaluation.Integration
dotnet test tests/Ambev.DeveloperEvaluation.Functional
```

**Integration tests** (`SaleHandlerIntegrationTests`) instantiate real CQRS handlers with a real EF Core InMemory repository — this validates the full application + persistence layer stack, including discount rules, cache invalidation interactions, and event publishing, without Docker.

**Functional tests** (`SalesEndpointsTests`) use `WebApplicationFactory<Program>` to spin up the actual Minimal API pipeline in-process. EF Core is overridden with InMemory DB, MongoDB is replaced with an NSubstitute mock, and Redis with `IDistributedMemoryCache`. These tests exercise the complete HTTP → MediatR → handler → repository → response chain.

> **Test isolation**: each integration test class uses a unique in-memory DB name (`$"IntegrationTests_{Guid.NewGuid()}"`) to ensure test independence. Functional tests share a single named DB (`"SalesFunctionalTests"`) scoped to the `WebApplicationFactory` instance for sequential scenario tests.

For test coverage report:

```bash
# Windows
coverage-report.bat

# Linux/macOS
./coverage-report.sh
```
