# ICanHelpApp
# README.md — ICanHelp-EFCore

# ICanHelp

Personal .NET project currently in development.

The goal of the project is to build a backend application while practicing application structure, separation of responsibilities, Entity Framework Core, and working with a relational database.

## Current status

**Work in progress**

The project is still under active development. The current repository contains the foundation of the application and the main layers used to organize the code.

## Architecture

The solution is divided into separate projects:

* **ICanHelp.Core** — core of the application and domain-related elements
* **ICanHelp.Application** — application logic and use-case related code
* **ICanHelp.Infrastructure** — infrastructure concerns, including database access

A UI/application host is planned as the project develops further.

## Technologies

* C#
* .NET 9
* Entity Framework Core
* SQL Server
* Dependency Injection
* Scrutor
* Swagger / OpenAPI tooling

## What I am practicing

The project is mainly focused on improving practical knowledge of:

* separation of responsibilities between application layers
* database access with Entity Framework Core
* designing application logic
* working with relational data
* dependency injection and service registration
* writing maintainable and understandable code

## Project structure

```text
ICanHelpApp
├── ICanHelp.Core
├── ICanHelp.Application
└── ICanHelp.Infrastructure
```

## Development

The application is being developed incrementally. More functionality and an application-facing layer will be added as development continues.
