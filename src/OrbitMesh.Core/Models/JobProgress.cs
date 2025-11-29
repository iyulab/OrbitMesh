using MessagePack;

namespace OrbitMesh.Core.Models;

/// <summary>
/// Represents progress information for a long-running job.
/// </summary>
[MessagePackObject]
public sealed record JobProgress
{
    /// <summary>
    /// The job ID this progress belongs to.
    /// </summary>
    [Key(0)]
    public required string JobId { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    [Key(1)]
    public int Percentage { get; init; }

    /// <summary>
    /// Current step description.
    /// </summary>
    [Key(2)]
    public string? CurrentStep { get; init; }

    /// <summary>
    /// Total number of steps (if known).
    /// </summary>
    [Key(3)]
    public int? TotalSteps { get; init; }

    /// <summary>
    /// Current step number (if using steps).
    /// </summary>
    [Key(4)]
    public int? CurrentStepNumber { get; init; }

    /// <summary>
    /// Estimated time remaining.
    /// </summary>
    [Key(5)]
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    /// Optional message with additional context.
    /// </summary>
    [Key(6)]
    public string? Message { get; init; }

    /// <summary>
    /// Timestamp of this progress update.
    /// </summary>
    [Key(7)]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a progress update with percentage.
    /// </summary>
    public static JobProgress Create(string jobId, int percentage, string? message = null) =>
        new()
        {
            JobId = jobId,
            Percentage = Math.Clamp(percentage, 0, 100),
            Message = message
        };

    /// <summary>
    /// Creates a step-based progress update.
    /// </summary>
    public static JobProgress CreateStep(string jobId, int currentStep, int totalSteps, string? stepDescription = null) =>
        new()
        {
            JobId = jobId,
            CurrentStepNumber = currentStep,
            TotalSteps = totalSteps,
            CurrentStep = stepDescription,
            Percentage = totalSteps > 0 ? (int)((double)currentStep / totalSteps * 100) : 0
        };
}
