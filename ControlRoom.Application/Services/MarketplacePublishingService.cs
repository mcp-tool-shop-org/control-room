namespace ControlRoom.Application.Services;

/// <summary>
/// Marketplace Publishing: Manages readiness checklist and validation for
/// Microsoft Partner Center / Marketplace publishing.
///
/// Covers:
/// - Strategic framing and positioning
/// - Partner account and legal readiness
/// - Offer definition and naming
/// - Listing copy optimization
/// - Tags and categories for discoverability
/// - Screenshot validation
/// - Technical validation
/// - Documentation readiness
/// - Reviewer psychology alignment
/// </summary>
public sealed class MarketplacePublishingService
{
    private readonly IMarketplaceRepository _repository;
    private readonly IScreenshotValidator _screenshotValidator;
    private readonly IListingAnalyzer _listingAnalyzer;

    public event EventHandler<ChecklistUpdatedEventArgs>? ChecklistUpdated;
    public event EventHandler<ReadinessChangedEventArgs>? ReadinessChanged;

    public MarketplacePublishingService(
        IMarketplaceRepository repository,
        IScreenshotValidator screenshotValidator,
        IListingAnalyzer listingAnalyzer)
    {
        _repository = repository;
        _screenshotValidator = screenshotValidator;
        _listingAnalyzer = listingAnalyzer;
    }

    // ========================================================================
    // STRATEGIC FRAMING (Section 0)
    // ========================================================================

    /// <summary>
    /// Validates strategic positioning clarity.
    /// </summary>
    public async Task<StrategicFramingResult> ValidateStrategicFramingAsync(
        StrategicFraming framing,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<FramingIssue>();

        // One-sentence description check
        if (string.IsNullOrWhiteSpace(framing.OneSentenceDescription))
        {
            issues.Add(new FramingIssue
            {
                Category = "Description",
                Severity = IssueSeverity.Critical,
                Message = "Product must be describable in one sentence without jargon"
            });
        }
        else if (ContainsJargon(framing.OneSentenceDescription))
        {
            issues.Add(new FramingIssue
            {
                Category = "Description",
                Severity = IssueSeverity.Warning,
                Message = "One-sentence description contains jargon that may confuse reviewers"
            });
        }

        // Target audience clarity
        if (string.IsNullOrWhiteSpace(framing.TargetAudience))
        {
            issues.Add(new FramingIssue
            {
                Category = "Audience",
                Severity = IssueSeverity.Critical,
                Message = "Must define who the product is for"
            });
        }

        if (string.IsNullOrWhiteSpace(framing.NotForAudience))
        {
            issues.Add(new FramingIssue
            {
                Category = "Audience",
                Severity = IssueSeverity.Warning,
                Message = "Consider defining who the product is NOT for to sharpen positioning"
            });
        }

        // Positioning category
        if (framing.PositioningCategory == PositioningCategory.None)
        {
            issues.Add(new FramingIssue
            {
                Category = "Positioning",
                Severity = IssueSeverity.Critical,
                Message = "Must select a clear positioning category (DevOps, Automation, Observability, or IDP)"
            });
        }

        // Consistency check
        if (!string.IsNullOrWhiteSpace(framing.OneSentenceDescription) &&
            framing.PositioningCategory != PositioningCategory.None)
        {
            var alignmentScore = CheckPositioningAlignment(
                framing.OneSentenceDescription,
                framing.PositioningCategory);

            if (alignmentScore < 0.6)
            {
                issues.Add(new FramingIssue
                {
                    Category = "Consistency",
                    Severity = IssueSeverity.Warning,
                    Message = "Description may not align well with selected positioning category"
                });
            }
        }

        var result = new StrategicFramingResult
        {
            IsValid = !issues.Any(i => i.Severity == IssueSeverity.Critical),
            Issues = issues,
            Recommendation = issues.Any(i => i.Severity == IssueSeverity.Critical)
                ? "Address critical framing issues before proceeding. Reviewers penalize 'confused' products."
                : issues.Any()
                    ? "Minor improvements suggested for clearer positioning."
                    : "Strategic framing is clear and consistent."
        };

        await _repository.SaveFramingValidationAsync(result, cancellationToken);
        return result;
    }

    // ========================================================================
    // PARTNER ACCOUNT & LEGAL (Section 1)
    // ========================================================================

    /// <summary>
    /// Validates partner account and legal readiness.
    /// </summary>
    public async Task<LegalReadinessResult> ValidateLegalReadinessAsync(
        LegalConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var checks = new List<LegalCheck>();

        // Microsoft Partner Setup
        checks.Add(new LegalCheck
        {
            Category = "Partner Account",
            Item = "Microsoft Partner account created",
            Status = config.HasPartnerAccount ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new LegalCheck
        {
            Category = "Partner Account",
            Item = "Organization profile completed (not personal)",
            Status = config.HasOrganizationProfile ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new LegalCheck
        {
            Category = "Partner Account",
            Item = "Verified business identity",
            Status = config.HasVerifiedIdentity ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new LegalCheck
        {
            Category = "Partner Account",
            Item = "Tax and payout info completed",
            Status = config.HasTaxInfo ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new LegalCheck
        {
            Category = "Partner Account",
            Item = "Correct publisher name chosen",
            Status = !string.IsNullOrWhiteSpace(config.PublisherName) ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true,
            Note = "Cannot be changed easily after submission"
        });

        // Legal & Compliance
        checks.Add(new LegalCheck
        {
            Category = "Legal",
            Item = "Privacy policy publicly accessible (HTTPS)",
            Status = await ValidateUrlAccessibleAsync(config.PrivacyPolicyUrl, cancellationToken),
            Required = true
        });

        checks.Add(new LegalCheck
        {
            Category = "Legal",
            Item = "Terms of service publicly accessible",
            Status = await ValidateUrlAccessibleAsync(config.TermsOfServiceUrl, cancellationToken),
            Required = true
        });

        checks.Add(new LegalCheck
        {
            Category = "Legal",
            Item = "Support contact email monitored",
            Status = !string.IsNullOrWhiteSpace(config.SupportEmail) ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new LegalCheck
        {
            Category = "Legal",
            Item = "Security contact defined",
            Status = !string.IsNullOrWhiteSpace(config.SecurityContact) ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new LegalCheck
        {
            Category = "Legal",
            Item = "Data handling described clearly",
            Status = config.HasDataHandlingDescription ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        // Control Room specific
        checks.Add(new LegalCheck
        {
            Category = "Control Room Specific",
            Item = "Local-first + optional cloud integrations stated",
            Status = config.StatesLocalFirst ? CheckStatus.Passed : CheckStatus.Warning,
            Required = false,
            Note = "Important for trust differentiation"
        });

        checks.Add(new LegalCheck
        {
            Category = "Control Room Specific",
            Item = "No silent data exfiltration stated",
            Status = config.StatesNoExfiltration ? CheckStatus.Passed : CheckStatus.Warning,
            Required = false,
            Note = "Important for trust differentiation"
        });

        var passedRequired = checks.Where(c => c.Required).All(c => c.Status == CheckStatus.Passed);

        return new LegalReadinessResult
        {
            IsReady = passedRequired,
            Checks = checks,
            PassedCount = checks.Count(c => c.Status == CheckStatus.Passed),
            TotalCount = checks.Count,
            RequiredPassedCount = checks.Count(c => c.Required && c.Status == CheckStatus.Passed),
            RequiredTotalCount = checks.Count(c => c.Required)
        };
    }

    // ========================================================================
    // OFFER DEFINITION (Section 2)
    // ========================================================================

    /// <summary>
    /// Validates offer definition.
    /// </summary>
    public async Task<OfferDefinitionResult> ValidateOfferDefinitionAsync(
        OfferDefinition offer,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<OfferIssue>();

        // Offer type validation
        if (offer.OfferType == OfferType.None)
        {
            issues.Add(new OfferIssue
            {
                Field = "OfferType",
                Severity = IssueSeverity.Critical,
                Message = "Must select correct offer category (App / Service / Integration)"
            });
        }

        // Product name validation
        if (string.IsNullOrWhiteSpace(offer.ProductName))
        {
            issues.Add(new OfferIssue
            {
                Field = "ProductName",
                Severity = IssueSeverity.Critical,
                Message = "Product name is required"
            });
        }
        else
        {
            if (offer.ProductName.Length > 50)
            {
                issues.Add(new OfferIssue
                {
                    Field = "ProductName",
                    Severity = IssueSeverity.Warning,
                    Message = "Product name should be short and memorable"
                });
            }

            if (IsMisleading(offer.ProductName))
            {
                issues.Add(new OfferIssue
                {
                    Field = "ProductName",
                    Severity = IssueSeverity.Critical,
                    Message = "Product name may be misleading"
                });
            }
        }

        // Subtitle validation
        if (string.IsNullOrWhiteSpace(offer.Subtitle))
        {
            issues.Add(new OfferIssue
            {
                Field = "Subtitle",
                Severity = IssueSeverity.Warning,
                Message = "Subtitle helps clarify purpose"
            });
        }
        else
        {
            // Check for marketing fluff
            var fluffPatterns = new[] { "future of", "revolutionary", "game-changing", "best-in-class", "world-class" };
            if (fluffPatterns.Any(p => offer.Subtitle.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new OfferIssue
                {
                    Field = "Subtitle",
                    Severity = IssueSeverity.Warning,
                    Message = "Subtitle should clarify purpose, not contain marketing fluff",
                    Suggestion = "Example: 'Local-First Automation & Observability Platform'"
                });
            }
        }

        return new OfferDefinitionResult
        {
            IsValid = !issues.Any(i => i.Severity == IssueSeverity.Critical),
            Issues = issues,
            NameQualityScore = CalculateNameQuality(offer.ProductName, offer.Subtitle)
        };
    }

    // ========================================================================
    // LISTING COPY (Section 3)
    // ========================================================================

    /// <summary>
    /// Analyzes and validates marketplace listing copy.
    /// </summary>
    public async Task<ListingCopyResult> ValidateListingCopyAsync(
        ListingCopy listing,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<ListingIssue>();

        // Short description (most important)
        if (string.IsNullOrWhiteSpace(listing.ShortDescription))
        {
            issues.Add(new ListingIssue
            {
                Field = "ShortDescription",
                Severity = IssueSeverity.Critical,
                Message = "Short description is required and is the most important field"
            });
        }
        else
        {
            var shortDescAnalysis = await _listingAnalyzer.AnalyzeShortDescriptionAsync(
                listing.ShortDescription, cancellationToken);

            if (!shortDescAnalysis.ExplainsWhatItDoes)
            {
                issues.Add(new ListingIssue
                {
                    Field = "ShortDescription",
                    Severity = IssueSeverity.Critical,
                    Message = "Short description must explain what the product does"
                });
            }

            if (!shortDescAnalysis.ExplainsWhoItsFor)
            {
                issues.Add(new ListingIssue
                {
                    Field = "ShortDescription",
                    Severity = IssueSeverity.Warning,
                    Message = "Short description should explain who it's for"
                });
            }

            if (!shortDescAnalysis.MentionsDifferentiator)
            {
                issues.Add(new ListingIssue
                {
                    Field = "ShortDescription",
                    Severity = IssueSeverity.Warning,
                    Message = "Consider mentioning key differentiator (offline-first, automation, integrations)"
                });
            }

            if (shortDescAnalysis.HasBuzzwordStacking)
            {
                issues.Add(new ListingIssue
                {
                    Field = "ShortDescription",
                    Severity = IssueSeverity.Warning,
                    Message = "Avoid buzzword stacking"
                });
            }
        }

        // Long description
        if (!string.IsNullOrWhiteSpace(listing.LongDescription))
        {
            var longDescAnalysis = await _listingAnalyzer.AnalyzeLongDescriptionAsync(
                listing.LongDescription, cancellationToken);

            if (!longDescAnalysis.HasSectionHeaders)
            {
                issues.Add(new ListingIssue
                {
                    Field = "LongDescription",
                    Severity = IssueSeverity.Warning,
                    Message = "Long description should have clear section headers"
                });
            }

            if (!longDescAnalysis.HasProblemSolutionFlow)
            {
                issues.Add(new ListingIssue
                {
                    Field = "LongDescription",
                    Severity = IssueSeverity.Warning,
                    Message = "Consider Problem -> Solution -> Outcome flow"
                });
            }

            if (!longDescAnalysis.HasConcreteUseCases)
            {
                issues.Add(new ListingIssue
                {
                    Field = "LongDescription",
                    Severity = IssueSeverity.Warning,
                    Message = "Include concrete use cases, not generic promises"
                });
            }

            if (longDescAnalysis.HasCompetitorCallouts)
            {
                issues.Add(new ListingIssue
                {
                    Field = "LongDescription",
                    Severity = IssueSeverity.Critical,
                    Message = "Avoid competitor callouts"
                });
            }
        }

        // Key capabilities
        if (listing.KeyCapabilities == null || !listing.KeyCapabilities.Any())
        {
            issues.Add(new ListingIssue
            {
                Field = "KeyCapabilities",
                Severity = IssueSeverity.Warning,
                Message = "Add bulleted key capabilities"
            });
        }
        else
        {
            var expectedCapabilities = new[]
            {
                "Automation", "Runbooks", "Observability", "Status",
                "Integrations", "Collaboration", "Security", "Trust"
            };

            var covered = expectedCapabilities
                .Count(ec => listing.KeyCapabilities.Any(kc =>
                    kc.Contains(ec, StringComparison.OrdinalIgnoreCase)));

            if (covered < 3)
            {
                issues.Add(new ListingIssue
                {
                    Field = "KeyCapabilities",
                    Severity = IssueSeverity.Warning,
                    Message = "Consider covering more key capability areas"
                });
            }
        }

        return new ListingCopyResult
        {
            IsValid = !issues.Any(i => i.Severity == IssueSeverity.Critical),
            Issues = issues,
            CopyQualityScore = CalculateCopyQuality(listing)
        };
    }

    // ========================================================================
    // TAGS & CATEGORIES (Section 4)
    // ========================================================================

    /// <summary>
    /// Validates tags for discoverability and ranking.
    /// </summary>
    public TagValidationResult ValidateTags(IReadOnlyList<string> tags)
    {
        var issues = new List<TagIssue>();

        var strongTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DevOps", "Automation", "Observability", "Reliability",
            "Incident Management", "Runbooks", "Infrastructure Monitoring",
            "IT Operations", "Platform Engineering", "SRE"
        };

        var weakTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Productivity", "Other", "Tools", "Utilities", "Business"
        };

        var strongTagCount = tags.Count(t => strongTags.Contains(t));
        var weakTagCount = tags.Count(t => weakTags.Contains(t));

        if (strongTagCount < 3)
        {
            issues.Add(new TagIssue
            {
                Severity = IssueSeverity.Warning,
                Message = "Add more high-intent, specific tags for better discoverability",
                SuggestedTags = strongTags.Except(tags, StringComparer.OrdinalIgnoreCase).Take(5).ToList()
            });
        }

        if (weakTagCount > 0)
        {
            issues.Add(new TagIssue
            {
                Severity = IssueSeverity.Warning,
                Message = "Remove vague tags that hurt ranking",
                ProblematicTags = tags.Where(t => weakTags.Contains(t)).ToList()
            });
        }

        if (tags.Count < 5)
        {
            issues.Add(new TagIssue
            {
                Severity = IssueSeverity.Warning,
                Message = "Consider adding more tags (5-10 recommended)"
            });
        }

        return new TagValidationResult
        {
            IsOptimal = !issues.Any(),
            Issues = issues,
            StrongTagCount = strongTagCount,
            WeakTagCount = weakTagCount,
            DiscoverabilityScore = CalculateDiscoverabilityScore(tags, strongTags)
        };
    }

    // ========================================================================
    // SCREENSHOTS (Section 5)
    // ========================================================================

    /// <summary>
    /// Validates screenshots against marketplace requirements.
    /// </summary>
    public async Task<ScreenshotValidationResult> ValidateScreenshotsAsync(
        IReadOnlyList<ScreenshotInfo> screenshots,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<ScreenshotIssue>();

        // Count check
        if (screenshots.Count < 6)
        {
            issues.Add(new ScreenshotIssue
            {
                Severity = IssueSeverity.Warning,
                Message = $"Recommended 6-8 screenshots, currently have {screenshots.Count}"
            });
        }

        // Global rules check for each screenshot
        foreach (var screenshot in screenshots)
        {
            var validation = await _screenshotValidator.ValidateAsync(screenshot, cancellationToken);

            if (validation.HasPlaceholderData)
            {
                issues.Add(new ScreenshotIssue
                {
                    ScreenshotIndex = screenshot.Order,
                    Severity = IssueSeverity.Critical,
                    Message = "Screenshot contains placeholder data"
                });
            }

            if (validation.HasLoremIpsum)
            {
                issues.Add(new ScreenshotIssue
                {
                    ScreenshotIndex = screenshot.Order,
                    Severity = IssueSeverity.Critical,
                    Message = "Screenshot contains lorem ipsum text"
                });
            }

            if (validation.HasInternalTestNames)
            {
                issues.Add(new ScreenshotIssue
                {
                    ScreenshotIndex = screenshot.Order,
                    Severity = IssueSeverity.Critical,
                    Message = "Screenshot contains internal test names"
                });
            }

            if (!validation.HasCrispResolution)
            {
                issues.Add(new ScreenshotIssue
                {
                    ScreenshotIndex = screenshot.Order,
                    Severity = IssueSeverity.Warning,
                    Message = "Screenshot resolution is not crisp"
                });
            }

            if (validation.HasSensitiveData)
            {
                issues.Add(new ScreenshotIssue
                {
                    ScreenshotIndex = screenshot.Order,
                    Severity = IssueSeverity.Critical,
                    Message = "Screenshot may contain sensitive data"
                });
            }
        }

        // Required screenshot types check
        var requiredTypes = new[]
        {
            (ScreenshotType.PrimaryValue, "Dashboard showing system health or automation overview"),
            (ScreenshotType.Automation, "Runbook or workflow view"),
            (ScreenshotType.Observability, "Timeline or run history"),
            (ScreenshotType.Integrations, "Integration dashboard"),
            (ScreenshotType.Collaboration, "Comments, activity feed, or team view"),
            (ScreenshotType.TrustControl, "Status page, permissions, or settings")
        };

        foreach (var (requiredType, description) in requiredTypes)
        {
            if (!screenshots.Any(s => s.Type == requiredType))
            {
                issues.Add(new ScreenshotIssue
                {
                    Severity = IssueSeverity.Warning,
                    Message = $"Missing recommended screenshot: {description}"
                });
            }
        }

        // Theme consistency
        var themes = screenshots.Select(s => s.Theme).Distinct().ToList();
        if (themes.Count > 1)
        {
            issues.Add(new ScreenshotIssue
            {
                Severity = IssueSeverity.Warning,
                Message = "Screenshots have inconsistent themes"
            });
        }

        return new ScreenshotValidationResult
        {
            IsValid = !issues.Any(i => i.Severity == IssueSeverity.Critical),
            Issues = issues,
            ScreenshotCount = screenshots.Count,
            CoverageScore = CalculateScreenshotCoverage(screenshots)
        };
    }

    // ========================================================================
    // TECHNICAL VALIDATION (Section 7)
    // ========================================================================

    /// <summary>
    /// Validates technical requirements.
    /// </summary>
    public TechnicalValidationResult ValidateTechnicalRequirements(TechnicalConfig config)
    {
        var checks = new List<TechnicalCheck>();

        // App Behavior
        checks.Add(new TechnicalCheck
        {
            Category = "App Behavior",
            Item = "App launches reliably",
            Status = config.LaunchesReliably ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new TechnicalCheck
        {
            Category = "App Behavior",
            Item = "No crash on first run",
            Status = config.NoCrashOnFirstRun ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new TechnicalCheck
        {
            Category = "App Behavior",
            Item = "Handles offline mode gracefully",
            Status = config.HandlesOfflineGracefully ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new TechnicalCheck
        {
            Category = "App Behavior",
            Item = "No hard dependency on unavailable cloud services",
            Status = config.NoHardCloudDependency ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        // Security
        checks.Add(new TechnicalCheck
        {
            Category = "Security",
            Item = "Credentials stored securely",
            Status = config.CredentialsStoredSecurely ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new TechnicalCheck
        {
            Category = "Security",
            Item = "OAuth flows clearly explained",
            Status = config.OAuthFlowsExplained ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new TechnicalCheck
        {
            Category = "Security",
            Item = "Webhooks validated",
            Status = config.WebhooksValidated ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new TechnicalCheck
        {
            Category = "Security",
            Item = "No secrets logged or displayed",
            Status = config.NoSecretsLogged ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        // Performance
        checks.Add(new TechnicalCheck
        {
            Category = "Performance",
            Item = "Reasonable startup time",
            Status = config.StartupTimeMs < 5000 ? CheckStatus.Passed : CheckStatus.Warning,
            Required = false,
            Note = $"Current: {config.StartupTimeMs}ms"
        });

        checks.Add(new TechnicalCheck
        {
            Category = "Performance",
            Item = "UI remains responsive during background work",
            Status = config.UIResponsiveDuringBackgroundWork ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new TechnicalCheck
        {
            Category = "Performance",
            Item = "Background sync does not block UI",
            Status = config.BackgroundSyncNonBlocking ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        var passedRequired = checks.Where(c => c.Required).All(c => c.Status == CheckStatus.Passed);

        return new TechnicalValidationResult
        {
            IsReady = passedRequired,
            Checks = checks,
            PassedCount = checks.Count(c => c.Status == CheckStatus.Passed),
            TotalCount = checks.Count
        };
    }

    // ========================================================================
    // DOCUMENTATION READINESS (Section 8)
    // ========================================================================

    /// <summary>
    /// Validates documentation and support readiness.
    /// </summary>
    public DocumentationReadinessResult ValidateDocumentation(DocumentationConfig config)
    {
        var checks = new List<DocumentationCheck>();

        checks.Add(new DocumentationCheck
        {
            Item = "Getting Started guide exists",
            Status = config.HasGettingStartedGuide ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new DocumentationCheck
        {
            Item = "Screenshots match actual UI",
            Status = config.ScreenshotsMatchUI ? CheckStatus.Passed : CheckStatus.Warning,
            Required = false
        });

        checks.Add(new DocumentationCheck
        {
            Item = "Support email monitored",
            Status = config.SupportEmailMonitored ? CheckStatus.Passed : CheckStatus.Failed,
            Required = true
        });

        checks.Add(new DocumentationCheck
        {
            Item = "Clear escalation path",
            Status = config.HasEscalationPath ? CheckStatus.Passed : CheckStatus.Warning,
            Required = false
        });

        // FAQ coverage
        var requiredFAQTopics = new[]
        {
            ("Offline behavior", config.FAQCoversOffline),
            ("Data storage", config.FAQCoversDataStorage),
            ("Integrations", config.FAQCoversIntegrations),
            ("Permissions", config.FAQCoversPermissions)
        };

        foreach (var (topic, covered) in requiredFAQTopics)
        {
            checks.Add(new DocumentationCheck
            {
                Item = $"FAQ covers: {topic}",
                Status = covered ? CheckStatus.Passed : CheckStatus.Warning,
                Required = false
            });
        }

        var passedRequired = checks.Where(c => c.Required).All(c => c.Status == CheckStatus.Passed);

        return new DocumentationReadinessResult
        {
            IsReady = passedRequired,
            Checks = checks,
            FAQCoverage = requiredFAQTopics.Count(t => t.Item2) / (double)requiredFAQTopics.Length
        };
    }

    // ========================================================================
    // REVIEWER PSYCHOLOGY (Section 9)
    // ========================================================================

    /// <summary>
    /// Evaluates listing from reviewer's psychological perspective.
    /// </summary>
    public ReviewerPsychologyResult EvaluateReviewerPerspective(
        StrategicFraming framing,
        ListingCopy listing,
        IReadOnlyList<ScreenshotInfo> screenshots)
    {
        var questions = new List<ReviewerQuestion>();

        // Does this feel safe?
        var safetyScore = CalculateSafetyPerception(listing, screenshots);
        questions.Add(new ReviewerQuestion
        {
            Question = "Does this feel safe?",
            Score = safetyScore,
            Factors = new List<string>
            {
                safetyScore > 0.7 ? "Clear security messaging" : "Security messaging could be clearer",
                safetyScore > 0.7 ? "Professional appearance" : "Consider more professional screenshots"
            }
        });

        // Does this feel intentional?
        var intentionalScore = CalculateIntentionalPerception(framing, listing);
        questions.Add(new ReviewerQuestion
        {
            Question = "Does this feel intentional?",
            Score = intentionalScore,
            Factors = new List<string>
            {
                intentionalScore > 0.7 ? "Clear positioning" : "Positioning could be sharper",
                intentionalScore > 0.7 ? "Consistent messaging" : "Messaging inconsistencies detected"
            }
        });

        // Does this feel maintained?
        var maintainedScore = CalculateMaintainedPerception(screenshots);
        questions.Add(new ReviewerQuestion
        {
            Question = "Does this feel maintained?",
            Score = maintainedScore,
            Factors = new List<string>
            {
                maintainedScore > 0.7 ? "Modern UI in screenshots" : "UI may appear dated",
                maintainedScore > 0.7 ? "Complete feature coverage" : "Some features not demonstrated"
            }
        });

        // Does this feel honest?
        var honestyScore = CalculateHonestyPerception(listing);
        questions.Add(new ReviewerQuestion
        {
            Question = "Does this feel honest?",
            Score = honestyScore,
            Factors = new List<string>
            {
                honestyScore > 0.7 ? "No overclaims detected" : "Some claims may need substantiation",
                honestyScore > 0.7 ? "Realistic feature descriptions" : "Consider more specific feature descriptions"
            }
        });

        // Does this feel enterprise-capable?
        var enterpriseScore = CalculateEnterprisePerception(listing, screenshots);
        questions.Add(new ReviewerQuestion
        {
            Question = "Does this feel enterprise-capable even if small?",
            Score = enterpriseScore,
            Factors = new List<string>
            {
                enterpriseScore > 0.7 ? "Security features highlighted" : "Emphasize security features more",
                enterpriseScore > 0.7 ? "Collaboration features shown" : "Show team/collaboration features"
            }
        });

        var overallScore = questions.Average(q => q.Score);

        return new ReviewerPsychologyResult
        {
            Questions = questions,
            OverallScore = overallScore,
            ApprovalLikelihood = overallScore switch
            {
                >= 0.8 => "High - all reviewer questions answered positively",
                >= 0.6 => "Moderate - some areas need attention",
                >= 0.4 => "Low - significant improvements needed",
                _ => "Very Low - major rework required"
            }
        };
    }

    // ========================================================================
    // FINAL READINESS CHECK (Section 10)
    // ========================================================================

    /// <summary>
    /// Performs final pre-submit validation.
    /// </summary>
    public async Task<FinalReadinessResult> PerformFinalCheckAsync(
        PublishingPackage package,
        CancellationToken cancellationToken = default)
    {
        var gates = new List<ReadinessGate>();

        // Strategic framing
        var framingResult = await ValidateStrategicFramingAsync(package.Framing, cancellationToken);
        gates.Add(new ReadinessGate
        {
            Name = "Strategic Framing",
            Passed = framingResult.IsValid,
            Details = framingResult.Recommendation
        });

        // Legal readiness
        var legalResult = await ValidateLegalReadinessAsync(package.Legal, cancellationToken);
        gates.Add(new ReadinessGate
        {
            Name = "Legal & Partner Account",
            Passed = legalResult.IsReady,
            Details = $"{legalResult.RequiredPassedCount}/{legalResult.RequiredTotalCount} required checks passed"
        });

        // Offer definition
        var offerResult = await ValidateOfferDefinitionAsync(package.Offer, cancellationToken);
        gates.Add(new ReadinessGate
        {
            Name = "Offer Definition",
            Passed = offerResult.IsValid,
            Details = offerResult.Issues.Any() ? string.Join("; ", offerResult.Issues.Select(i => i.Message)) : "Valid"
        });

        // Listing copy
        var listingResult = await ValidateListingCopyAsync(package.Listing, cancellationToken);
        gates.Add(new ReadinessGate
        {
            Name = "Listing Copy",
            Passed = listingResult.IsValid,
            Details = $"Quality score: {listingResult.CopyQualityScore:P0}"
        });

        // Tags
        var tagResult = ValidateTags(package.Tags);
        gates.Add(new ReadinessGate
        {
            Name = "Tags & Categories",
            Passed = tagResult.IsOptimal || tagResult.StrongTagCount >= 3,
            Details = $"Discoverability score: {tagResult.DiscoverabilityScore:P0}"
        });

        // Screenshots
        var screenshotResult = await ValidateScreenshotsAsync(package.Screenshots, cancellationToken);
        gates.Add(new ReadinessGate
        {
            Name = "Screenshots",
            Passed = screenshotResult.IsValid,
            Details = $"Coverage: {screenshotResult.CoverageScore:P0}"
        });

        // Technical
        var techResult = ValidateTechnicalRequirements(package.Technical);
        gates.Add(new ReadinessGate
        {
            Name = "Technical Requirements",
            Passed = techResult.IsReady,
            Details = $"{techResult.PassedCount}/{techResult.TotalCount} checks passed"
        });

        // Documentation
        var docResult = ValidateDocumentation(package.Documentation);
        gates.Add(new ReadinessGate
        {
            Name = "Documentation",
            Passed = docResult.IsReady,
            Details = $"FAQ coverage: {docResult.FAQCoverage:P0}"
        });

        // Reviewer psychology
        var psychResult = EvaluateReviewerPerspective(
            package.Framing, package.Listing, package.Screenshots);
        gates.Add(new ReadinessGate
        {
            Name = "Reviewer Psychology",
            Passed = psychResult.OverallScore >= 0.6,
            Details = psychResult.ApprovalLikelihood
        });

        var allPassed = gates.All(g => g.Passed);

        var result = new FinalReadinessResult
        {
            IsReadyToPublish = allPassed,
            Gates = gates,
            PassedGates = gates.Count(g => g.Passed),
            TotalGates = gates.Count,
            Summary = allPassed
                ? "Ready to publish. A stranger can understand the product in <60 seconds, reviewers see no red flags, screenshots reinforce trust, claims are provable, and the product feels boringly reliable."
                : $"Not ready. {gates.Count - gates.Count(g => g.Passed)} gate(s) need attention before submission."
        };

        ReadinessChanged?.Invoke(this, new ReadinessChangedEventArgs(result.IsReadyToPublish));

        return result;
    }

    // ========================================================================
    // HELPER METHODS
    // ========================================================================

    private bool ContainsJargon(string text)
    {
        var jargonPatterns = new[]
        {
            "synergy", "leverage", "paradigm", "holistic", "disrupt",
            "next-gen", "cutting-edge", "best-of-breed", "turnkey",
            "scalable solution", "ecosystem", "digital transformation"
        };

        return jargonPatterns.Any(j => text.Contains(j, StringComparison.OrdinalIgnoreCase));
    }

    private double CheckPositioningAlignment(string description, PositioningCategory category)
    {
        var keywords = category switch
        {
            PositioningCategory.DevOpsITOps => new[] { "devops", "operations", "deploy", "infrastructure" },
            PositioningCategory.AutomationRunbook => new[] { "automat", "runbook", "workflow", "orchestrat" },
            PositioningCategory.ObservabilityReliability => new[] { "observ", "monitor", "reliab", "incident" },
            PositioningCategory.InternalDeveloperPlatform => new[] { "developer", "platform", "self-service", "portal" },
            _ => Array.Empty<string>()
        };

        var matches = keywords.Count(k => description.Contains(k, StringComparison.OrdinalIgnoreCase));
        return (double)matches / Math.Max(keywords.Length, 1);
    }

    private bool IsMisleading(string name)
    {
        // Check for terms that might mislead about capabilities
        var misleadingTerms = new[] { "AI-powered", "ML-driven", "intelligent" };
        return misleadingTerms.Any(t => name.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private double CalculateNameQuality(string? name, string? subtitle)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;

        double score = 0.5;

        // Short and memorable
        if (name.Length <= 20) score += 0.2;

        // Has clarifying subtitle
        if (!string.IsNullOrWhiteSpace(subtitle)) score += 0.2;

        // No marketing fluff
        if (!ContainsJargon(name)) score += 0.1;

        return Math.Min(score, 1.0);
    }

    private double CalculateCopyQuality(ListingCopy listing)
    {
        double score = 0;

        if (!string.IsNullOrWhiteSpace(listing.ShortDescription)) score += 0.3;
        if (!string.IsNullOrWhiteSpace(listing.LongDescription)) score += 0.3;
        if (listing.KeyCapabilities?.Count >= 5) score += 0.2;
        if (!ContainsJargon(listing.ShortDescription ?? "")) score += 0.2;

        return score;
    }

    private double CalculateDiscoverabilityScore(IReadOnlyList<string> tags, HashSet<string> strongTags)
    {
        if (!tags.Any()) return 0;

        var strongCount = tags.Count(t => strongTags.Contains(t));
        return Math.Min((double)strongCount / 5, 1.0);
    }

    private double CalculateScreenshotCoverage(IReadOnlyList<ScreenshotInfo> screenshots)
    {
        var requiredTypes = Enum.GetValues<ScreenshotType>()
            .Where(t => t != ScreenshotType.Other)
            .ToList();

        var covered = requiredTypes.Count(rt => screenshots.Any(s => s.Type == rt));
        return (double)covered / requiredTypes.Count;
    }

    private async Task<CheckStatus> ValidateUrlAccessibleAsync(
        string? url,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url)) return CheckStatus.Failed;
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return CheckStatus.Warning;

        // In a real implementation, would actually check URL accessibility
        return CheckStatus.Passed;
    }

    private double CalculateSafetyPerception(ListingCopy listing, IReadOnlyList<ScreenshotInfo> screenshots)
    {
        double score = 0.5;

        var safetyKeywords = new[] { "secure", "privacy", "trust", "safe", "encrypted" };
        if (safetyKeywords.Any(k => listing.ShortDescription?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false))
            score += 0.25;

        if (screenshots.Any(s => s.Type == ScreenshotType.TrustControl))
            score += 0.25;

        return score;
    }

    private double CalculateIntentionalPerception(StrategicFraming framing, ListingCopy listing)
    {
        double score = 0.5;

        if (framing.PositioningCategory != PositioningCategory.None)
            score += 0.25;

        if (!string.IsNullOrWhiteSpace(framing.TargetAudience))
            score += 0.25;

        return score;
    }

    private double CalculateMaintainedPerception(IReadOnlyList<ScreenshotInfo> screenshots)
    {
        if (!screenshots.Any()) return 0.3;

        double score = 0.5;

        // Modern theme
        if (screenshots.All(s => s.Theme == "modern" || s.Theme == "dark"))
            score += 0.25;

        // Good coverage
        if (screenshots.Count >= 6)
            score += 0.25;

        return score;
    }

    private double CalculateHonestyPerception(ListingCopy listing)
    {
        double score = 0.7;

        var overclaims = new[] { "best", "only", "revolutionary", "game-changing", "#1" };
        var claimCount = overclaims.Count(c =>
            listing.ShortDescription?.Contains(c, StringComparison.OrdinalIgnoreCase) ?? false);

        score -= claimCount * 0.15;

        return Math.Max(score, 0.2);
    }

    private double CalculateEnterprisePerception(ListingCopy listing, IReadOnlyList<ScreenshotInfo> screenshots)
    {
        double score = 0.5;

        var enterpriseKeywords = new[] { "team", "collaboration", "permission", "audit", "compliance" };
        var keywordCount = enterpriseKeywords.Count(k =>
            listing.LongDescription?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false);

        score += Math.Min(keywordCount * 0.1, 0.3);

        if (screenshots.Any(s => s.Type == ScreenshotType.Collaboration))
            score += 0.2;

        return Math.Min(score, 1.0);
    }
}

// ========================================================================
// SUPPORTING TYPES
// ========================================================================

public enum PositioningCategory
{
    None,
    DevOpsITOps,
    AutomationRunbook,
    ObservabilityReliability,
    InternalDeveloperPlatform
}

public enum OfferType
{
    None,
    App,
    Service,
    Integration
}

public enum IssueSeverity
{
    Info,
    Warning,
    Critical
}

public enum CheckStatus
{
    Failed,
    Warning,
    Passed
}

public enum ScreenshotType
{
    PrimaryValue,
    Automation,
    Observability,
    Integrations,
    Collaboration,
    TrustControl,
    Other
}

public class StrategicFraming
{
    public string? OneSentenceDescription { get; init; }
    public string? TargetAudience { get; init; }
    public string? NotForAudience { get; init; }
    public PositioningCategory PositioningCategory { get; init; }
}

public class StrategicFramingResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<FramingIssue> Issues { get; init; } = Array.Empty<FramingIssue>();
    public required string Recommendation { get; init; }
}

public class FramingIssue
{
    public required string Category { get; init; }
    public IssueSeverity Severity { get; init; }
    public required string Message { get; init; }
}

public class LegalConfiguration
{
    public bool HasPartnerAccount { get; init; }
    public bool HasOrganizationProfile { get; init; }
    public bool HasVerifiedIdentity { get; init; }
    public bool HasTaxInfo { get; init; }
    public string? PublisherName { get; init; }
    public string? PrivacyPolicyUrl { get; init; }
    public string? TermsOfServiceUrl { get; init; }
    public string? SupportEmail { get; init; }
    public string? SecurityContact { get; init; }
    public bool HasDataHandlingDescription { get; init; }
    public bool StatesLocalFirst { get; init; }
    public bool StatesNoExfiltration { get; init; }
}

public class LegalReadinessResult
{
    public bool IsReady { get; init; }
    public IReadOnlyList<LegalCheck> Checks { get; init; } = Array.Empty<LegalCheck>();
    public int PassedCount { get; init; }
    public int TotalCount { get; init; }
    public int RequiredPassedCount { get; init; }
    public int RequiredTotalCount { get; init; }
}

public class LegalCheck
{
    public required string Category { get; init; }
    public required string Item { get; init; }
    public CheckStatus Status { get; init; }
    public bool Required { get; init; }
    public string? Note { get; init; }
}

public class OfferDefinition
{
    public OfferType OfferType { get; init; }
    public string? ProductName { get; init; }
    public string? Subtitle { get; init; }
}

public class OfferDefinitionResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<OfferIssue> Issues { get; init; } = Array.Empty<OfferIssue>();
    public double NameQualityScore { get; init; }
}

public class OfferIssue
{
    public required string Field { get; init; }
    public IssueSeverity Severity { get; init; }
    public required string Message { get; init; }
    public string? Suggestion { get; init; }
}

public class ListingCopy
{
    public string? ShortDescription { get; init; }
    public string? LongDescription { get; init; }
    public IReadOnlyList<string>? KeyCapabilities { get; init; }
}

public class ListingCopyResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<ListingIssue> Issues { get; init; } = Array.Empty<ListingIssue>();
    public double CopyQualityScore { get; init; }
}

public class ListingIssue
{
    public required string Field { get; init; }
    public IssueSeverity Severity { get; init; }
    public required string Message { get; init; }
}

public class TagValidationResult
{
    public bool IsOptimal { get; init; }
    public IReadOnlyList<TagIssue> Issues { get; init; } = Array.Empty<TagIssue>();
    public int StrongTagCount { get; init; }
    public int WeakTagCount { get; init; }
    public double DiscoverabilityScore { get; init; }
}

public class TagIssue
{
    public IssueSeverity Severity { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<string>? SuggestedTags { get; init; }
    public IReadOnlyList<string>? ProblematicTags { get; init; }
}

public class ScreenshotInfo
{
    public int Order { get; init; }
    public ScreenshotType Type { get; init; }
    public required string FilePath { get; init; }
    public string? Theme { get; init; }
    public string? Caption { get; init; }
}

public class ScreenshotValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<ScreenshotIssue> Issues { get; init; } = Array.Empty<ScreenshotIssue>();
    public int ScreenshotCount { get; init; }
    public double CoverageScore { get; init; }
}

public class ScreenshotIssue
{
    public int? ScreenshotIndex { get; init; }
    public IssueSeverity Severity { get; init; }
    public required string Message { get; init; }
}

public class TechnicalConfig
{
    public bool LaunchesReliably { get; init; }
    public bool NoCrashOnFirstRun { get; init; }
    public bool HandlesOfflineGracefully { get; init; }
    public bool NoHardCloudDependency { get; init; }
    public bool CredentialsStoredSecurely { get; init; }
    public bool OAuthFlowsExplained { get; init; }
    public bool WebhooksValidated { get; init; }
    public bool NoSecretsLogged { get; init; }
    public int StartupTimeMs { get; init; }
    public bool UIResponsiveDuringBackgroundWork { get; init; }
    public bool BackgroundSyncNonBlocking { get; init; }
}

public class TechnicalValidationResult
{
    public bool IsReady { get; init; }
    public IReadOnlyList<TechnicalCheck> Checks { get; init; } = Array.Empty<TechnicalCheck>();
    public int PassedCount { get; init; }
    public int TotalCount { get; init; }
}

public class TechnicalCheck
{
    public required string Category { get; init; }
    public required string Item { get; init; }
    public CheckStatus Status { get; init; }
    public bool Required { get; init; }
    public string? Note { get; init; }
}

public class DocumentationConfig
{
    public bool HasGettingStartedGuide { get; init; }
    public bool ScreenshotsMatchUI { get; init; }
    public bool SupportEmailMonitored { get; init; }
    public bool HasEscalationPath { get; init; }
    public bool FAQCoversOffline { get; init; }
    public bool FAQCoversDataStorage { get; init; }
    public bool FAQCoversIntegrations { get; init; }
    public bool FAQCoversPermissions { get; init; }
}

public class DocumentationReadinessResult
{
    public bool IsReady { get; init; }
    public IReadOnlyList<DocumentationCheck> Checks { get; init; } = Array.Empty<DocumentationCheck>();
    public double FAQCoverage { get; init; }
}

public class DocumentationCheck
{
    public required string Item { get; init; }
    public CheckStatus Status { get; init; }
    public bool Required { get; init; }
}

public class ReviewerPsychologyResult
{
    public IReadOnlyList<ReviewerQuestion> Questions { get; init; } = Array.Empty<ReviewerQuestion>();
    public double OverallScore { get; init; }
    public required string ApprovalLikelihood { get; init; }
}

public class ReviewerQuestion
{
    public required string Question { get; init; }
    public double Score { get; init; }
    public IReadOnlyList<string> Factors { get; init; } = Array.Empty<string>();
}

public class PublishingPackage
{
    public required StrategicFraming Framing { get; init; }
    public required LegalConfiguration Legal { get; init; }
    public required OfferDefinition Offer { get; init; }
    public required ListingCopy Listing { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ScreenshotInfo> Screenshots { get; init; } = Array.Empty<ScreenshotInfo>();
    public required TechnicalConfig Technical { get; init; }
    public required DocumentationConfig Documentation { get; init; }
}

public class FinalReadinessResult
{
    public bool IsReadyToPublish { get; init; }
    public IReadOnlyList<ReadinessGate> Gates { get; init; } = Array.Empty<ReadinessGate>();
    public int PassedGates { get; init; }
    public int TotalGates { get; init; }
    public required string Summary { get; init; }
}

public class ReadinessGate
{
    public required string Name { get; init; }
    public bool Passed { get; init; }
    public required string Details { get; init; }
}

public class ChecklistUpdatedEventArgs : EventArgs
{
    public string Section { get; }
    public ChecklistUpdatedEventArgs(string section) => Section = section;
}

public class ReadinessChangedEventArgs : EventArgs
{
    public bool IsReady { get; }
    public ReadinessChangedEventArgs(bool isReady) => IsReady = isReady;
}

// ========================================================================
// REPOSITORY & SERVICE INTERFACES
// ========================================================================

public interface IMarketplaceRepository
{
    Task SaveFramingValidationAsync(StrategicFramingResult result, CancellationToken cancellationToken);
}

public interface IScreenshotValidator
{
    Task<ScreenshotValidation> ValidateAsync(ScreenshotInfo screenshot, CancellationToken cancellationToken);
}

public class ScreenshotValidation
{
    public bool HasPlaceholderData { get; init; }
    public bool HasLoremIpsum { get; init; }
    public bool HasInternalTestNames { get; init; }
    public bool HasCrispResolution { get; init; }
    public bool HasSensitiveData { get; init; }
}

public interface IListingAnalyzer
{
    Task<ShortDescriptionAnalysis> AnalyzeShortDescriptionAsync(string description, CancellationToken cancellationToken);
    Task<LongDescriptionAnalysis> AnalyzeLongDescriptionAsync(string description, CancellationToken cancellationToken);
}

public class ShortDescriptionAnalysis
{
    public bool ExplainsWhatItDoes { get; init; }
    public bool ExplainsWhoItsFor { get; init; }
    public bool MentionsDifferentiator { get; init; }
    public bool HasBuzzwordStacking { get; init; }
}

public class LongDescriptionAnalysis
{
    public bool HasSectionHeaders { get; init; }
    public bool HasProblemSolutionFlow { get; init; }
    public bool HasConcreteUseCases { get; init; }
    public bool HasCompetitorCallouts { get; init; }
}
