# Coding Tracker

A .NET 10 console app for tracking coding sessions. Each record is a session with a
start and end time; the **duration is computed, not stored**. Records can be entered
manually or timed live with a built-in stopwatch, and a reports view summarizes totals.

Built with **Dapper** over **SQLite**, a **Spectre.Console** UI, and configuration read
from **`appsettings.json`** via `Microsoft.Extensions.Configuration`.

## Features

- View, add, update, and delete coding sessions (CRUD)
- **Live stopwatch** — start a session and stop it with a keypress; it saves automatically
- **Reports** — total sessions, total time, average session length, this week, this month

## Requirements

- .NET 10 SDK

## Run

From the project directory (the folder containing `CodingTracker.csproj`):

```sh
dotnet run
```

The SQLite database file (`coding-tracker.db` by default) is created next to the
executable on first run, and `appsettings.json` is copied to the output folder.

## Configuration

Edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=coding-tracker.db"
  },
  "Database": {
    "Path": "coding-tracker.db"
  },
  "DateFormat": "yyyy-MM-dd HH:mm"
}
```

- `ConnectionStrings:Default` — the SQLite connection string Dapper uses.
- `Database:Path` — the database file path, surfaced separately for reference.
- `DateFormat` — how dates are parsed on input and shown on screen.

## Project layout

```
CodingTracker/
├── appsettings.json            App configuration (copied to output)
├── Program.cs                  Entry point: Init() then Run()
├── Config/AppConfig.cs         Reads appsettings.json once at startup
├── Models/CodingSession.cs     record(Id, StartTime, EndTime) with computed Duration
├── Database/DatabaseManager.cs Dapper data access (CRUD)
└── UI/UserInterface.cs         Spectre.Console menu loop
```
