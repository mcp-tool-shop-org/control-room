using ControlRoom.Domain.Model;
using IntegrationModel = ControlRoom.Domain.Model.Integration;

namespace ControlRoom.Application.Services;

/// <summary>
/// Catalog of available integrations in the marketplace.
/// Provides discovery, search, and metadata for all supported integrations.
/// </summary>
public sealed class MarketplaceCatalog
{
    private readonly Dictionary<string, MarketplaceIntegration> _integrations = new();
    private readonly Dictionary<IntegrationCategory, List<string>> _byCategory = new();

    public MarketplaceCatalog()
    {
        InitializeCatalog();
    }

    /// <summary>
    /// Gets all available integrations.
    /// </summary>
    public IReadOnlyList<MarketplaceIntegration> GetAll()
    {
        return _integrations.Values.OrderBy(i => i.Category).ThenBy(i => i.Name).ToList();
    }

    /// <summary>
    /// Gets an integration by ID.
    /// </summary>
    public MarketplaceIntegration? Get(string integrationId)
    {
        return _integrations.GetValueOrDefault(integrationId);
    }

    /// <summary>
    /// Gets integrations by category.
    /// </summary>
    public IReadOnlyList<MarketplaceIntegration> GetByCategory(IntegrationCategory category)
    {
        if (!_byCategory.TryGetValue(category, out var ids))
            return [];

        return ids.Select(id => _integrations[id]).ToList();
    }

    /// <summary>
    /// Searches integrations by query.
    /// </summary>
    public IReadOnlyList<MarketplaceIntegration> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAll();

        var lowerQuery = query.ToLowerInvariant();

        return _integrations.Values
            .Where(i =>
                i.Name.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                i.Tags.Any(t => t.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(i => i.Name)
            .ToList();
    }

    /// <summary>
    /// Gets featured integrations.
    /// </summary>
    public IReadOnlyList<MarketplaceIntegration> GetFeatured()
    {
        return _integrations.Values
            .Where(i => i.IsFeatured)
            .OrderBy(i => i.Name)
            .ToList();
    }

    /// <summary>
    /// Gets popular integrations.
    /// </summary>
    public IReadOnlyList<MarketplaceIntegration> GetPopular(int count = 10)
    {
        return _integrations.Values
            .OrderByDescending(i => i.PopularityScore)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets recently added integrations.
    /// </summary>
    public IReadOnlyList<MarketplaceIntegration> GetRecentlyAdded(int count = 5)
    {
        return _integrations.Values
            .OrderByDescending(i => i.AddedAt)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets category statistics.
    /// </summary>
    public IReadOnlyList<CategoryStats> GetCategoryStats()
    {
        return _byCategory
            .Select(kvp => new CategoryStats(
                kvp.Key,
                kvp.Value.Count,
                kvp.Value.Select(id => _integrations[id]).Count(i => i.IsFeatured)))
            .OrderBy(s => s.Category)
            .ToList();
    }

    // ========================================================================
    // Catalog Initialization
    // ========================================================================

    private void InitializeCatalog()
    {
        // Cloud Providers
        AddIntegration(new MarketplaceIntegration
        {
            Id = "aws",
            Name = "Amazon Web Services",
            Description = "Connect to AWS for EC2, S3, RDS, CloudWatch, and Cost Explorer",
            Category = IntegrationCategory.CloudProvider,
            AuthMethod = AuthMethod.ApiKey,
            Icon = "\uE753",
            LogoUrl = "https://aws.amazon.com/favicon.ico",
            WebsiteUrl = "https://aws.amazon.com",
            DocumentationUrl = "https://docs.aws.amazon.com",
            IsFeatured = true,
            PopularityScore = 95,
            Tags = ["cloud", "aws", "ec2", "s3", "lambda", "infrastructure"],
            Features = ["EC2 Instance Management", "S3 Bucket Operations", "CloudWatch Metrics", "Cost Explorer", "Security Groups", "VPC Management"],
            RequiredScopes = ["ec2:*", "s3:*", "cloudwatch:*", "ce:*"],
            SetupSteps = [
                "Create an IAM user or role with appropriate permissions",
                "Generate an access key and secret key",
                "Configure the integration with your credentials"
            ],
            AddedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });

        AddIntegration(new MarketplaceIntegration
        {
            Id = "azure",
            Name = "Microsoft Azure",
            Description = "Connect to Azure for VMs, Storage, SQL Database, and Monitor",
            Category = IntegrationCategory.CloudProvider,
            AuthMethod = AuthMethod.OAuth2,
            Icon = "\uE753",
            LogoUrl = "https://azure.microsoft.com/favicon.ico",
            WebsiteUrl = "https://azure.microsoft.com",
            DocumentationUrl = "https://docs.microsoft.com/azure",
            IsFeatured = true,
            PopularityScore = 90,
            Tags = ["cloud", "azure", "microsoft", "vms", "infrastructure"],
            Features = ["Virtual Machine Management", "Storage Accounts", "SQL Database", "Azure Monitor", "Cost Management", "Network Security Groups"],
            RequiredScopes = ["https://management.azure.com/.default"],
            SetupSteps = [
                "Create an Azure AD application registration",
                "Generate a client secret",
                "Grant appropriate Azure RBAC roles",
                "Configure the integration with tenant ID, client ID, and secret"
            ],
            AddedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });

        AddIntegration(new MarketplaceIntegration
        {
            Id = "gcp",
            Name = "Google Cloud Platform",
            Description = "Connect to GCP for Compute Engine, Cloud Storage, Cloud SQL, and Monitoring",
            Category = IntegrationCategory.CloudProvider,
            AuthMethod = AuthMethod.GCP_ServiceAccount,
            Icon = "\uE753",
            LogoUrl = "https://cloud.google.com/favicon.ico",
            WebsiteUrl = "https://cloud.google.com",
            DocumentationUrl = "https://cloud.google.com/docs",
            IsFeatured = true,
            PopularityScore = 85,
            Tags = ["cloud", "gcp", "google", "compute", "infrastructure"],
            Features = ["Compute Engine Instances", "Cloud Storage Buckets", "Cloud SQL Databases", "Cloud Monitoring", "Firewall Rules", "VPC Networks"],
            RequiredScopes = ["https://www.googleapis.com/auth/cloud-platform"],
            SetupSteps = [
                "Create a service account in GCP Console",
                "Download the JSON key file",
                "Configure the integration with the key file"
            ],
            AddedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });

        // Source Control
        AddIntegration(new MarketplaceIntegration
        {
            Id = "github",
            Name = "GitHub",
            Description = "Connect to GitHub for repositories, pull requests, issues, and Actions",
            Category = IntegrationCategory.SourceControl,
            AuthMethod = AuthMethod.OAuth2,
            Icon = "\uE943",
            LogoUrl = "https://github.com/favicon.ico",
            WebsiteUrl = "https://github.com",
            DocumentationUrl = "https://docs.github.com",
            IsFeatured = true,
            PopularityScore = 98,
            Tags = ["git", "source control", "repositories", "pull requests", "ci/cd"],
            Features = ["Repository Management", "Pull Request Workflows", "GitHub Actions", "Issue Tracking", "Code Review", "Webhooks"],
            RequiredScopes = ["repo", "workflow", "read:org"],
            SetupSteps = [
                "Create a GitHub OAuth App or Personal Access Token",
                "Configure the required scopes",
                "Authorize Control Room to access your repositories"
            ],
            AddedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });

        AddIntegration(new MarketplaceIntegration
        {
            Id = "gitlab",
            Name = "GitLab",
            Description = "Connect to GitLab for repositories, merge requests, and CI/CD pipelines",
            Category = IntegrationCategory.SourceControl,
            AuthMethod = AuthMethod.OAuth2,
            Icon = "\uE943",
            LogoUrl = "https://gitlab.com/favicon.ico",
            WebsiteUrl = "https://gitlab.com",
            DocumentationUrl = "https://docs.gitlab.com",
            IsFeatured = false,
            PopularityScore = 75,
            Tags = ["git", "source control", "merge requests", "ci/cd", "devops"],
            Features = ["Repository Management", "Merge Requests", "CI/CD Pipelines", "Issue Boards", "Container Registry"],
            RequiredScopes = ["api", "read_repository", "write_repository"],
            SetupSteps = [
                "Create a GitLab application or Personal Access Token",
                "Configure the required scopes",
                "Add your GitLab instance URL if self-hosted"
            ],
            AddedAt = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero)
        });

        // Issue Tracking
        AddIntegration(new MarketplaceIntegration
        {
            Id = "jira",
            Name = "Jira",
            Description = "Connect to Jira for project management, issues, sprints, and workflows",
            Category = IntegrationCategory.IssueTracking,
            AuthMethod = AuthMethod.ApiKey,
            Icon = "\uE762",
            LogoUrl = "https://jira.atlassian.com/favicon.ico",
            WebsiteUrl = "https://www.atlassian.com/software/jira",
            DocumentationUrl = "https://developer.atlassian.com/cloud/jira/platform/rest/v3/",
            IsFeatured = true,
            PopularityScore = 92,
            Tags = ["issue tracking", "project management", "agile", "sprints", "kanban"],
            Features = ["Issue Management", "Sprint Planning", "Workflow Automation", "Custom Fields", "JQL Search", "Webhooks"],
            RequiredScopes = ["read:jira-work", "write:jira-work"],
            SetupSteps = [
                "Generate an API token from your Atlassian account",
                "Enter your Jira Cloud URL",
                "Configure the integration with your email and API token"
            ],
            AddedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });

        AddIntegration(new MarketplaceIntegration
        {
            Id = "linear",
            Name = "Linear",
            Description = "Connect to Linear for modern issue tracking and project management",
            Category = IntegrationCategory.IssueTracking,
            AuthMethod = AuthMethod.OAuth2,
            Icon = "\uE762",
            LogoUrl = "https://linear.app/favicon.ico",
            WebsiteUrl = "https://linear.app",
            DocumentationUrl = "https://developers.linear.app",
            IsFeatured = false,
            PopularityScore = 70,
            Tags = ["issue tracking", "project management", "modern", "fast"],
            Features = ["Issue Management", "Cycles", "Projects", "Roadmaps", "Integrations"],
            RequiredScopes = ["read", "write", "issues:create"],
            SetupSteps = [
                "Create a Linear API key or OAuth application",
                "Configure the required permissions",
                "Connect your Linear workspace"
            ],
            AddedAt = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero)
        });

        // Incident Management
        AddIntegration(new MarketplaceIntegration
        {
            Id = "pagerduty",
            Name = "PagerDuty",
            Description = "Connect to PagerDuty for incident management, on-call schedules, and alerting",
            Category = IntegrationCategory.Alerting,
            AuthMethod = AuthMethod.ApiKey,
            Icon = "\uE814",
            LogoUrl = "https://www.pagerduty.com/favicon.ico",
            WebsiteUrl = "https://www.pagerduty.com",
            DocumentationUrl = "https://developer.pagerduty.com",
            IsFeatured = true,
            PopularityScore = 88,
            Tags = ["incident management", "on-call", "alerts", "escalation"],
            Features = ["Incident Management", "On-Call Schedules", "Escalation Policies", "Event API", "Service Health", "Analytics"],
            RequiredScopes = ["read", "write"],
            SetupSteps = [
                "Generate a PagerDuty API key",
                "Optionally create integration keys for event routing",
                "Configure escalation policies as needed"
            ],
            AddedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });

        AddIntegration(new MarketplaceIntegration
        {
            Id = "opsgenie",
            Name = "Opsgenie",
            Description = "Connect to Opsgenie for alert management and on-call scheduling",
            Category = IntegrationCategory.Alerting,
            AuthMethod = AuthMethod.ApiKey,
            Icon = "\uE814",
            LogoUrl = "https://www.atlassian.com/software/opsgenie/favicon.ico",
            WebsiteUrl = "https://www.atlassian.com/software/opsgenie",
            DocumentationUrl = "https://docs.opsgenie.com",
            IsFeatured = false,
            PopularityScore = 72,
            Tags = ["incident management", "on-call", "alerts", "atlassian"],
            Features = ["Alert Management", "On-Call Schedules", "Routing Rules", "Integrations"],
            RequiredScopes = ["read", "write"],
            SetupSteps = [
                "Generate an Opsgenie API key",
                "Configure team and integration settings"
            ],
            AddedAt = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero)
        });

        // Communication
        AddIntegration(new MarketplaceIntegration
        {
            Id = "slack",
            Name = "Slack",
            Description = "Connect to Slack for messaging, notifications, and ChatOps",
            Category = IntegrationCategory.Communication,
            AuthMethod = AuthMethod.OAuth2,
            Icon = "\uE8BD",
            LogoUrl = "https://slack.com/favicon.ico",
            WebsiteUrl = "https://slack.com",
            DocumentationUrl = "https://api.slack.com",
            IsFeatured = true,
            PopularityScore = 95,
            Tags = ["messaging", "notifications", "chatops", "collaboration"],
            Features = ["Channel Messaging", "Direct Messages", "Block Kit", "Slash Commands", "Webhooks", "App Home"],
            RequiredScopes = ["chat:write", "channels:read", "users:read"],
            SetupSteps = [
                "Create a Slack App in your workspace",
                "Configure OAuth scopes",
                "Install the app to your workspace",
                "Use the bot token for integration"
            ],
            AddedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });

        AddIntegration(new MarketplaceIntegration
        {
            Id = "teams",
            Name = "Microsoft Teams",
            Description = "Connect to Microsoft Teams for messaging and notifications",
            Category = IntegrationCategory.Communication,
            AuthMethod = AuthMethod.OAuth2,
            Icon = "\uE8BD",
            LogoUrl = "https://teams.microsoft.com/favicon.ico",
            WebsiteUrl = "https://www.microsoft.com/microsoft-teams",
            DocumentationUrl = "https://docs.microsoft.com/microsoftteams",
            IsFeatured = false,
            PopularityScore = 80,
            Tags = ["messaging", "notifications", "microsoft", "collaboration"],
            Features = ["Channel Messaging", "Adaptive Cards", "Webhooks", "Bot Framework"],
            RequiredScopes = ["ChannelMessage.Send", "Chat.ReadWrite"],
            SetupSteps = [
                "Register an Azure AD application",
                "Configure Teams permissions",
                "Create incoming webhooks for channels"
            ],
            AddedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });

        // Monitoring
        AddIntegration(new MarketplaceIntegration
        {
            Id = "datadog",
            Name = "Datadog",
            Description = "Connect to Datadog for metrics, logs, and APM",
            Category = IntegrationCategory.Monitoring,
            AuthMethod = AuthMethod.ApiKey,
            Icon = "\uE9D9",
            LogoUrl = "https://www.datadoghq.com/favicon.ico",
            WebsiteUrl = "https://www.datadoghq.com",
            DocumentationUrl = "https://docs.datadoghq.com",
            IsFeatured = true,
            PopularityScore = 85,
            Tags = ["monitoring", "metrics", "logs", "apm", "observability"],
            Features = ["Metrics", "Logs", "APM", "Dashboards", "Monitors", "Events"],
            RequiredScopes = ["dashboards_read", "metrics_read", "monitors_read"],
            SetupSteps = [
                "Generate a Datadog API key and Application key",
                "Configure the site (US, EU, etc.)",
                "Set up the integration"
            ],
            AddedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });

        AddIntegration(new MarketplaceIntegration
        {
            Id = "prometheus",
            Name = "Prometheus",
            Description = "Connect to Prometheus for metrics collection and alerting",
            Category = IntegrationCategory.Monitoring,
            AuthMethod = AuthMethod.ApiKey,
            Icon = "\uE9D9",
            LogoUrl = "https://prometheus.io/favicon.ico",
            WebsiteUrl = "https://prometheus.io",
            DocumentationUrl = "https://prometheus.io/docs",
            IsFeatured = false,
            PopularityScore = 78,
            Tags = ["monitoring", "metrics", "time series", "open source"],
            Features = ["PromQL Queries", "Metrics Scraping", "Alerting Rules", "Federation"],
            RequiredScopes = [],
            SetupSteps = [
                "Ensure your Prometheus instance is accessible",
                "Configure basic auth if enabled",
                "Add the Prometheus URL to the integration"
            ],
            AddedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });
    }

    private void AddIntegration(MarketplaceIntegration integration)
    {
        _integrations[integration.Id] = integration;

        if (!_byCategory.ContainsKey(integration.Category))
        {
            _byCategory[integration.Category] = [];
        }
        _byCategory[integration.Category].Add(integration.Id);
    }
}

/// <summary>
/// Marketplace integration metadata.
/// </summary>
public sealed record MarketplaceIntegration
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required IntegrationCategory Category { get; init; }
    public required AuthMethod AuthMethod { get; init; }
    public required string Icon { get; init; }
    public string? LogoUrl { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? DocumentationUrl { get; init; }
    public bool IsFeatured { get; init; }
    public int PopularityScore { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> Features { get; init; } = [];
    public IReadOnlyList<string> RequiredScopes { get; init; } = [];
    public IReadOnlyList<string> SetupSteps { get; init; } = [];
    public DateTimeOffset AddedAt { get; init; }

    /// <summary>
    /// Converts to domain Integration model.
    /// </summary>
    public IntegrationModel ToDomainModel()
    {
        return new IntegrationModel(
            IntegrationId.New(),
            Id,
            Name,
            Description,
            Category,
            AuthMethod,
            Icon,
            DocumentationUrl ?? WebsiteUrl ?? string.Empty,
            IsBuiltIn: false,
            IsEnabled: true,
            new IntegrationCapabilities(
                SupportsWebhooks: true,
                SupportsPush: true,
                SupportsPull: true,
                SupportsSync: true,
                SupportsEvents: true,
                SupportsActions: true,
                SupportsHealthCheck: true,
                Features.ToList(),
                [WebhookEventType.Generic]),
            new IntegrationConfig(
                [],
                RequiredScopes.ToList(),
                null,
                null,
                null,
                WebsiteUrl,
                new Dictionary<string, string>(),
                60,
                30000),
            AddedAt,
            null);
    }
}

/// <summary>
/// Category statistics.
/// </summary>
public sealed record CategoryStats(
    IntegrationCategory Category,
    int TotalCount,
    int FeaturedCount);
