# NeoPos Backend - Central API Solution

A modular .NET-based backend system providing the core logic and data persistence for the NeoPos ecosystem.

## Architecture
The project follows a layered architecture:
- **NeoPos.WebAPI**: The entry point, containing Controllers, Hubs, and Middleware.
- **BusinessLayer**: Core logic, services, DTOs, and AutoMapper profiles.
- **DAL.Server**: Data Access Layer using Entity Framework Core. Supports **PostgreSQL** (default) and **SQLite** (fallback).
- **Domain**: Central repository for Entities, Enums, and common interfaces.

## Database Providers
The system can switch between providers via `appsettings.json`:
```json
"DatabaseProvider": "Npgsql" // for PostgreSQL
// OR
"DatabaseProvider": "Sqlite" // for local SQLite
```

## Tech Stack
- **.NET 8**
- **Entity Framework Core** with **PostgreSQL** (`Npgsql`).
- **SignalR**: Real-time communication for POS and Boss panels.
- **JWT Authentication**: Secure stateless authentication.
- **FluentValidation**: Robust request validation.
- **ClosedXML**: High-performance Excel reporting.

## Key Features
- Multi-tenant support for multiple businesses.
- Real-time order synchronization via SignalR.
- Automated data reporting and Excel exports.
- Secure API with role-based access control.
