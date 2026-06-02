var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// SPA fallback: any non-API, non-file route serves index.html
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
