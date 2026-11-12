using CoreApi.Enums;
using CoreApi.Models.Requests;
using CoreApi.Services.Projects;
using CoreApi.Tests.Support;
using Xunit;

namespace CoreApi.Tests;

public class ProjectServiceTests
{
    private readonly CoreFixture _f = new();

    private static CreateProjectRequest Create(string slug = "mock", string? backend = "http://mock-api:8080/api", string? frontend = null, string? health = null)
        => new() { Slug = slug, Name = "Mock", BackendUrl = backend, FrontendUrl = frontend, HealthUrl = health };

    private static UpdateProjectRequest Update(string name = "Renamed", string? backend = "http://mock-api:8080/api", string? frontend = null, string? health = null, bool active = true, Guid? defaultRole = null)
        => new() { Name = name, BackendUrl = backend, FrontendUrl = frontend, HealthUrl = health, IsActive = active, DefaultRoleId = defaultRole };

    // ---- create

    [Fact]
    public async Task A_new_project_is_an_active_exchange_project_and_not_a_system_project()
    {
        var result = await _f.ProjectService().CreateAsync(Create());

        Assert.True(result.IsSuccess);
        var project = Assert.Single(_f.Projects.Items);
        Assert.Equal(ProjectTokenMode.Exchange, project.TokenMode);
        Assert.True(project.IsActive);
        Assert.False(project.IsSystem);
        Assert.Null(project.DefaultRoleId);
        Assert.Equal(_f.Clock.UtcNow, project.CreatedAt);
        Assert.Equal(1, _f.UnitOfWork.Saves);
        Assert.Equal(project.Id, result.Value.Id);
        Assert.Equal("Exchange", result.Value.TokenMode);
    }

    [Fact]
    public async Task The_slug_is_trimmed_and_lowercased_and_the_urls_are_trimmed()
    {
        await _f.ProjectService().CreateAsync(Create("  My-App ", backend: " http://a:8080/api ", frontend: " http://b:8080 ", health: " http://a:8080/health "));

        var project = _f.Projects.Items.Single();
        Assert.Equal("my-app", project.Slug);
        Assert.Equal("http://a:8080/api", project.BackendUrl);
        Assert.Equal("http://b:8080", project.FrontendUrl);
        Assert.Equal("http://a:8080/health", project.HealthUrl);
    }

    [Fact]
    public async Task Blank_urls_and_description_become_null()
    {
        var request = Create(backend: " ", frontend: "") with { Description = "  " };

        await _f.ProjectService().CreateAsync(request);

        var project = _f.Projects.Items.Single();
        Assert.Null(project.BackendUrl);
        Assert.Null(project.FrontendUrl);
        Assert.Null(project.Description);
    }

    [Theory]
    [InlineData("http://mock-api:8080/api", null)]
    [InlineData(null, "http://mock-web:8080")]
    [InlineData("http://mock-api:8080/api", "http://mock-web:8080")]
    [InlineData(null, null)]
    public async Task A_project_may_have_a_backend_a_frontend_both_or_neither(string? backend, string? frontend)
    {
        var result = await _f.ProjectService().CreateAsync(Create(backend: backend, frontend: frontend));

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("core")]
    [InlineData("identity")]
    [InlineData("email")]
    [InlineData("gateway")]
    [InlineData("internal")]
    [InlineData("health")]
    [InlineData("admin")]
    [InlineData("CORE")]
    [InlineData(" Gateway ")]
    public async Task Reserved_slugs_are_refused(string slug)
    {
        var result = await _f.ProjectService().CreateAsync(Create(slug));

        Assert.Same(ProjectErrors.SlugReserved, result.Error);
        Assert.Empty(_f.Projects.Items);
    }

    [Fact]
    public async Task A_taken_slug_is_a_conflict_even_in_another_case()
    {
        _f.AddProject("mock");

        var result = await _f.ProjectService().CreateAsync(Create("MOCK"));

        Assert.Same(ProjectErrors.SlugTaken, result.Error);
    }

    [Fact]
    public async Task A_health_url_needs_a_backend()
    {
        var result = await _f.ProjectService().CreateAsync(Create(backend: null, frontend: "http://web:8080", health: "http://api:8080/health"));

        Assert.Equal("Project.InvalidUrl", result.Error!.Code);
        Assert.Contains("HealthUrl needs a BackendUrl", result.Error.Message);
    }

    [Theory]
    [InlineData("ftp://x")]
    [InlineData("http://user:pw@x")]
    [InlineData("http://x/?q=1")]
    public async Task An_invalid_url_is_refused_and_named(string url)
    {
        var backend = await _f.ProjectService().CreateAsync(Create(backend: url));
        var frontend = await _f.ProjectService().CreateAsync(Create("other", frontend: url));

        Assert.Contains("BackendUrl", backend.Error!.Message);
        Assert.Contains("FrontendUrl", frontend.Error!.Message);
        Assert.Empty(_f.Projects.Items);
    }

    [Fact]
    public async Task Local_addresses_are_refused_in_production_and_allowed_in_development()
    {
        var production = await _f.ProjectService().CreateAsync(Create(backend: "http://localhost:5000"));
        _f.Environment.EnvironmentName = "Development";
        var development = await _f.ProjectService().CreateAsync(Create(backend: "http://localhost:5000"));

        Assert.False(production.IsSuccess);
        Assert.True(development.IsSuccess);
    }

    // ---- update

    [Fact]
    public async Task Updating_replaces_the_editable_fields()
    {
        var project = _f.AddProject("mock", backend: "http://old:8080/api");
        _f.Clock.Advance(TimeSpan.FromHours(1));

        var result = await _f.ProjectService().UpdateAsync(project.Id, Update("New name", "http://new:8080/api", "http://web:8080", "http://new:8080/health", active: false));

        Assert.True(result.IsSuccess);
        Assert.Equal("New name", project.Name);
        Assert.Equal("http://new:8080/api", project.BackendUrl);
        Assert.Equal("http://web:8080", project.FrontendUrl);
        Assert.Equal("http://new:8080/health", project.HealthUrl);
        Assert.False(project.IsActive);
        Assert.Equal(_f.Clock.UtcNow, project.UpdatedAt);
        Assert.Equal("mock", project.Slug);
    }

    [Fact]
    public async Task Updating_an_unknown_project_is_not_found()
    {
        var result = await _f.ProjectService().UpdateAsync(Guid.NewGuid(), Update());

        Assert.Same(ProjectErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Removing_the_backend_makes_a_health_url_invalid()
    {
        var project = _f.AddProject("mock");

        var result = await _f.ProjectService().UpdateAsync(project.Id, Update(backend: null, health: "http://x:8080/health"));

        Assert.Equal("Project.InvalidUrl", result.Error!.Code);
    }

    [Fact]
    public async Task A_system_project_keeps_its_urls_and_stays_active_but_may_be_renamed()
    {
        var core = _f.AddProject("core", backend: "http://core-api:8080/api", system: true);

        Assert.Same(ProjectErrors.SystemProtected, (await _f.ProjectService().UpdateAsync(core.Id, Update(backend: "http://evil:8080/api"))).Error);
        Assert.Same(ProjectErrors.SystemProtected, (await _f.ProjectService().UpdateAsync(core.Id, Update(backend: "http://core-api:8080/api", frontend: "http://x:80"))).Error);
        Assert.Same(ProjectErrors.SystemProtected, (await _f.ProjectService().UpdateAsync(core.Id, Update(backend: "http://core-api:8080/api", active: false))).Error);

        var renamed = await _f.ProjectService().UpdateAsync(core.Id, Update("Core API", "http://core-api:8080/api"));
        Assert.True(renamed.IsSuccess);
        Assert.Equal("Core API", core.Name);
    }

    [Fact]
    public async Task The_default_role_must_belong_to_the_project()
    {
        var project = _f.AddProject("mock");
        var other = _f.AddProject("other");
        var foreign = _f.AddRole(other);
        var own = _f.AddRole(project);
        var service = _f.ProjectService();

        Assert.Same(ProjectErrors.InvalidDefaultRole, (await service.UpdateAsync(project.Id, Update(defaultRole: foreign.Id))).Error);
        Assert.Same(ProjectErrors.InvalidDefaultRole, (await service.UpdateAsync(project.Id, Update(defaultRole: Guid.NewGuid()))).Error);

        Assert.True((await service.UpdateAsync(project.Id, Update(defaultRole: own.Id))).IsSuccess);
        Assert.Equal(own.Id, project.DefaultRoleId);
    }

    [Fact]
    public async Task Leaving_the_default_role_out_makes_the_project_invite_only()
    {
        var project = _f.AddProject("mock");
        project.DefaultRoleId = _f.AddRole(project).Id;

        await _f.ProjectService().UpdateAsync(project.Id, Update(defaultRole: null));

        Assert.Null(project.DefaultRoleId);
    }

    [Fact]
    public async Task A_pass_through_project_cannot_have_a_default_role()
    {
        var identity = _f.AddProject("identity", system: true, mode: ProjectTokenMode.PassThrough, backend: "http://identity-api:8080/api");
        var role = _f.AddRole(identity);

        var result = await _f.ProjectService().UpdateAsync(identity.Id, Update(backend: "http://identity-api:8080/api", defaultRole: role.Id));

        Assert.Same(ProjectErrors.NoMemberships, result.Error);
    }

    // ---- delete

    [Fact]
    public async Task Deleting_removes_the_project()
    {
        var project = _f.AddProject("mock");

        var result = await _f.ProjectService().DeleteAsync(project.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_f.Projects.Items);
    }

    [Fact]
    public async Task A_default_role_is_cleared_in_a_save_of_its_own_before_the_delete()
    {
        var project = _f.AddProject("mock");
        project.DefaultRoleId = _f.AddRole(project).Id;

        await _f.ProjectService().DeleteAsync(project.Id);

        Assert.Equal(2, _f.UnitOfWork.Saves);
        Assert.Null(project.DefaultRoleId);
    }

    [Fact]
    public async Task A_project_without_a_default_role_is_deleted_in_one_save()
    {
        var project = _f.AddProject("mock");

        await _f.ProjectService().DeleteAsync(project.Id);

        Assert.Equal(1, _f.UnitOfWork.Saves);
    }

    [Fact]
    public async Task System_and_unknown_projects_cannot_be_deleted()
    {
        var core = _f.AddProject("core", system: true);

        Assert.Same(ProjectErrors.SystemProtected, (await _f.ProjectService().DeleteAsync(core.Id)).Error);
        Assert.Same(ProjectErrors.NotFound, (await _f.ProjectService().DeleteAsync(Guid.NewGuid())).Error);
        Assert.Single(_f.Projects.Items);
    }

    // ---- read

    [Fact]
    public async Task Projects_are_listed_by_slug_with_paging()
    {
        _f.AddProject("charlie");
        _f.AddProject("alpha");
        _f.AddProject("bravo");

        var result = await _f.ProjectService().GetPagedAsync(1, 2);

        Assert.Equal(["alpha", "bravo"], result.Value.Items.Select(p => p.Slug));
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Equal(2, result.Value.TotalPages);
    }

    [Fact]
    public async Task One_project_can_be_read_by_id_with_both_urls()
    {
        var project = _f.AddProject("mock", frontend: "http://web:8080");

        var result = await _f.ProjectService().GetByIdAsync(project.Id);

        Assert.Equal("http://mock-api:8080/api", result.Value.BackendUrl);
        Assert.Equal("http://web:8080", result.Value.FrontendUrl);
        Assert.Same(ProjectErrors.NotFound, (await _f.ProjectService().GetByIdAsync(Guid.NewGuid())).Error);
    }
}
