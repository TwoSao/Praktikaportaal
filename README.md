# Praktikaportaal

A web application connecting **students looking for internships** with **companies offering internship opportunities**.

## Features
- Layered solution with a REST API, MVC web front end and a background worker
- Automated tests in `Praktikaportaal.Tests`
- Dockerfile for the API

## Tech stack
C# · ASP.NET Core · Entity Framework Core · Docker

## Project structure
```
Praktikaportaal.Core            domain models and interfaces
Praktikaportaal.Infrastructure  data access and services
Praktikaportaal.API             REST API
Praktikaportaal.Web             MVC web front end
Praktikaportaal.Worker          background jobs
Praktikaportaal.Tests           automated tests
```

## Getting started
```bash
git clone https://github.com/TwoSao/Praktikaportaal.git
cd Praktikaportaal
dotnet restore
dotnet run --project Praktikaportaal.Web
```
