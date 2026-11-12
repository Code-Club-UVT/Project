using CoreApi.Models.Requests;
using CoreApi.Services.UserTabs;
using CoreApi.Tests.Support;
using Xunit;

namespace CoreApi.Tests;

public class UserTabRulesTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/about")]
    [InlineData("/items/3")]
    [InlineData("/search?q=a b&page=2")]
    [InlineData("/page#section")]
    [InlineData("/%2e%2e/x")]
    public void A_path_that_starts_with_one_slash_is_valid(string path)
    {
        Assert.True(UserTabRules.IsValidPath(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("about")]
    [InlineData("//evil.example.com")]
    [InlineData("/\\evil.example.com")]
    [InlineData("\\evil")]
    [InlineData("/a\\b")]
    [InlineData("http://evil.example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/a\nb")]
    [InlineData("/a\tb")]
    [InlineData("/a\0b")]
    public void Anything_else_is_refused(string? path)
    {
        Assert.False(UserTabRules.IsValidPath(path));
    }
}

public class UserTabServiceTests
{
    private readonly CoreFixture _f = new();

    private static SaveUserTabsRequest Save(params (Guid ProjectId, string Path)[] tabs)
        => new() { Tabs = tabs.Select(t => new UserTabRequest { ProjectId = t.ProjectId, LastPath = t.Path }).ToList() };

    private (Entities.UserProfile User, Entities.Project A, Entities.Project B, Entities.Project C) Setup()
    {
        var user = _f.AddProfile("ada");
        var a = _f.AddProject("alpha", frontend: "http://a-web:8080");
        var b = _f.AddProject("bravo", frontend: "http://b-web:8080");
        var c = _f.AddProject("charlie", frontend: "http://c-web:8080");
        foreach (var project in new[] { a, b, c })
        {
            _f.AddMember(user, project, _f.AddRole(project));
        }

        return (user, a, b, c);
    }

    [Fact]
    public async Task Saving_stores_the_tabs_in_order_and_returns_them()
    {
        var (user, a, b, _) = Setup();

        var result = await _f.TabService().ReplaceAsync(user.UserId, Save((b.Id, "/two"), (a.Id, "/")));

        Assert.True(result.IsSuccess);
        Assert.Equal([(b.Id, "/two"), (a.Id, "/")], result.Value.Select(t => (t.ProjectId, t.LastPath)));
        Assert.Equal([0, 1], _f.Tabs.Items.OrderBy(t => t.Position).Select(t => t.Position));
        Assert.Equal(1, _f.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Saving_again_updates_in_place_adds_new_ones_and_removes_the_rest()
    {
        var (user, a, b, c) = Setup();
        var service = _f.TabService();
        await service.ReplaceAsync(user.UserId, Save((a.Id, "/old"), (b.Id, "/")));
        var kept = _f.Tabs.Items.Single(t => t.ProjectId == a.Id);
        _f.Clock.Advance(TimeSpan.FromMinutes(1));

        var result = await service.ReplaceAsync(user.UserId, Save((c.Id, "/new"), (a.Id, "/changed")));

        Assert.Equal([(c.Id, "/new"), (a.Id, "/changed")], result.Value.Select(t => (t.ProjectId, t.LastPath)));
        Assert.Equal(2, _f.Tabs.Items.Count);
        Assert.Same(kept, _f.Tabs.Items.Single(t => t.ProjectId == a.Id));
        Assert.Equal((1, "/changed", _f.Clock.UtcNow), (kept.Position, kept.LastPath, kept.UpdatedAt));
        Assert.DoesNotContain(_f.Tabs.Items, t => t.ProjectId == b.Id);
    }

    [Fact]
    public async Task An_empty_list_clears_the_tabs()
    {
        var (user, a, _, _) = Setup();
        await _f.TabService().ReplaceAsync(user.UserId, Save((a.Id, "/")));

        var result = await _f.TabService().ReplaceAsync(user.UserId, Save());

        Assert.Empty(result.Value);
        Assert.Empty(_f.Tabs.Items);
    }

    [Fact]
    public async Task Projects_the_user_is_not_a_member_of_are_dropped()
    {
        var (user, a, _, _) = Setup();
        var stranger = _f.AddProject("secret", frontend: "http://s-web:8080");

        var result = await _f.TabService().ReplaceAsync(user.UserId, Save((stranger.Id, "/"), (a.Id, "/"), (Guid.NewGuid(), "/")));

        Assert.Equal([a.Id], result.Value.Select(t => t.ProjectId));
        Assert.Single(_f.Tabs.Items);
    }

    [Fact]
    public async Task A_project_listed_twice_counts_once_and_the_first_position_wins()
    {
        var (user, a, b, _) = Setup();

        var result = await _f.TabService().ReplaceAsync(user.UserId, Save((a.Id, "/first"), (b.Id, "/"), (a.Id, "/second")));

        Assert.Equal([(a.Id, "/first"), (b.Id, "/")], result.Value.Select(t => (t.ProjectId, t.LastPath)));
    }

    [Fact]
    public async Task Twenty_tabs_are_allowed_and_twenty_one_are_not()
    {
        var (user, a, _, _) = Setup();

        var twenty = await _f.TabService().ReplaceAsync(user.UserId, Save(Enumerable.Repeat((a.Id, "/"), 20).ToArray()));
        var tooMany = await _f.TabService().ReplaceAsync(user.UserId, Save(Enumerable.Repeat((a.Id, "/"), 21).ToArray()));

        Assert.True(twenty.IsSuccess);
        Assert.Same(UserTabErrors.TooMany, tooMany.Error);
    }

    [Fact]
    public async Task One_bad_path_refuses_the_whole_list_and_changes_nothing()
    {
        var (user, a, b, _) = Setup();
        await _f.TabService().ReplaceAsync(user.UserId, Save((a.Id, "/keep")));

        var result = await _f.TabService().ReplaceAsync(user.UserId, Save((b.Id, "/fine"), (a.Id, "//evil.example.com")));

        Assert.Same(UserTabErrors.InvalidPath, result.Error);
        Assert.Equal([(a.Id, "/keep")], _f.Tabs.Items.Select(t => (t.ProjectId, t.LastPath)));
    }

    [Fact]
    public async Task Tabs_of_other_users_are_never_touched()
    {
        var (user, a, _, _) = Setup();
        var other = _f.AddProfile("grace");
        _f.AddMember(other, a, _f.AddRole(a, "Other"));
        _f.AddTab(other, a, 0, "/graces");

        await _f.TabService().ReplaceAsync(user.UserId, Save());

        Assert.Equal("/graces", Assert.Single(_f.Tabs.Items).LastPath);
    }

    [Fact]
    public async Task Reading_returns_the_saved_tabs_in_order_without_inactive_projects_or_projects_without_a_frontend()
    {
        var (user, a, b, c) = Setup();
        var inactive = _f.AddProject("off", frontend: "http://off-web:8080", active: false);
        var noFrontend = _f.AddProject("api-only", frontend: null);
        _f.AddMember(user, inactive, _f.AddRole(inactive));
        _f.AddMember(user, noFrontend, _f.AddRole(noFrontend));
        _f.AddTab(user, c, 3, "/c");
        _f.AddTab(user, inactive, 0, "/off");
        _f.AddTab(user, a, 1, "/a");
        _f.AddTab(user, noFrontend, 2, "/api");

        var result = await _f.TabService().GetAsync(user.UserId);

        Assert.Equal([(a.Id, "/a"), (c.Id, "/c")], result.Value.Select(t => (t.ProjectId, t.LastPath)));
        Assert.DoesNotContain(b.Id, result.Value.Select(t => t.ProjectId));
    }

    [Fact]
    public async Task A_user_with_no_saved_tabs_gets_an_empty_list()
    {
        var (user, _, _, _) = Setup();

        var result = await _f.TabService().GetAsync(user.UserId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
