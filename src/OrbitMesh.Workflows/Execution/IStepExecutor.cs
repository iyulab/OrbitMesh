using OrbitMesh.Workflows.Models;

namespace OrbitMesh.Workflows.Execution;

/// <summary>
/// Interface for step executors that handle specific step types.
/// </summary>
public interface IStepExecutor
{
    /// <summary>
    /// The step type this executor handles.
    /// </summary>
    StepType StepType { get; }

    /// <summary>
    /// Executes the step.
    /// </summary>
    /// <param name="context">Execution context with step and instance information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The step execution result.</returns>
    Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Context for step execution.
/// </summary>
public sealed class StepExecutionContext
{
    /// <summary>
    /// The current workflow instance.
    /// </summary>
    public required WorkflowInstance WorkflowInstance { get; init; }

    /// <summary>
    /// The step definition to execute.
    /// </summary>
    public required WorkflowStep Step { get; init; }

    /// <summary>
    /// The current step instance state.
    /// </summary>
    public required StepInstance StepInstance { get; init; }

    /// <summary>
    /// Current workflow variables.
    /// </summary>
    public required Dictionary<string, object?> Variables { get; init; }
}

/// <summary>
/// Result of step execution.
/// </summary>
public sealed record StepExecutionResult
{
    /// <summary>
    /// Resulting status of the step.
    /// </summary>
    public required StepStatus Status { get; init; }

    /// <summary>
    /// Output data from the step.
    /// </summary>
    public object? Output { get; init; }

    /// <summary>
    /// Error message if the step failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Job ID if a job was created.
    /// </summary>
    public string? JobId { get; init; }

    /// <summary>
    /// Sub-workflow instance ID if a sub-workflow was started.
    /// </summary>
    public string? SubWorkflowInstanceId { get; init; }

    /// <summary>
    /// Branch instances for parallel/foreach steps.
    /// </summary>
    public IReadOnlyList<BranchInstance>? Branches { get; init; }

    /// <summary>
    /// Creates a successful completion result.
    /// </summary>
    public static StepExecutionResult Completed(object? output = null) => new()
    {
        Status = StepStatus.Completed,
        Output = output
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static StepExecutionResult Failed(string error) => new()
    {
        Status = StepStatus.Failed,
        Error = error
    };

    /// <summary>
    /// Creates a waiting for event result.
    /// </summary>
    public static StepExecutionResult WaitingForEvent() => new()
    {
        Status = StepStatus.WaitingForEvent
    };

    /// <summary>
    /// Creates a waiting for approval result.
    /// </summary>
    public static StepExecutionResult WaitingForApproval() => new()
    {
        Status = StepStatus.WaitingForApproval
    };
}

/// <summary>
/// Factory for creating step executors.
/// </summary>
public interface IStepExecutorFactory
{
    /// <summary>
    /// Creates an executor for the specified step type.
    /// </summary>
    /// <param name="stepType">The step type.</param>
    /// <returns>The step executor.</returns>
    IStepExecutor Create(StepType stepType);
}

/// <summary>
/// Expression evaluator for workflow conditions and expressions.
/// </summary>
public interface IExpressionEvaluator
{
    /// <summary>
    /// Evaluates a boolean expression.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="variables">Variables available to the expression.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The boolean result.</returns>
    Task<bool> EvaluateBoolAsync(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates an expression and returns the result.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="variables">Variables available to the expression.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluation result.</returns>
    Task<object?> EvaluateAsync(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Interpolates variables in a string template.
    /// </summary>
    /// <param name="templateString">The template string with ${variable} placeholders.</param>
    /// <param name="variables">Variables for interpolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The interpolated string.</returns>
    Task<string> InterpolateAsync(
        string templateString,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default);
}
