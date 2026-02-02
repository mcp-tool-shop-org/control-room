using ControlRoom.Application.Services;
using ControlRoom.Domain.Model;

namespace ControlRoom.Tests.Unit.Application;

/// <summary>
/// Tests for MarketplaceCatalog service.
/// </summary>
public sealed class MarketplaceCatalogTests
{
    private readonly MarketplaceCatalog _catalog = new();

    // ========================================================================
    // GetAll Tests
    // ========================================================================

    [Fact]
    public void GetAll_ReturnsAllIntegrations()
    {
        var all = _catalog.GetAll();

        Assert.NotEmpty(all);
        Assert.True(all.Count >= 10); // We defined at least 10 integrations
    }

    [Fact]
    public void GetAll_OrdersByCategoryThenName()
    {
        var all = _catalog.GetAll();

        for (var i = 1; i < all.Count; i++)
        {
            var prev = all[i - 1];
            var curr = all[i];

            if (prev.Category == curr.Category)
            {
                Assert.True(string.Compare(prev.Name, curr.Name, StringComparison.Ordinal) <= 0);
            }
        }
    }

    // ========================================================================
    // Get Tests
    // ========================================================================

    [Theory]
    [InlineData("aws")]
    [InlineData("azure")]
    [InlineData("gcp")]
    [InlineData("github")]
    [InlineData("jira")]
    [InlineData("pagerduty")]
    [InlineData("slack")]
    public void Get_ReturnsIntegrationById(string id)
    {
        var integration = _catalog.Get(id);

        Assert.NotNull(integration);
        Assert.Equal(id, integration.Id);
    }

    [Fact]
    public void Get_ReturnsNullForUnknownId()
    {
        var integration = _catalog.Get("unknown-integration");

        Assert.Null(integration);
    }

    // ========================================================================
    // GetByCategory Tests
    // ========================================================================

    [Fact]
    public void GetByCategory_CloudProvider_ReturnsCloudProviders()
    {
        var cloudProviders = _catalog.GetByCategory(IntegrationCategory.CloudProvider);

        Assert.NotEmpty(cloudProviders);
        Assert.All(cloudProviders, i => Assert.Equal(IntegrationCategory.CloudProvider, i.Category));
        Assert.Contains(cloudProviders, i => i.Id == "aws");
        Assert.Contains(cloudProviders, i => i.Id == "azure");
        Assert.Contains(cloudProviders, i => i.Id == "gcp");
    }

    [Fact]
    public void GetByCategory_SourceControl_ReturnsSourceControlProviders()
    {
        var sourceControl = _catalog.GetByCategory(IntegrationCategory.SourceControl);

        Assert.NotEmpty(sourceControl);
        Assert.All(sourceControl, i => Assert.Equal(IntegrationCategory.SourceControl, i.Category));
        Assert.Contains(sourceControl, i => i.Id == "github");
    }

    [Fact]
    public void GetByCategory_IssueTracker_ReturnsIssueTrackers()
    {
        var issueTrackers = _catalog.GetByCategory(IntegrationCategory.IssueTracking);

        Assert.NotEmpty(issueTrackers);
        Assert.All(issueTrackers, i => Assert.Equal(IntegrationCategory.IssueTracking, i.Category));
        Assert.Contains(issueTrackers, i => i.Id == "jira");
    }

    [Fact]
    public void GetByCategory_IncidentManagement_ReturnsIncidentManagementProviders()
    {
        var incidentManagement = _catalog.GetByCategory(IntegrationCategory.Alerting);

        Assert.NotEmpty(incidentManagement);
        Assert.All(incidentManagement, i => Assert.Equal(IntegrationCategory.Alerting, i.Category));
        Assert.Contains(incidentManagement, i => i.Id == "pagerduty");
    }

    [Fact]
    public void GetByCategory_Communication_ReturnsCommunicationProviders()
    {
        var communication = _catalog.GetByCategory(IntegrationCategory.Communication);

        Assert.NotEmpty(communication);
        Assert.All(communication, i => Assert.Equal(IntegrationCategory.Communication, i.Category));
        Assert.Contains(communication, i => i.Id == "slack");
    }

    [Fact]
    public void GetByCategory_Monitoring_ReturnsMonitoringProviders()
    {
        var monitoring = _catalog.GetByCategory(IntegrationCategory.Monitoring);

        Assert.NotEmpty(monitoring);
        Assert.All(monitoring, i => Assert.Equal(IntegrationCategory.Monitoring, i.Category));
        Assert.Contains(monitoring, i => i.Id == "datadog");
    }

    // ========================================================================
    // Search Tests
    // ========================================================================

    [Fact]
    public void Search_EmptyQuery_ReturnsAll()
    {
        var results = _catalog.Search("");
        var all = _catalog.GetAll();

        Assert.Equal(all.Count, results.Count);
    }

    [Fact]
    public void Search_ByName_ReturnsMatches()
    {
        var results = _catalog.Search("GitHub");

        Assert.Single(results);
        Assert.Equal("github", results[0].Id);
    }

    [Fact]
    public void Search_ByDescription_ReturnsMatches()
    {
        var results = _catalog.Search("incident management");

        Assert.NotEmpty(results);
        Assert.Contains(results, i => i.Id == "pagerduty");
    }

    [Fact]
    public void Search_ByTag_ReturnsMatches()
    {
        var results = _catalog.Search("observability");

        Assert.NotEmpty(results);
        Assert.Contains(results, i => i.Id == "datadog");
    }

    [Fact]
    public void Search_CaseInsensitive()
    {
        var lowerResults = _catalog.Search("aws");
        var upperResults = _catalog.Search("AWS");
        var mixedResults = _catalog.Search("AwS");

        Assert.Equal(lowerResults.Count, upperResults.Count);
        Assert.Equal(lowerResults.Count, mixedResults.Count);
    }

    [Fact]
    public void Search_PartialMatch_ReturnsResults()
    {
        var results = _catalog.Search("git");

        Assert.True(results.Count >= 2); // github and gitlab
    }

    [Fact]
    public void Search_NoMatches_ReturnsEmpty()
    {
        var results = _catalog.Search("xyznonexistent");

        Assert.Empty(results);
    }

    // ========================================================================
    // GetFeatured Tests
    // ========================================================================

    [Fact]
    public void GetFeatured_ReturnsOnlyFeaturedIntegrations()
    {
        var featured = _catalog.GetFeatured();

        Assert.NotEmpty(featured);
        Assert.All(featured, i => Assert.True(i.IsFeatured));
    }

    [Fact]
    public void GetFeatured_IncludesExpectedIntegrations()
    {
        var featured = _catalog.GetFeatured();

        Assert.Contains(featured, i => i.Id == "aws");
        Assert.Contains(featured, i => i.Id == "github");
        Assert.Contains(featured, i => i.Id == "slack");
    }

    // ========================================================================
    // GetPopular Tests
    // ========================================================================

    [Fact]
    public void GetPopular_ReturnsRequestedCount()
    {
        var popular5 = _catalog.GetPopular(5);
        var popular10 = _catalog.GetPopular(10);

        Assert.Equal(5, popular5.Count);
        Assert.True(popular10.Count <= 10);
    }

    [Fact]
    public void GetPopular_OrderedByPopularityDescending()
    {
        var popular = _catalog.GetPopular(10);

        for (var i = 1; i < popular.Count; i++)
        {
            Assert.True(popular[i - 1].PopularityScore >= popular[i].PopularityScore);
        }
    }

    [Fact]
    public void GetPopular_HighestScoresFirst()
    {
        var popular = _catalog.GetPopular(3);

        // GitHub should be near the top (score 98)
        Assert.Contains(popular, i => i.Id == "github");
    }

    // ========================================================================
    // GetRecentlyAdded Tests
    // ========================================================================

    [Fact]
    public void GetRecentlyAdded_ReturnsRequestedCount()
    {
        var recent = _catalog.GetRecentlyAdded(5);

        Assert.True(recent.Count <= 5);
    }

    [Fact]
    public void GetRecentlyAdded_OrderedByAddedAtDescending()
    {
        var recent = _catalog.GetRecentlyAdded(10);

        for (var i = 1; i < recent.Count; i++)
        {
            Assert.True(recent[i - 1].AddedAt >= recent[i].AddedAt);
        }
    }

    // ========================================================================
    // GetCategoryStats Tests
    // ========================================================================

    [Fact]
    public void GetCategoryStats_ReturnsAllCategories()
    {
        var stats = _catalog.GetCategoryStats();

        Assert.NotEmpty(stats);
        Assert.Contains(stats, s => s.Category == IntegrationCategory.CloudProvider);
        Assert.Contains(stats, s => s.Category == IntegrationCategory.SourceControl);
        Assert.Contains(stats, s => s.Category == IntegrationCategory.IssueTracking);
    }

    [Fact]
    public void GetCategoryStats_CountsAreCorrect()
    {
        var stats = _catalog.GetCategoryStats();
        var cloudStats = stats.First(s => s.Category == IntegrationCategory.CloudProvider);

        var cloudProviders = _catalog.GetByCategory(IntegrationCategory.CloudProvider);

        Assert.Equal(cloudProviders.Count, cloudStats.TotalCount);
    }

    [Fact]
    public void GetCategoryStats_FeaturedCountsAreCorrect()
    {
        var stats = _catalog.GetCategoryStats();
        var cloudStats = stats.First(s => s.Category == IntegrationCategory.CloudProvider);

        var featuredCloudProviders = _catalog.GetByCategory(IntegrationCategory.CloudProvider)
            .Count(i => i.IsFeatured);

        Assert.Equal(featuredCloudProviders, cloudStats.FeaturedCount);
    }

    // ========================================================================
    // MarketplaceIntegration Tests
    // ========================================================================

    [Fact]
    public void MarketplaceIntegration_HasAllRequiredProperties()
    {
        var aws = _catalog.Get("aws");

        Assert.NotNull(aws);
        Assert.Equal("aws", aws.Id);
        Assert.Equal("Amazon Web Services", aws.Name);
        Assert.NotEmpty(aws.Description);
        Assert.Equal(IntegrationCategory.CloudProvider, aws.Category);
        Assert.Equal(AuthMethod.ApiKey, aws.AuthMethod);
        Assert.NotEmpty(aws.Icon);
        Assert.NotNull(aws.WebsiteUrl);
        Assert.NotNull(aws.DocumentationUrl);
        Assert.NotEmpty(aws.Tags);
        Assert.NotEmpty(aws.Features);
        Assert.NotEmpty(aws.SetupSteps);
    }

    [Fact]
    public void MarketplaceIntegration_ToDomainModel_CreatesValidModel()
    {
        var marketplace = _catalog.Get("github");
        Assert.NotNull(marketplace);

        var domain = marketplace.ToDomainModel();

        Assert.Equal("github", domain.Name);
        Assert.Equal("GitHub", domain.DisplayName);
        Assert.Equal(marketplace.Description, domain.Description);
        Assert.Equal(marketplace.Category, domain.Category);
        Assert.Equal(marketplace.AuthMethod, domain.AuthMethod);
    }

    // ========================================================================
    // Integration Content Tests
    // ========================================================================

    [Theory]
    [InlineData("aws", IntegrationCategory.CloudProvider)]
    [InlineData("azure", IntegrationCategory.CloudProvider)]
    [InlineData("gcp", IntegrationCategory.CloudProvider)]
    [InlineData("github", IntegrationCategory.SourceControl)]
    [InlineData("gitlab", IntegrationCategory.SourceControl)]
    [InlineData("jira", IntegrationCategory.IssueTracking)]
    [InlineData("linear", IntegrationCategory.IssueTracking)]
    [InlineData("pagerduty", IntegrationCategory.Alerting)]
    [InlineData("opsgenie", IntegrationCategory.Alerting)]
    [InlineData("slack", IntegrationCategory.Communication)]
    [InlineData("teams", IntegrationCategory.Communication)]
    [InlineData("datadog", IntegrationCategory.Monitoring)]
    [InlineData("prometheus", IntegrationCategory.Monitoring)]
    public void Integration_HasCorrectCategory(string id, IntegrationCategory expectedCategory)
    {
        var integration = _catalog.Get(id);

        Assert.NotNull(integration);
        Assert.Equal(expectedCategory, integration.Category);
    }

    [Theory]
    [InlineData("aws", AuthMethod.ApiKey)]
    [InlineData("azure", AuthMethod.OAuth2)]
    [InlineData("gcp", AuthMethod.GCP_ServiceAccount)]
    [InlineData("github", AuthMethod.OAuth2)]
    [InlineData("jira", AuthMethod.ApiKey)]
    [InlineData("pagerduty", AuthMethod.ApiKey)]
    [InlineData("slack", AuthMethod.OAuth2)]
    public void Integration_HasCorrectAuthMethod(string id, AuthMethod expectedAuthMethod)
    {
        var integration = _catalog.Get(id);

        Assert.NotNull(integration);
        Assert.Equal(expectedAuthMethod, integration.AuthMethod);
    }

    [Theory]
    [InlineData("aws")]
    [InlineData("azure")]
    [InlineData("gcp")]
    [InlineData("github")]
    [InlineData("jira")]
    [InlineData("pagerduty")]
    [InlineData("slack")]
    [InlineData("datadog")]
    public void FeaturedIntegrations_AreFeatured(string id)
    {
        var integration = _catalog.Get(id);

        Assert.NotNull(integration);
        Assert.True(integration.IsFeatured);
    }

    [Theory]
    [InlineData("aws", "ec2")]
    [InlineData("aws", "s3")]
    [InlineData("github", "pull requests")]
    [InlineData("jira", "agile")]
    [InlineData("slack", "chatops")]
    public void Integration_HasExpectedTags(string id, string expectedTag)
    {
        var integration = _catalog.Get(id);

        Assert.NotNull(integration);
        Assert.Contains(integration.Tags, t => t.Contains(expectedTag, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AllIntegrations_HaveNonEmptyFeatures()
    {
        var all = _catalog.GetAll();

        Assert.All(all, i => Assert.NotEmpty(i.Features));
    }

    [Fact]
    public void AllIntegrations_HaveNonEmptySetupSteps()
    {
        var all = _catalog.GetAll();

        Assert.All(all, i => Assert.NotEmpty(i.SetupSteps));
    }

    // ========================================================================
    // CategoryStats Record Tests
    // ========================================================================

    [Fact]
    public void CategoryStats_HasCorrectProperties()
    {
        var stats = new CategoryStats(IntegrationCategory.CloudProvider, 5, 3);

        Assert.Equal(IntegrationCategory.CloudProvider, stats.Category);
        Assert.Equal(5, stats.TotalCount);
        Assert.Equal(3, stats.FeaturedCount);
    }
}
