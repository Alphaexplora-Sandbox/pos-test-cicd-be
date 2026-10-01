using PosTestCicdBackend.Domain;

var builder = WebApplication.CreateBuilder(args);

// Registered as an interface so a test can replace it without replacing
// the host. A concrete registration would leave nothing to substitute.
builder.Services.AddSingleton<IServiceStatus, ServiceStatus>();

// Describes the API so the contract tests have something to generate
// from. Document only — Swagger UI is a separate package and is not
// referenced, so nothing new is published by the running service.
builder.Services.AddOpenApi();

// A browser only lets a frontend on another origin call this API when that
// origin is listed here. ALPHACI sets CORS_ORIGINS to the deployed
// frontend's address on managed hosting; a local run falls back to the
// frontend dev server.
var corsOrigins = (builder.Configuration["CORS_ORIGINS"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

// The production gate probes /health after a deployment, so this endpoint
// is part of the pipeline contract rather than a convenience.
// .Produces<T>() is what puts a response SHAPE in the document. Without
// it the schema says an endpoint exists and nothing about what it
// returns, and a contract test generated from that can only check the
// status code — it would pass against an endpoint returning anything.
app.MapGet("/health", (IServiceStatus status) =>
    Results.Ok(new HealthResponse(status.CurrentStatus(), ServiceInfo.Name)))
   .Produces<HealthResponse>(StatusCodes.Status200OK);

app.MapGet("/", () => Results.Ok(new HealthResponse("ready", ServiceInfo.Name)))
   .Produces<HealthResponse>(StatusCodes.Status200OK);

app.Run();

public record HealthResponse(string Status, string Service);

public static class ServiceInfo
{
    public const string Name = "pos-test-cicd-backend";
}

// Exposed so the test project can host the application in memory.
public partial class Program;
