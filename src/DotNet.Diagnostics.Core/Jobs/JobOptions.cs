namespace DotNet.Diagnostics.Core;

public interface IJobOptions
{
    public const string DefaultSectionName = "Jobs";

    /// <summary>
    /// Gets or sets how long before a job gets expired.
    /// </summary>
    /// <value></value>
    public TimeSpan Expiry { get; set; }
}