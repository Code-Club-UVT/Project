using System.Reflection;
using System.Security.Claims;
using CoreApi.Controllers;
using CoreApi.Extensions;
using CoreApi.Models.Requests;
using CoreApi.Models.Responses;
using CoreApi.Services.ProjectMembers;
using CoreApi.Services.Projects;
using CoreApi.Services.UserTabs;
using CoreApi.Services.Users;
using CoreApi.Tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PlatformKit.Internal;
using Xunit;

namespace CoreApi.Tests;

/// <summary>What protects each endpoint, checked on the attributes, so a new controller cannot slip out unprotected.</summary>
public class AuthorizationAttributeTests
{
    private static readonly Type[] Controllers = typeof(UsersController).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
        .ToArray();

    [Fact]
    public void Every_controller_requires_authentication()
    {
        Assert.NotEmpty(Controllers);
        foreach (var controller in Controllers)
        {
            Assert.True(
                controller.GetCustomAttributes<AuthorizeAttribute>().Any(),
                $"{controller.Name} has no [Authorize]");
            Assert.False(
                controller.GetCustomAttributes<AllowAnonymousAttribute>().Any()
                || controller.GetMethods().Any(m => m.GetCustomAttributes<AllowAnonymousAttribute>().Any()),
                $"{controller.Name} allows anonymous access");
        }
    }

    [Fact]
    public void Project_management_is_for_admins_only()
    {
        var authorize = typeof(ProjectsController).GetCustomAttributes<AuthorizeAttribute>().Single();

        Assert.Equal(CoreRoles.Admin, authorize.Roles);
    }

    [Fact]
    public void Internal_endpoints_use_the_internal_key_scheme_and_live_outside_the_api_path()
    {
        var controller = typeof(InternalUsersController);

        Assert.Equal(InternalAuth.SchemeName, controller.GetCustomAttributes<AuthorizeAttribute>().Single().AuthenticationSchemes);
        Assert.StartsWith("internal/", controller.GetCustomAttributes<RouteAttribute>().Single().Template);
    }

    [Fact]
    public void Only_the_internal_controller_is_outside_the_api_path()
    {
        foreach (var controller in Controllers.Where(c => c != typeof(InternalUsersController)))
        {
            var template = controller.GetCustomAttributes<RouteAttribute>(inherit: true).Single().Template;
            Assert.StartsWith("api/", template);
        }
    }

    [Fact]
    public void Listing_all_users_is_for_admins_only_and_the_other_user_endpoints_are_for_any_member()
    {
        var all = typeof(UsersController).GetMethod(nameof(UsersController.GetUsers))!;

        Assert.Equal(CoreRoles.Admin, all.GetCustomAttributes<AuthorizeAttribute>().Single().Roles);
        foreach (var name in new[] { nameof(UsersController.GetMe), nameof(UsersController.UpdateMe), nameof(UsersController.GetMyProjects), nameof(UsersController.GetMyTabs), nameof(UsersController.SaveMyTabs) })
        {
            Assert.Empty(typeof(UsersController).GetMethod(name)!.GetCustomAttributes<AuthorizeAttribute>());
        }
    }
}

public class UsersControllerTabsTests
{
    private sealed class FakeProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(HttpContext httpContext, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null)
            => new() { Status = statusCode, Title = title, Detail = detail };

        public override ValidationProblemDetails CreateValidationProblemDetails(HttpContext httpContext, ModelStateDictionary modelStateDictionary, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null)
            => new(modelStateDictionary) { Status = statusCode, Title = title, Detail = detail };
    }

    private readonly CoreFixture _f = new();

    private UsersController ControllerFor(Guid userId)
    {
        var identity = new ClaimsIdentity([new Claim("sub", userId.ToString())], "test");
        return new UsersController(_f.UserService(), _f.MemberService(), _f.TabService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } },
            ProblemDetailsFactory = new FakeProblemDetailsFactory()
        };
    }

    [Fact]
    public async Task Tabs_are_saved_and_read_for_the_user_in_the_token_only()
    {
        var ada = _f.AddProfile("ada");
        var grace = _f.AddProfile("grace");
        var project = _f.AddProject("mock", frontend: "http://web:8080");
        _f.AddMember(ada, project, _f.AddRole(project));
        _f.AddMember(grace, project, _f.AddRole(project, "Other"));

        var saved = await ControllerFor(ada.UserId).SaveMyTabs(new SaveUserTabsRequest { Tabs = [new UserTabRequest { ProjectId = project.Id, LastPath = "/about" }] }, default);
        var adas = await ControllerFor(ada.UserId).GetMyTabs(default);
        var graces = await ControllerFor(grace.UserId).GetMyTabs(default);

        Assert.IsType<OkObjectResult>(saved);
        var list = Assert.IsAssignableFrom<IReadOnlyList<UserTabResponse>>(Assert.IsType<OkObjectResult>(adas).Value);
        Assert.Equal([(project.Id, "/about")], list.Select(t => (t.ProjectId, t.LastPath)));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<UserTabResponse>>(Assert.IsType<OkObjectResult>(graces).Value));
    }

    [Fact]
    public async Task A_bad_path_is_a_400_with_its_code()
    {
        var ada = _f.AddProfile("ada");

        var result = await ControllerFor(ada.UserId).SaveMyTabs(new SaveUserTabsRequest { Tabs = [new UserTabRequest { ProjectId = Guid.NewGuid(), LastPath = "//evil" }] }, default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("UserTab.InvalidPath", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public void A_token_without_a_valid_subject_is_refused()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "not-a-guid")], "test"));

        Assert.Throws<InvalidOperationException>(() => principal.GetUserId());
    }
}
