using CoreApi.Entities;
using CoreApi.Enums;
using CoreApi.Options;
using CoreApi.Services.ProjectMembers;
using CoreApi.Services.ProjectRoles;
using CoreApi.Services.Projects;
using CoreApi.Services.UserTabs;
using CoreApi.Services.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoreApi.Tests.Support;

/// <summary>Real services on top of in-memory repositories, with a clock the test controls.</summary>
public sealed class CoreFixture
{
    public FakeTimeProvider Clock { get; } = new();
    public FakeUnitOfWork UnitOfWork { get; } = new();
    public TestEnvironment Environment { get; } = new("Production");
    public SeedOptions Seed { get; } = new();

    public FakeProfileRepository Profiles { get; } = new();
    public FakeProjectRepository Projects { get; } = new();
    public FakeMemberRepository Members { get; } = new();
    public FakeRoleRepository Roles { get; }
    public FakeTabRepository Tabs { get; }

    public CoreFixture()
    {
        Roles = new FakeRoleRepository(Members);
        Tabs = new FakeTabRepository(Members);
        Members.Load = member =>
        {
            member.Project = Projects.Items.First(p => p.Id == member.ProjectId);
            member.Role = Roles.Items.First(r => r.Id == member.ProjectRoleId);
            member.User = Profiles.Items.First(u => u.UserId == member.UserId);
            return member;
        };
    }

    public ProjectService ProjectService() => new(Projects, Roles, UnitOfWork, Environment, Clock);

    public ProjectRoleService RoleService() => new(Projects, Roles, UnitOfWork, Clock);

    public ProjectMemberService MemberService() => new(Projects, Roles, Members, Profiles, UnitOfWork, Clock);

    public UserService UserService() => new(Profiles, UnitOfWork, Clock);

    public UserProvisioningService Provisioning() => new(
        Profiles, Projects, Roles, Members, UnitOfWork,
        Microsoft.Extensions.Options.Options.Create(Seed), Clock, NullLogger<UserProvisioningService>.Instance);

    public UserTabService TabService() => new(Tabs, Members, UnitOfWork, Clock);

    public UserProfile AddProfile(string username = "ada", string? email = null, Guid? id = null)
    {
        var profile = new UserProfile
        {
            UserId = id ?? Guid.NewGuid(),
            Username = username,
            Email = email ?? $"{username}@example.org",
            CreatedAt = Clock.UtcNow,
            UpdatedAt = Clock.UtcNow
        };
        Profiles.Items.Add(profile);
        return profile;
    }

    public Project AddProject(
        string slug = "mock",
        string? backend = "http://mock-api:8080/api",
        string? frontend = null,
        bool active = true,
        bool system = false,
        ProjectTokenMode mode = ProjectTokenMode.Exchange,
        string? name = null)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = name ?? slug,
            BackendUrl = backend,
            FrontendUrl = frontend,
            IsActive = active,
            IsSystem = system,
            TokenMode = mode,
            CreatedAt = Clock.UtcNow,
            UpdatedAt = Clock.UtcNow
        };
        Projects.Items.Add(project);
        return project;
    }

    public ProjectRole AddRole(Project project, string name = "Viewer")
    {
        var role = new ProjectRole
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = name,
            CreatedAt = Clock.UtcNow,
            UpdatedAt = Clock.UtcNow
        };
        Roles.Items.Add(role);
        return role;
    }

    public ProjectMember AddMember(UserProfile user, Project project, ProjectRole role)
    {
        var member = new ProjectMember
        {
            UserId = user.UserId,
            ProjectId = project.Id,
            ProjectRoleId = role.Id,
            GrantedAt = Clock.UtcNow,
            UpdatedAt = Clock.UtcNow
        };
        Members.Items.Add(member);
        return member;
    }

    public UserTab AddTab(UserProfile user, Project project, int position, string path = "/")
    {
        var tab = new UserTab
        {
            UserId = user.UserId,
            ProjectId = project.Id,
            Position = position,
            LastPath = path,
            UpdatedAt = Clock.UtcNow
        };
        Tabs.Items.Add(tab);
        return tab;
    }
}
