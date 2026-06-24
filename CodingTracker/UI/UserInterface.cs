using Spectre.Console;
using CodingTracker.Config;
using CodingTracker.Database;
using CodingTracker.Models;

namespace CodingTracker.UI;

internal class UserInterface
{
    public void Run()
    {
        while (true)
        {
            AnsiConsole.Clear();
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Coding Tracker[/]")
                    .AddChoices(
                        "View Records",
                        "Add Record",
                        "Start Live Session",
                        "Update Record",
                        "Delete Record",
                        "Reports",
                        "Exit"));

            switch (choice)
            {
                case "View Records": ViewRecords(); break;
                case "Add Record": AddRecord(); break;
                case "Start Live Session": LiveSession(); break;
                case "Update Record": UpdateRecord(); break;
                case "Delete Record": DeleteRecord(); break;
                case "Reports": Reports(); break;
                case "Exit": return;
            }

            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            AnsiConsole.Console.Input.ReadKey(true);
        }
    }


    static void ViewRecords()
    {
        var sessions = DatabaseManager.All();
        if (sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No records yet.[/]");
            return;
        }

        var table = new Table()
            .AddColumn("Id")
            .AddColumn("Start")
            .AddColumn("End")
            .AddColumn("Duration");

        foreach (var s in sessions)
            table.AddRow(
                s.Id.ToString(),
                s.StartTime.ToString(AppConfig.DateFormat),
                s.EndTime.ToString(AppConfig.DateFormat),
                FormatDuration(s.Duration));

        AnsiConsole.Write(table);
    }

    static void AddRecord()
    {
        var start = PromptDate("Start time");
        var end = PromptDate("End time");

        if (end <= start)
        {
            AnsiConsole.MarkupLine("[red]End time must be after start time.[/]");
            return;
        }

        if (!Confirm($"Add session {start.ToString(AppConfig.DateFormat)} -> {end.ToString(AppConfig.DateFormat)}?"))
            return;

        DatabaseManager.Add(start, end);
        AnsiConsole.MarkupLine("[green]Record added.[/]");
    }

    static void LiveSession()
    {
        var start = DateTime.Now;
        AnsiConsole.MarkupLine($"[green]Session started at {start.ToString(AppConfig.DateFormat)}.[/] Press any key to stop...");

        AnsiConsole.Live(new Markup(""))
            .Start(ctx =>
            {
                while (!Console.KeyAvailable)
                {
                    ctx.UpdateTarget(new Markup($"Time: [yellow]{FormatDuration(DateTime.Now - start)}[/]"));
                    ctx.Refresh();
                    Thread.Sleep(1000);
                }
            });
        Console.ReadKey(true);

        var end = DateTime.Now;
        DatabaseManager.Add(start, end);
        AnsiConsole.MarkupLine($"[green]Session saved.[/] Duration: {FormatDuration(end - start)}");
    }

    static void UpdateRecord()
    {
        var id = PromptExistingId();
        if (id is null) return;

        var start = PromptDate("New start time");
        var end = PromptDate("New end time");

        if (end <= start)
        {
            AnsiConsole.MarkupLine("[red]End time must be after start time.[/]");
            return;
        }

        if (!Confirm($"Update record {id}?")) return;

        DatabaseManager.Update(id.Value, start, end);
        AnsiConsole.MarkupLine("[green]Record updated.[/]");
    }

    static void DeleteRecord()
    {
        var id = PromptExistingId();
        if (id is null) return;

        if (!Confirm($"Delete record {id}?")) return;

        DatabaseManager.Delete(id.Value);
        AnsiConsole.MarkupLine("[green]Record deleted.[/]");
    }

    static void Reports()
    {
        var sessions = DatabaseManager.All();
        if (sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No records to report on.[/]");
            return;
        }

        var total = sessions.Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Duration);
        var average = total / sessions.Count;

        var now = DateTime.Now;
        var weekStart = now.Date.AddDays(-(int)now.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var thisWeek = sessions.Where(s => s.StartTime >= weekStart)
            .Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Duration);
        var thisMonth = sessions.Where(s => s.StartTime >= monthStart)
            .Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Duration);

        var table = new Table().AddColumn("Metric").AddColumn("Value");
        table.AddRow("Total sessions", sessions.Count.ToString());
        table.AddRow("Total time", FormatDuration(total));
        table.AddRow("Average per session", FormatDuration(average));
        table.AddRow("This week", FormatDuration(thisWeek));
        table.AddRow("This month", FormatDuration(thisMonth));

        AnsiConsole.Write(table);
    }

    // --- helpers ---

    static DateTime PromptDate(string label)
    {
        var input = AnsiConsole.Prompt(
            new TextPrompt<string>($"{label} ([grey]{AppConfig.DateFormat}[/]):")
                .Validate(value =>
                    DateTime.TryParseExact(
                        value,
                        AppConfig.DateFormat,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out _)
                        ? ValidationResult.Success()
                        : ValidationResult.Error($"[red]Use the format {AppConfig.DateFormat}[/]")));

        return DateTime.ParseExact(
            input,
            AppConfig.DateFormat,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None);
    }

    static int? PromptExistingId()
    {
        var id = AnsiConsole.Prompt(new TextPrompt<int>("Record [green]Id[/]:"));
        if (DatabaseManager.Exists(id)) return id;

        AnsiConsole.MarkupLine($"[red]No record with Id {id}.[/]");
        return null;
    }

    static bool Confirm(string message) =>
        AnsiConsole.Prompt(new ConfirmationPrompt(message));

    static string FormatDuration(TimeSpan d) =>
        $"{(int)d.TotalHours:D2}:{d.Minutes:D2}:{d.Seconds:D2}";
}
