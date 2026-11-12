using CoreApi.Enums;
using CoreApi.Models.Requests;
using CoreApi.Services.ProjectRoles;
using CoreApi.Services.Projects;
using CoreApi.Tests.Support;
using Xunit;

namespace CoreApi.Tests;

public class ProjectRoleServiceTests
{
    private readonly CoreFixture _f = new();

    private static ProjectRoleRequest Request(string name = "Editor", string? description = null) => new() { Name = name, Description = description };

    [Fact]
    public async Task A_role_is_created_with_a_trimmed_name_and_a_blank_description_as_null()
    {
        var project = _f.AddProject("mock");

        var result = await _f.RoleService().CreateAsync(project.Id, Request("  Editor ", "  "));

        Assert.True(result.IsSuccess);
        var role = Assert.Single(_f.Roles.Items);
        Assert.Equal((project.Id, "Editor", null), (role.ProjectId, role.Name, role.Description));
        Assert.Equal(result.Value.Id, role.Id);
    }

    [Fact]
    public async Task Role_names_are_unique_per_project_without_regard_to_case()
    {
        var mock = _f.AddProject("mock");
        var other = _f.AddProject("other");
        _f.AddRole(mock, "Editor");
        var service = _f.RoleService();

        Assert.Same(ProjectRoleErrors.NameTaken, (await service.CreateAsync(mock.Id, Request("editor"))).Error);
        Assert.True((await service.CreateAsync(other.Id, Request("Editor"))).IsSuccess);
    }

    [Fact]
    public async Task Roles_need_an_existing_project_that_takes_project_tokens()
    {
        var identity = _f.AddProject("identity", system: true, mode: ProjectTokenMode.PassThrough);

        Assert.Same(ProjectErrors.NotFound, (await _f.RoleService().CreateAsync(Guid.NewGuid(), Request())).Error);
        Assert.Same(ProjectErrors.NoMemberships, (await _f.RoleService().CreateAsync(identity.Id, Request())).Error);
        Assert.Same(ProjectErrors.NotFound, (await _f.RoleService().GetPagedAsync(Guid.NewGuid(), 1, 10)).Error);
        Assert.Same(ProjectErrors.NoMemberships, (await _f.RoleService().GetPagedAsync(identity.Id, 1, 10)).Error);
    }

    [Fact]
    public async Task Roles_are_listed_per_project_by_name()
    {
        var project = _f.AddProject("mock");
        _f.AddRole(project, "Viewer");
        _f.AddRole(project, "Admin");
        _f.AddRole(_f.AddProject("other"), "Elsewhere");

        var result = await _f.RoleService().GetPagedAsync(project.Id, 1, 10);

        Assert.Equal(["Admin", "Viewer"], result.Value.Items.Select(r => r.Name));
    }

    [Fact]
    public async Task A_role_can_be_renamed_and_keep_its_own_name()
    {
        var project = _f.AddProject("mock");
        var role = _f.AddRole(project, "Viewer");
        _f.Clock.Advance(TimeSpan.FromHours(1));
        var service = _f.RoleService();

        Assert.True((await service.UpdateAsync(project.Id, role.Id, Request("Viewer", "Read only"))).IsSuccess);
        Assert.Equal("Read only", role.Description);

        Assert.True((await service.UpdateAsync(project.Id, role.Id, Request("Reader"))).IsSuccess);
        Assert.Equal("Reader", role.Name);
        Assert.Equal(_f.Clock.UtcNow, role.UpdatedAt);
    }

    [Fact]
    public async Task A_role_cannot_take_the_name_of_another_role_of_the_project()
    {
        var project = _f.AddProject("mock");
        _f.AddRole(project, "Editor");
        var viewer = _f.AddRole(project, "Viewer");

        var result = await _f.RoleService().UpdateAsync(project.Id, viewer.Id, Request("EDITOR"));

        Assert.Same(ProjectRoleErrors.NameTaken, result.Error);
    }

    [Fact]
    public async Task A_role_of_another_project_is_not_found_through_this_one()
    {
        var mock = _f.AddProject("mock");
        var other = _f.AddProject("other");
        var role = _f.AddRole(other);

        Assert.Same(ProjectRoleErrors.NotFound, (await _f.RoleService().UpdateAsync(mock.Id, role.Id, Request())).Error);
        Assert.Same(ProjectRoleErrors.NotFound, (await _f.RoleService().DeleteAsync(mock.Id, role.Id)).Error);
        Assert.Same(ProjectRoleErrors.NotFound, (await _f.RoleService().DeleteAsync(mock.Id, Guid.NewGuid())).Error);
    }

    [Fact]
    public async Task The_admin_role_of_the_core_project_cannot_be_renamed_or_deleted()
    {
        var core = _f.AddProject("core", system: true);
        var admin = _f.AddRole(core, "Admin");

        Assert.Same(ProjectRoleErrors.SystemRole, (await _f.RoleService().UpdateAsync(core.Id, admin.Id, Request("Boss"))).Error);
        Assert.Same(ProjectRoleErrors.SystemRole, (await _f.RoleService().DeleteAsync(core.Id, admin.Id)).Error);
        Assert.Contains(admin, _f.Roles.Items);
    }

    [Fact]
    public async Task An_admin_role_in_any_other_project_is_an_ordinary_role()
    {
        var project = _f.AddProject("mock");
        var admin = _f.AddRole(project, "Admin");

        Assert.True((await _f.RoleService().UpdateAsync(project.Id, admin.Id, Request("Boss"))).IsSuccess);
        Assert.True((await _f.RoleService().DeleteAsync(project.Id, admin.Id)).IsSuccess);
    }

    [Fact]
    public async Task Other_core_roles_can_still_be_changed()
    {
        var core = _f.AddProject("core", system: true);
        var user = _f.AddRole(core, "User");

        Assert.True((await _f.RoleService().UpdateAsync(core.Id, user.Id, Request("Member"))).IsSuccess);
    }

    [Fact]
    public async Task A_role_in_use_cannot_be_deleted_and_an_unused_one_can()
    {
        var project = _f.AddProject("mock");
        var used = _f.AddRole(project, "Used");
        var spare = _f.AddRole(project, "Spare");
        _f.AddMember(_f.AddProfile("ada"), project, used);

        Assert.Same(ProjectRoleErrors.InUse, (await _f.RoleService().DeleteAsync(project.Id, used.Id)).Error);
        Assert.True((await _f.RoleService().DeleteAsync(project.Id, spare.Id)).IsSuccess);
        Assert.Equal([used], _f.Roles.Items);
    }
}
