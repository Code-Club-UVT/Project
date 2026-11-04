using CoreApi.Common;
using CoreApi.Extensions;
using CoreApi.Services.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Real development secrets (SMTP password, ...) live in this git-ignored file, not in appsettings.Development.json.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
}

// Add services to the container.

builder.Services.AddPostgres(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddServices(builder.Environment, builder.Configuration);
builder.Services.AddAuth(builder.Environment);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

// Fail at startup, not on the first request, if the signing key is missing or invalid.
app.Services.GetRequiredService<ISigningKeyProvider>();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "CoreApi v1"));
}

// TLS, CORS and rate limiting are handled in front of this API (the gateway is the only caller). A redirect to https here
// would break the gateway's plain-http calls whenever an https port is configured.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
