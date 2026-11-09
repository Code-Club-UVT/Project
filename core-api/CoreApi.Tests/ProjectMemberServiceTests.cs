using System.Text.Json;
using CoreApi.Enums;
using CoreApi.Models.Requests;
using CoreApi.Services.ProjectMembers;
using CoreApi.Services.ProjectRoles;
using CoreApi.Services.Projects;
using CoreApi.Services.Users;
using CoreApi.Tests.Support;
using Xunit;

namespace CoreApi.Tests;

public class ProjectMemberServiceTests
{
    private readonly CoreFixture _f = new();

    // ---- grant

    [Fact]
    public async Task Granting_access_creates_one_membership_with_who_granted_it()
    {
        var project = _f.AddProject("mock");
        var role = _f.AddRole(project, "Editor");
        var user = _f.AddProfile("ada");
        var admin = _f.AddProfile("admin");

        var result = await _f.MemberService().GrantAsync(project.Id, admin.UserId, new GrantProjectAccessRequest { UserId = user.UserId, RoleId = role.Id });

        Assert.True(result.IsSuccess);
        var member = Assert.Single(_f.Members.Items);
        Assert.Equal((user.UserId, project.Id, role.Id, admin.UserId), (member.UserId, member.ProjectId, member.ProjectRoleId, member.GrantedByUserId));
        Assert.Equal(_f.Clock.UtcNow, member.GrantedAt);
        Assert.Equal(("ada", "Editor"), (result.Value.Username, result.Value.RoleName));
    }

    [Fact]
    public async Task Granting_needs_a_known_project_user_and_a_role_of_that_project()
    {
        var project = _f.AddProject("mock");
        var other = _f.AddProject("other");
        var role = _f.AddRole(project);
        var foreignRole = _f.AddRole(other);
        var user = _f.AddProfile("ada");
        var service = _f.MemberService();

        Assert.Same(ProjectErrors.NotFound, (await service.GrantAsync(Guid.NewGuid(), user.UserId, new() { UserId = user.UserId, RoleId = role.Id })).Error);
        Assert.Same(UserErrors.NotFound, (await service.GrantAsync(project.Id, user.UserId, new() { UserId = Guid.NewGuid(), RoleId = role.Id })).Error);
        Assert.Same(ProjectRoleErrors.NotFound, (await service.GrantAsync(project.Id, user.UserId, new() { UserId = user.UserId, RoleId = foreignRole.Id })).Error);
        Assert.Same(ProjectRoleErrors.NotFound, (await service.GrantAsync(project.Id, user.UserId, new() { UserId = user.UserId, RoleId = Guid.NewGuid() })).Error);
        Assert.Empty(_f.Members.Items);
    }

    [Fact]
    public async Task A_user_can_have_only_one_role_in_a_project()
    {
        var project = _f.AddProject("mock");
        var role = _f.AddRole(project);
        var user = _f.AddProfile("ada");
        _f.AddMember(user, project, role);

        var result = await _f.MemberService().GrantAsync(project.Id, user.UserId, new() { UserId = user.UserId, RoleId = role.Id });

        Assert.Same(ProjectMemberErrors.AlreadyMember, result.Error);
        Assert.Single(_f.Members.Items);
    }

    [Fact]
    public async Task A_pass_through_project_has_no_members()
    {
        var identity = _f.AddProject("identity", system: true, mode: ProjectTokenMode.PassThrough);
        var user = _f.AddProfile("ada");
        var service = _f.MemberService();

        Assert.Same(ProjectErrors.NoMemberships, (await service.GrantAsync(identity.Id, user.UserId, new() { UserId = user.UserId, RoleId = Guid.NewGuid() })).Error);
        Assert.Same(ProjectErrors.NoMemberships, (await service.GetPagedAsync(identity.Id, 1, 10)).Error);
    }

    // ---- change role and revoke

    [Fact]
    public async Task A_role_can_be_changed_to_another_role_of_the_same_project()
    {
        var project = _f.AddProject("mock");
        var viewer = _f.AddRole(project, "Viewer");
        var editor = _f.AddRole(project, "Editor");
        var user = _f.AddProfile("ada");
        var member = _f.AddMember(user, project, viewer);
        _f.Clock.Advance(TimeSpan.FromHours(1));

        var result = await _f.MemberService().ChangeRoleAsync(project.Id, user.UserId, new ChangeProjectRoleRequest { RoleId = editor.Id });

        Assert.True(result.IsSuccess);
        Assert.Equal(editor.Id, member.ProjectRoleId);
        Assert.Equal(_f.Clock.UtcNow, member.UpdatedAt);
        Assert.Equal("Editor", result.Value.RoleName);
    }

    [Fact]
    public async Task Changing_a_role_needs_a_member_and_a_role_of_the_project()
    {
        var project = _f.AddProject("mock");
        var other = _f.AddProject("other");
        var viewer = _f.AddRole(project, "Viewer");
        var foreign = _f.AddRole(other);
        var user = _f.AddProfile("ada");
        _f.AddMember(user, project, viewer);
        var stranger = _f.AddProfile("grace");
        var service = _f.MemberService();

        Assert.Same(ProjectMemberErrors.NotFound, (await service.ChangeRoleAsync(project.Id, stranger.UserId, new() { RoleId = viewer.Id })).Error);
        Assert.Same(ProjectRoleErrors.NotFound, (await service.ChangeRoleAsync(project.Id, user.UserId, new() { RoleId = foreign.Id })).Error);
    }

    [Fact]
    public async Task Revoking_removes_the_membership_and_a_second_revoke_is_not_found()
    {
        var project = _f.AddProject("mock");
        var user = _f.AddProfile("ada");
        _f.AddMember(user, project, _f.AddRole(project));
        var service = _f.MemberService();

        Assert.True((await service.RevokeAsync(project.Id, user.UserId)).IsSuccess);
        Assert.Empty(_f.Members.Items);
        Assert.Same(ProjectMemberErrors.NotFound, (await service.RevokeAsync(project.Id, user.UserId)).Error);
    }

    // ---- lists

    [Fact]
    public async Task Members_are_listed_by_username_with_their_role()
    {
        var project = _f.AddProject("mock");
        var role = _f.AddRole(project, "Viewer");
        _f.AddMember(_f.AddProfile("zed"), project, role);
        _f.AddMember(_f.AddProfile("ada"), project, role);
        _f.AddMember(_f.AddProfile("elsewhere"), _f.AddProject("other"), _f.AddRole(_f.Projects.Items.Last()));

        var result = await _f.MemberService().GetPagedAsync(project.Id, 1, 10);

        Assert.Equal(["ada", "zed"], result.Value.Items.Select(m => m.Username));
        Assert.Equal(2, result.Value.TotalCount);
    }

    [Fact]
    public async Task My_projects_lists_only_active_projects_of_the_user_by_name_with_their_role_and_flags()
    {
        var user = _f.AddProfile("ada");
        var someoneElse = _f.AddProfile("grace");
        var zeta = _f.AddProject("zeta", name: "Zeta", frontend: "http://zeta-web:8080", backend: null);
        var alpha = _f.AddProject("alpha", name: "Alpha", backend: "http://alpha:8080/api");
        var off = _f.AddProject("off", name: "Off", active: false);
        var both = _f.AddProject("both", name: "Both", frontend: "http://b:80");
        var theirs = _f.AddProject("theirs", name: "Theirs");
        foreach (var project in new[] { zeta, alpha, off, both })
        {
            _f.AddMember(user, project, _f.AddRole(project, project.Slug + "-role"));
        }

        _f.AddMember(someoneElse, theirs, _f.AddRole(theirs));

        var result = await _f.MemberService().GetMyProjectsAsync(user.UserId);

        Assert.Equal(["Alpha", "Both", "Zeta"], result.Value.Select(p => p.Name));
        var zetaItem = result.Value.Single(p => p.Slug == "zeta");
        Assert.Equal(("zeta-role", true, false), (zetaItem.RoleName, zetaItem.HasFrontend, zetaItem.HasBackend));
        var alphaItem = result.Value.Single(p => p.Slug == "alpha");
        Assert.Equal((false, true), (alphaItem.HasFrontend, alphaItem.HasBackend));
        var bothItem = result.Value.Single(p => p.Slug == "both");
        Assert.Equal((true, true), (bothItem.HasFrontend, bothItem.HasBackend));
    }

    [Fact]
    public async Task My_projects_never_reveal_the_internal_urls()
    {
        var user = _f.AddProfile("ada");
        var project = _f.AddProject("mock", backend: "http://secret-backend:8080/api", frontend: "http://secret-frontend:8080");
        _f.AddMember(user, project, _f.AddRole(project));

        var result = await _f.MemberService().GetMyProjectsAsync(user.UserId);

        var json = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("secret-backend", json);
        Assert.DoesNotContain("secret-frontend", json);
        Assert.DoesNotContain("Url", json);
    }

    [Fact]
    public async Task A_user_with_no_projects_gets_an_empty_list()
    {
        var user = _f.AddProfile("ada");

        var result = await _f.MemberService().GetMyProjectsAsync(user.UserId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
