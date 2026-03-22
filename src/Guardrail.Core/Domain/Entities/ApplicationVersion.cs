namespace Guardrail.Core.Domain.Entities;

/// <summary>
/// Represents a versioned snapshot of an <see cref="Application"/>'s configuration at a point in time.
/// </summary>
public sealed class ApplicationVersion : BaseEntity
{
    /// <summary>Foreign key to the parent <see cref="Application"/>.</summary>
    public Guid ApplicationId { get; private set; }

    /// <summary>Semantic or sequential version number (e.g., "1.0.0", "v2").</summary>
    public string VersionNumber { get; private set; } = string.Empty;

    /// <summary>Indicates whether this version is the currently active deployment.</summary>
    public bool IsActive { get; private set; }

    /// <summary>UTC timestamp when this version was deployed.</summary>
    public DateTimeOffset DeployedAt { get; private set; }

    /// <summary>
    /// JSON snapshot of the application's complete configuration at the time of deployment.
    /// This provides an immutable audit trail of what configuration was active at any point.
    /// </summary>
    public string ConfigurationSnapshot { get; private set; } = string.Empty;

    private ApplicationVersion() { }

    /// <summary>
    /// Factory method to create a new <see cref="ApplicationVersion"/>.
    /// </summary>
    public static ApplicationVersion Create(
        Guid applicationId,
        string versionNumber,
        string configurationSnapshot,
        string? createdBy = null)
    {
        if (applicationId == Guid.Empty)
            throw new ArgumentException("ApplicationId must not be empty.", nameof(applicationId));

        ArgumentException.ThrowIfNullOrWhiteSpace(versionNumber, nameof(versionNumber));
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationSnapshot, nameof(configurationSnapshot));

        return new ApplicationVersion
        {
            ApplicationId = applicationId,
            VersionNumber = versionNumber.Trim(),
            ConfigurationSnapshot = configurationSnapshot,
            DeployedAt = DateTimeOffset.UtcNow,
            IsActive = false,
            CreatedBy = createdBy
        };
    }

    /// <summary>Marks this version as the active deployment.</summary>
    public void Activate(string updatedBy)
    {
        IsActive = true;
        DeployedAt = DateTimeOffset.UtcNow;
        MarkUpdated(updatedBy);
    }

    /// <summary>Deactivates this version (e.g., rolled back or superseded).</summary>
    public void Deactivate(string updatedBy)
    {
        IsActive = false;
        MarkUpdated(updatedBy);
    }

    /// <summary>Updates the configuration snapshot for this version.</summary>
    public void UpdateSnapshot(string configurationSnapshot, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationSnapshot, nameof(configurationSnapshot));

        if (IsActive)
            throw new InvalidOperationException("Cannot update the configuration snapshot of an active version.");

        ConfigurationSnapshot = configurationSnapshot;
        MarkUpdated(updatedBy);
    }
}
