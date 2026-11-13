using Choco.Application;
using Choco.Infrastructure;
using Choco.Infrastructure.Health;
using Choco.Presentation;
using Choco.Presentation.Health;
using Choco.Presentation.Notifications;
using Choco.Presentation.OpenApi;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPresentation(builder.Configuration);

WebApplication app = builder.Build();

app.UseResponseCompression();
app.UseHttpLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseApiDocumentation();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationsHub>(NotificationsHub.Path);
HealthCheckOptions liveness = new() { Predicate = _ => false };
app.MapHealthChecks("/health", liveness).WithHttpLogging(HttpLoggingFields.None);
app.MapHealthChecks("/health/live", liveness).WithHttpLogging(HttpLoggingFields.None);
app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
            ResponseWriter = ReadinessResponseWriter.WriteAsync
        })
    .WithHttpLogging(HttpLoggingFields.None);

app.Run();
