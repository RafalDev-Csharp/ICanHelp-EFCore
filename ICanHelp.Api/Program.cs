using System.Reflection;
using System.Text.Json.Serialization;
using ICanHelp.Api.ErrorHandling;
using ICanHelp.Api.Notifications;
using ICanHelp.Application;
using ICanHelp.Application.Notifications;
using ICanHelp.Infrastructure;
using Microsoft.OpenApi.Models;

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

// Powiadomienia na żywo (Krok 13). Enumy jako tekst – tak jak w kontrolerach.
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<INotificationSender, SignalRNotificationSender>();

builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "ICanHelp API", Version = "v1" });

    // Niepuste stringi w DTO nie są „nullable” – klient TS dostaje `userName: string`, a nie `string | undefined`.
    o.SupportNonNullableReferenceTypes();

    // Stałe operationId "Kontroler_Akcja" (np. "Users_Login") – NSwag (frontend, npm run api) robi z nich
    // klasy i metody klienta: UsersClient.login(...).
    o.CustomOperationIds(api =>
        $"{api.ActionDescriptor.RouteValues["controller"]}_{api.ActionDescriptor.RouteValues["action"]}");

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));

    // Przycisk "Authorize" w Swaggerze: wklej accessToken z POST /api/users/sign-in (bez słowa "Bearer").
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT z POST /api/users/sign-in"
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
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

// Najpierw "kim jesteś" (JWT z nagłówka Authorization), potem "czy możesz" ([Authorize]).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationsHub>(NotificationsHub.Path);
app.MapHealthChecks("/health");

app.Run();

/// <summary>Widoczny dla testów integracyjnych (WebApplicationFactory, Krok 8).</summary>
public partial class Program;
