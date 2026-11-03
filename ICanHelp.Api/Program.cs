using System.Reflection;
using System.Text.Json.Serialization;
using ICanHelp.Api.ErrorHandling;
using ICanHelp.Application;
using ICanHelp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    // Enumy w JSON jako tekst ("Open"), tak samo jak w bazie.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Każdy błąd (wyjątek, 404, błąd walidacji) zwracamy w formacie ProblemDetails (RFC 9457).
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    ctx.ProblemDetails.Instance ??= $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
    ctx.ProblemDetails.Extensions.TryAdd("traceId", ctx.HttpContext.TraceIdentifier);
});
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "ICanHelp API", Version = "v1" });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

var app = builder.Build();

// Wyjątek -> DomainExceptionHandler (400/404/409), a jeśli go nie obsłuży -> 500 jako ProblemDetails.
app.UseExceptionHandler();
// Puste odpowiedzi 4xx/5xx (np. nieistniejący adres) też dostają ciało ProblemDetails.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // Wygoda: http://localhost:5080/ -> Swagger.
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}
else
{
    // W Development profil "http" nie ma portu https – przekierowanie tylko logowałoby ostrzeżenie.
    app.UseHttpsRedirection();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Widoczny dla testów integracyjnych (WebApplicationFactory, Krok 8).</summary>
public partial class Program;
