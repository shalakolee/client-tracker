using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/health"));

app.MapGet("/health", async () =>
{
    var connectionString =
        Environment.GetEnvironmentVariable("CLIENTTRACKER_MYSQL_CONNECTION")
        ?? builder.Configuration.GetConnectionString("ClientTracker");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem("Missing MySQL connection string. Set CLIENTTRACKER_MYSQL_CONNECTION or ConnectionStrings:ClientTracker.", statusCode: 500);
    }

    try
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand("SELECT 1", conn);
        _ = await cmd.ExecuteScalarAsync();
        return Results.Ok(new { status = "ok" });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

app.Run();
