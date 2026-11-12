using CoreApi.Enums;
using CoreApi.Models.Requests;
using CoreApi.Services.Projects;
using CoreApi.Services.Users;
using CoreApi.Tests.Support;
using Xunit;

namespace CoreApi.Tests;

public class UserServicesTests
{
    private readonly CoreFixture _f = new();

    private static InternalUserRequest Registered(Guid id, string username = "ada", string email = "ada@example.org", string? first = "Ada", string? last = null)
        => new() { UserId = id, Username = username, Email = email, FirstName = first, LastName = last };

    // ---- profiles

    [Fact]
    public async Task A_profile_is_read_by_id_and_an_unknown_one_is_not_found()
    {
        var profile = _f.AddProfile("ada");

        Assert.Equal("ada", (await _f.UserService().GetByIdAsync(profile.UserId)).Value.Username);
        Assert.Same(UserErrors.NotFound, (await _f.UserService().GetByIdAsync(Guid.NewGuid())).Error);
    }

    [Fact]
    public async Task Updating_the_profile_trims_the_names_and_turns_blanks_into_null()
    {
        var profile = _f.AddProfile("ada");
        _f.Clock.Advance(TimeSpan.FromHours(1));

        var result = await _f.UserService().UpdateProfileAsync(profile.UserId, new UpdateProfileRequest { FirstName = "  Ada ", LastName = "   " });

        Assert.True(result.IsSuccess);
        Assert.Equal(("Ada", null), (profile.FirstName, profile.LastName));
        Assert.Equal(_f.Clock.UtcNow, profile.UpdatedAt);
        Assert.Equal(1, _f.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Updating_an_unknown_profile_is_not_found()
    {
        var result = await _f.UserService().UpdateProfileAsync(Guid.NewGuid(), new UpdateProfileRequest());

        Assert.Same(UserErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Profiles_are_listed_by_username_with_paging()
    {
        _f.AddProfile("charlie");
        _f.AddProfile("alpha");
        _f.AddProfile("bravo");

        var result = await _f.UserService().GetPagedAsync(2, 2);

        Assert.Equal(["charlie"], result.Value.Items.Select(u => u.Username));
        Assert.Equal(3, result.Value.TotalCount);
    }

    // ---- what identity tells core: registered

    [Fact]
    public async Task A_registered_account_gets_a_profile()
    {
        var id = Guid.NewGuid();

        var result = await _f.Provisioning().UpsertAsync(Registered(id, "  ada ", " ada@example.org ", "Ada", " "));

        Assert.True(result.IsSuccess);
        var profile = Assert.Single(_f.Profiles.Items);
        Assert.Equal((id, "ada", "ada@example.org", "Ada", null), (profile.UserId, profile.Username, profile.Email, profile.FirstName, profile.LastName));
        Assert.Equal(_f.Clock.UtcNow, profile.CreatedAt);
    }

    [Fact]
    public async Task A_repeated_delivery_changes_nothing_and_never_overwrites_the_name()
    {
        var id = Guid.NewGuid();
        var service = _f.Provisioning();
        await service.UpsertAsync(Registered(id));
        var profile = _f.Profiles.Items.Single();
        profile.FirstName = "Changed by the user";
        _f.Clock.Advance(TimeSpan.FromMinutes(5));

        var result = await service.UpsertAsync(Registered(id, "ada2", "ada2@example.org", first: "Ada"));

        Assert.True(result.IsSuccess);
        Assert.Single(_f.Profiles.Items);
        Assert.Equal(("ada2", "ada2@example.org", "Changed by the user"), (profile.Username, profile.Email, profile.FirstName));
        Assert.Equal(_f.Clock.UtcNow, profile.UpdatedAt);
    }

    // ---- what identity tells core: confirmed

    private (Entities.Project Project, Guid RoleId) ProjectWithDefaultRole(string slug, bool active = true)
    {
        var project = _f.AddProject(slug, active: active);
        var role = _f.AddRole(project, "User");
        project.DefaultRoleId = role.Id;
        return (project, role.Id);
    }

    [Fact]
    public async Task Confirming_grants_the_default_role_of_every_active_project()
    {
        var user = _f.AddProfile("ada");
        var (core, coreRole) = ProjectWithDefaultRole("core");
        var (mock, mockRole) = ProjectWithDefaultRole("mock");
        ProjectWithDefaultRole("off", active: false);
        _f.AddProject("invite-only");

        var result = await _f.Provisioning().ConfirmedAsync(user.UserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [(core.Id, coreRole), (mock.Id, mockRole)],
            _f.Members.Items.Select(m => (m.ProjectId, m.ProjectRoleId)).OrderBy(x => x.ProjectId == core.Id ? 0 : 1));
        Assert.All(_f.Members.Items, m =>
        {
            Assert.Equal(user.UserId, m.UserId);
            Assert.Null(m.GrantedByUserId);
        });
    }

    [Fact]
    public async Task Confirming_twice_grants_nothing_new_and_keeps_roles_an_admin_changed()
    {
        var user = _f.AddProfile("ada");
        var (project, _) = ProjectWithDefaultRole("mock");
        var service = _f.Provisioning();
        await service.ConfirmedAsync(user.UserId);
        var member = _f.Members.Items.Single();
        var promoted = _f.AddRole(project, "Admin");
        member.ProjectRoleId = promoted.Id;

        await service.ConfirmedAsync(user.UserId);

        Assert.Single(_f.Members.Items);
        Assert.Equal(promoted.Id, member.ProjectRoleId);
    }

    [Fact]
    public async Task Confirming_an_unknown_profile_is_not_found_so_identity_retries()
    {
        var result = await _f.Provisioning().ConfirmedAsync(Guid.NewGuid());

        Assert.Same(UserErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task The_seed_admin_gets_the_admin_role_in_core_and_the_default_role_elsewhere()
    {
        _f.Seed.AdminEmail = "  ADMIN@example.org ";
        var admin = _f.AddProfile("admin", "admin@example.org");
        var (core, defaultRole) = ProjectWithDefaultRole("core");
        var adminRole = _f.AddRole(core, CoreRoles.Admin);
        var (mock, mockDefault) = ProjectWithDefaultRole("mock");

        await _f.Provisioning().ConfirmedAsync(admin.UserId);

        Assert.Equal(adminRole.Id, _f.Members.Items.Single(m => m.ProjectId == core.Id).ProjectRoleId);
        Assert.Equal(mockDefault, _f.Members.Items.Single(m => m.ProjectId == mock.Id).ProjectRoleId);
        Assert.NotEqual(defaultRole, adminRole.Id);
    }

    [Fact]
    public async Task Other_users_never_get_the_admin_role_even_with_a_similar_email()
    {
        _f.Seed.AdminEmail = "admin@example.org";
        var user = _f.AddProfile("mallory", "admin@example.org.evil.com");
        var (core, defaultRole) = ProjectWithDefaultRole("core");
        _f.AddRole(core, CoreRoles.Admin);

        await _f.Provisioning().ConfirmedAsync(user.UserId);

        Assert.Equal(defaultRole, _f.Members.Items.Single().ProjectRoleId);
    }

    [Fact]
    public async Task Without_a_seed_admin_email_nobody_is_promoted()
    {
        _f.Seed.AdminEmail = null;
        var user = _f.AddProfile("ada");
        var (core, defaultRole) = ProjectWithDefaultRole("core");
        _f.AddRole(core, CoreRoles.Admin);

        await _f.Provisioning().ConfirmedAsync(user.UserId);

        Assert.Equal(defaultRole, _f.Members.Items.Single().ProjectRoleId);
    }

    [Fact]
    public async Task The_seed_admin_gets_the_default_role_if_the_core_project_has_no_admin_role_yet()
    {
        _f.Seed.AdminEmail = "admin@example.org";
        var admin = _f.AddProfile("admin", "admin@example.org");
        var (_, defaultRole) = ProjectWithDefaultRole("core");

        await _f.Provisioning().ConfirmedAsync(admin.UserId);

        Assert.Equal(defaultRole, _f.Members.Items.Single().ProjectRoleId);
    }

    [Fact]
    public async Task A_pass_through_project_is_never_a_target_because_it_has_no_default_role()
    {
        var user = _f.AddProfile("ada");
        _f.AddProject("identity", system: true, mode: ProjectTokenMode.PassThrough);

        await _f.Provisioning().ConfirmedAsync(user.UserId);

        Assert.Empty(_f.Members.Items);
    }
}
