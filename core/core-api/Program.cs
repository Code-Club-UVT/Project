using CoreApi.Common;
using CoreApi.Extensions;
using PlatformKit;

var builder = WebApplication.CreateBuilder(args);

// Real development secrets live in this git-ignored file, not in appsettings.Development.json.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
}

builder.Services.AddPostgres(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddServices();
builder.Services.AddAuth(builder.Configuration, builder.Environment);
builder.Services.AddRequestContext();
builder.Services.AddPlatformProblemDetails();

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "CoreApi v1"));
}

// TLS, CORS and rate limiting are handled in front of this API (the gateway is the only caller). A redirect to https here
// would break the gateway's plain-http calls whenever an https port is configured.
app.UseAuthentication();
app.UseRequestContext();
app.UseAuthorization();

app.MapControllers();
app.MapPlatformHealth();

app.Run();
