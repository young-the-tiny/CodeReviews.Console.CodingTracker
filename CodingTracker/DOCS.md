# Coding Tracker — How It Works

This document explains each component and how data flows through the app. The app is a
small console CRUD tool for coding sessions, layered as: **Config → Model → Data access → UI**.

## Startup flow

`Program.cs` is the entry point. It runs two lines:

```csharp
DatabaseManager.Init();   // create the table if it doesn't exist
new UserInterface().Run(); // enter the interactive menu loop
```

`Init()` runs first so the `coding_sessions` table always exists before the UI touches it.
`Run()` then loops until the user picks **Exit**.

## Configuration — `Config/AppConfig.cs`

`AppConfig` is a static class with a static constructor, so `appsettings.json` is read
**once**, the first time any property is accessed.

```csharp
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)   // look next to the .exe, not the CWD
    .AddJsonFile("appsettings.json")
    .Build();

ConnectionString = config.GetConnectionString("Default")!; // ConnectionStrings:Default
DateFormat       = config["DateFormat"] ?? "yyyy-MM-dd HH:mm";
```

- **`SetBasePath(AppContext.BaseDirectory)`** is important: the `.csproj` copies
  `appsettings.json` to the output folder (`CopyToOutputDirectory=PreserveNewest`), so the
  app finds it regardless of which directory you launch from.
- If `ConnectionStrings:Default` is missing, the constructor throws — failing fast and
  loudly rather than silently using a wrong database.
- `DateFormat` falls back to a sensible default if not present.

Two public properties are exposed for the rest of the app:
`AppConfig.ConnectionString` and `AppConfig.DateFormat`.

## Model — `Models/CodingSession.cs`

```csharp
record CodingSession(int Id, DateTime StartTime, DateTime EndTime)
{
    public TimeSpan Duration => EndTime - StartTime;
}
```

- A positional `record`: immutable, value-based equality, concise.
- **`Duration` is a computed property** — it is derived from `StartTime`/`EndTime` every
  time it's read. It is **never stored** in the database (there is no `Duration` column).
  This keeps the data normalized: you can't have a duration that disagrees with the times.
- Dapper maps a query result row directly onto this record by matching column names
  (`Id`, `StartTime`, `EndTime`) to the constructor parameters.

## Data access — `Database/DatabaseManager.cs`

A static class wrapping Dapper. Every method opens a short-lived connection through one
helper and disposes it with `using`:

```csharp
static SqliteConnection Connection()
{
    var connection = new SqliteConnection(AppConfig.ConnectionString);
    connection.Open();
    return connection;
}
```

This is the key difference from a hand-rolled ADO.NET layer: instead of creating commands,
adding parameters, and reading a `DataReader` by hand, each operation is a single Dapper call.

| Method | What it does | Dapper call |
|--------|--------------|-------------|
| `Init()` | Creates the `coding_sessions` table if missing | `connection.Execute(createTableSql)` |
| `Add(start, end)` | Inserts a new session | `Execute("INSERT ...", new { startTime, endTime })` |
| `Update(id, start, end)` | Updates a session by Id | `Execute("UPDATE ... WHERE Id=@id", new { id, startTime, endTime })` |
| `Delete(id)` | Deletes a session by Id | `Execute("DELETE ... WHERE Id=@id", new { id })` |
| `Exists(id)` | Returns whether an Id exists | `ExecuteScalar<long>("SELECT COUNT(1) ...") > 0` |
| `All()` | Returns all sessions, newest first | `Query<CodingSession>("SELECT ... ORDER BY StartTime DESC").ToList()` |

How Dapper helps here:

- **Parameters** — passing an anonymous object like `new { id }` binds `@id` safely
  (parameterized query, no SQL injection). No manual `AddWithValue` calls.
- **Reads** — `Query<CodingSession>(...)` runs the SQL and materializes each row into a
  `CodingSession` automatically by name. No `DataReader` loop, no manual `GetInt32`/`GetString`.
- **Scalars** — `ExecuteScalar<long>(...)` returns the single count value already typed.

### Schema

```sql
CREATE TABLE IF NOT EXISTS coding_sessions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    StartTime TEXT NOT NULL,
    EndTime TEXT NOT NULL);
```

`StartTime`/`EndTime` are stored as `TEXT`. Microsoft.Data.Sqlite + Dapper round-trip
`DateTime` to/from an ISO-8601 string automatically, so the C# code works with real
`DateTime` values on both sides.

## User interface — `UI/UserInterface.cs`

`Run()` is the main loop, built on Spectre.Console:

1. Clear the screen and show a `SelectionPrompt` menu.
2. Dispatch the chosen option to a handler method.
3. After the handler returns, wait for a keypress, then loop.
4. **Exit** returns out of the loop, ending the program.

### Menu actions

- **View Records** — calls `All()` and renders a Spectre `Table` of
  Id / Start / End / Duration. Empty database shows a friendly message instead.
- **Add Record** — prompts for start and end times (parsed using `AppConfig.DateFormat`),
  validates that end is after start, asks for confirmation, then calls `Add(...)`.
- **Start Live Session** — the stopwatch:
  ```csharp
  var start = DateTime.Now;
  while (!Console.KeyAvailable)               // poll for a keypress
  {
      AnsiConsole.Markup($"\rElapsed: {FormatDuration(DateTime.Now - start)}");
      Thread.Sleep(250);                      // redraw 4×/second
  }
  Console.ReadKey(true);
  DatabaseManager.Add(start, DateTime.Now);   // save when stopped
  ```
  It records the start, redraws the running elapsed time on one line until any key is
  pressed, then saves the session and reports its duration.
- **Update Record** — asks for an Id, validates it with `Exists(id)`, prompts for new
  times (same validation as Add), confirms, then calls `Update(...)`.
- **Delete Record** — asks for an Id, validates with `Exists(id)`, confirms, calls `Delete(...)`.
- **Reports** — loads `All()` and computes, in C# with LINQ:
  - total session count
  - total time (`Aggregate` summing each `Duration`)
  - average per session (`total / count`)
  - this week (sessions since the most recent Sunday)
  - this month (sessions since the 1st)

  These are aggregated in code rather than SQL because the dataset is small; the logic
  stays in one place and is easy to read.

### Input & validation helpers

- `PromptDate(label)` — a `TextPrompt<DateTime>`; Spectre re-prompts automatically until
  the input parses, showing the expected `DateFormat`.
- `PromptExistingId()` — reads an int Id and returns it only if `Exists` is true, otherwise
  prints an error and returns `null` so the caller aborts.
- `Confirm(message)` — a yes/no `ConfirmationPrompt` shown before every mutation.
- `FormatDuration(TimeSpan)` — formats as `HH:MM:SS` (hours can exceed 24).

## Data flow summary

```
appsettings.json
      │  (read once at startup)
      ▼
  AppConfig ──ConnectionString──► DatabaseManager ──Dapper──► SQLite (coding-tracker.db)
      │                                  ▲                          │
   DateFormat                            │  CodingSession records   │
      │                                  └──────────────────────────┘
      ▼
 UserInterface (Spectre.Console menu)  ◄── user input / display
```

## Extending it

- **New field on a session** — add it to the `CREATE TABLE`, the `CodingSession` record,
  and the relevant `INSERT`/`UPDATE` SQL + UI prompts. Dapper picks it up by name.
- **New report** — add a LINQ computation in `Reports()` and a row to the table.
- **Different database location** — change `ConnectionStrings:Default` in `appsettings.json`;
  no code change needed.
