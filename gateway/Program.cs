using Gateway.Data;
using Gateway.Extensions;
using Gateway.Pipeline;
using Gateway.Routing;
using PlatformKit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGatewayForwardedHeaders(builder.Configuration);
builder.Services.AddGatewayOptions(builder.Environment);
builder.Services.AddGatewayData(builder.Configuration);
builder.Services.AddPlatformAuthentication(builder.Configuration);
builder.Services.AddRequestContext();
builder.Services.AddGatewayRateLimiting();
builder.Services.AddGatewayCors();
builder.Services.AddGatewayProxy();
builder.Services.AddPlatformHealth().AddCheck<GatewayDbHealthCheck>("postgres").AddCheck<RoutesLoadedHealthCheck>("routes");

var app = builder.Build();

// First, so rate limiting, logging and the forwarded X-Forwarded-For all see the real client address.
app.UseForwardedHeaders();

app.UseCors();
app.UseAuthentication();
app.UseRequestContext();
app.UseRateLimiter();
app.UseAuthorization();

app.MapPlatformHealth();
app.MapReverseProxy(proxy =>
{
    proxy.UseMiddleware<ProjectAccessMiddleware>();
    proxy.UseSessionAffinity();
    proxy.UseLoadBalancing();
    proxy.UsePassiveHealthChecks();
});

app.Run();
