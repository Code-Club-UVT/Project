using System.Reflection;
using Identity.Controllers;
using Identity.Tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformKit.Internal;
using System.Text.Json;
using Xunit;

namespace Identity.Tests;

/// <summary>What protects each endpoint, checked on the attributes, so a new controller cannot slip out unprotected.</summary>
public class ControllerAttributeTests
{
    private static readonly Type[] Controllers = typeof(AuthController).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
        .ToArray();

    [Fact]
    public void The_controllers_are_the_ones_we_know_so_a_new_one_has_to_be_looked_at()
    {
        Assert.Equal(
            ["AccountController", "AuthController", "InternalProjectTokensController", "WellKnownController"],
            Controllers.Select(c => c.Name).Order());
    }

    [Fact]
    public void Only_login_and_the_published_keys_are_open_to_anyone()
    {
        var anonymous = Controllers.Where(c => c.GetCustomAttributes<AllowAnonymousAttribute>().Any()).Select(c => c.Name).Order();

        Assert.Equal(["AuthController", "WellKnownController"], anonymous);
    }

    [Fact]
    public void Changing_a_password_needs_a_logged_in_user()
    {
        var authorize = Assert.Single(typeof(AccountController).GetCustomAttributes<AuthorizeAttribute>());

        Assert.Null(authorize.AuthenticationSchemes);
        Assert.Null(authorize.Roles);
        Assert.DoesNotContain(typeof(AccountController).GetMethods(), m => m.GetCustomAttributes<AllowAnonymousAttribute>().Any());
    }

    [Fact]
    public void Project_tokens_can_only_be_asked_for_with_the_internal_key_and_outside_the_api_path()
    {
        var controller = typeof(InternalProjectTokensController);

        Assert.Equal(InternalAuth.SchemeName, Assert.Single(controller.GetCustomAttributes<AuthorizeAttribute>()).AuthenticationSchemes);
        Assert.Equal("internal/project-tokens", Assert.Single(controller.GetCustomAttributes<RouteAttribute>()).Template);
    }

    [Fact]
    public void The_public_account_endpoints_live_under_the_api_path_the_gateway_routes()
    {
        Assert.Equal("api/[controller]", typeof(AuthController).GetCustomAttributes<RouteAttribute>(inherit: true).Single().Template);
        Assert.Equal("api/[controller]", typeof(AccountController).GetCustomAttributes<RouteAttribute>(inherit: true).Single().Template);
    }

    [Fact]
    public void The_published_keys_hold_only_the_public_half_of_the_key()
    {
        using var signing = new IdentityFixture().SigningKey;
        var controller = new WellKnownController(signing);

        var json = JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(controller.GetJwks()).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var key = document.RootElement.GetProperty("keys")[0];
        Assert.Equal("EC", key.GetProperty("kty").GetString());
        Assert.False(key.TryGetProperty("d", out _), "the private key must never be published");
    }
}
