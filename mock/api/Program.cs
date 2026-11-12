using PlatformKit;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// The gateway is the only caller. Trust its X-Forwarded-For (it carries the real client address) from the private Docker range.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
});

builder.Services.AddPlatformAuthentication(builder.Configuration);
builder.Services.AddRequestContext();
builder.Services.AddPlatformHealth();
builder.Services.AddControllers();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseRequestContext();
app.UseAuthorization();

app.MapPlatformHealth();
app.MapControllers();

app.Run();
