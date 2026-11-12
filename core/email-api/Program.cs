using EmailApi.Extensions;
using PlatformKit;

var builder = WebApplication.CreateBuilder(args);

// Real development secrets (the SMTP password) live in this git-ignored file, not in appsettings.Development.json.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
}

builder.Services.AddPostgres(builder.Configuration);
builder.Services.AddInternalOnlyAuth(builder.Environment);
builder.Services.AddEmailServices(builder.Environment, builder.Configuration);
builder.Services.AddRequestContext();
builder.Services.AddPlatformProblemDetails();
builder.Services.AddControllers();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseRequestContext();
app.UseAuthorization();

app.MapControllers();
app.MapPlatformHealth();

app.Run();
