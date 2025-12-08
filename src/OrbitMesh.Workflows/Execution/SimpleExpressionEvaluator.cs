using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OrbitMesh.Workflows.Execution;

/// <summary>
/// Simple expression evaluator for workflow conditions and templates.
/// Supports basic variable interpolation and simple comparisons.
/// </summary>
public sealed partial class SimpleExpressionEvaluator : IExpressionEvaluator
{
    /// <inheritdoc />
    public Task<bool> EvaluateBoolAsync(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        var result = EvaluateExpression(expression, variables);

        var boolResult = result switch
        {
            bool b => b,
            string s => !string.IsNullOrEmpty(s) && !s.Equals("false", StringComparison.OrdinalIgnoreCase),
            int i => i != 0,
            double d => d != 0,
            null => false,
            _ => true
        };

        return Task.FromResult(boolResult);
    }

    /// <inheritdoc />
    public Task<object?> EvaluateAsync(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        var result = EvaluateExpression(expression, variables);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<string> InterpolateAsync(
        string templateString,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        var result = InterpolationRegex().Replace(templateString, match =>
        {
            var varName = match.Groups[1].Value;
            return GetVariableValue(varName, variables)?.ToString() ?? "";
        });

        return Task.FromResult(result);
    }

    private static object? EvaluateExpression(string expression, IReadOnlyDictionary<string, object?> variables)
    {
        var trimmed = expression.Trim();

        // Simple variable reference: ${varName} or just varName
        if (trimmed.StartsWith("${", StringComparison.Ordinal) && trimmed.EndsWith('}'))
        {
            var varName = trimmed[2..^1];
            return GetVariableValue(varName, variables);
        }

        // Comparison expressions
        if (TryParseComparison(trimmed, variables, out var comparisonResult))
        {
            return comparisonResult;
        }

        // Logical operators
        if (TryParseLogicalOperator(trimmed, variables, out var logicalResult))
        {
            return logicalResult;
        }

        // Direct variable reference
        if (variables.TryGetValue(trimmed, out var directValue))
        {
            return directValue;
        }

        // Try to parse as literal first (before nested property access)
        if (bool.TryParse(trimmed, out var boolVal))
        {
            return boolVal;
        }

        if (int.TryParse(trimmed, out var intVal))
        {
            return intVal;
        }

        if (double.TryParse(trimmed, out var doubleVal))
        {
            return doubleVal;
        }

        // Check for nested property access: variable.property.subproperty
        // This must be after literal parsing to avoid treating "3.14" as property access
        if (trimmed.Contains('.', StringComparison.Ordinal))
        {
            return GetVariableValue(trimmed, variables);
        }

        // Return as string literal if quoted
        if ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) ||
            (trimmed.StartsWith('\'') && trimmed.EndsWith('\'')))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static object? GetVariableValue(string path, IReadOnlyDictionary<string, object?> variables)
    {
        var parts = path.Split('.');
        object? current = null;

        // First part is the variable name
        if (!variables.TryGetValue(parts[0], out current) || current == null)
        {
            return null;
        }

        // Navigate nested properties
        for (var i = 1; i < parts.Length; i++)
        {
            if (current == null)
            {
                return null;
            }

            var propertyName = parts[i];

            // Handle dictionary access
            if (current is IDictionary<string, object?> dict)
            {
                if (!dict.TryGetValue(propertyName, out current))
                {
                    return null;
                }

                continue;
            }

            // Handle object property access via reflection
            // Note: This uses reflection for dynamic property access in workflow expressions.
            // Trimming warning is suppressed as workflow variables are runtime-provided.
#pragma warning disable IL2075 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute'
            var currentType = current.GetType();
            var property = currentType.GetProperty(propertyName);
#pragma warning restore IL2075
            if (property != null)
            {
                current = property.GetValue(current);
                continue;
            }

            // Handle array/list indexer
            if (int.TryParse(propertyName, out var index) && current is IList<object> list)
            {
                current = index < list.Count ? list[index] : null;
                continue;
            }

            return null;
        }

        return current;
    }

    private static bool TryParseComparison(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
        out bool result)
    {
        result = false;

        // Try different comparison operators
        var operators = new[] { "==", "!=", ">=", "<=", ">", "<" };

        foreach (var op in operators)
        {
            var parts = expression.Split(op, 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            var left = EvaluateExpression(parts[0], variables);
            var right = EvaluateExpression(parts[1], variables);

            result = op switch
            {
                "==" => Equals(left, right),
                "!=" => !Equals(left, right),
                ">" => Compare(left, right) > 0,
                "<" => Compare(left, right) < 0,
                ">=" => Compare(left, right) >= 0,
                "<=" => Compare(left, right) <= 0,
                _ => false
            };

            return true;
        }

        return false;
    }

    private static bool TryParseLogicalOperator(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
        out bool result)
    {
        result = false;

        // AND operator
        if (expression.Contains(" && ", StringComparison.Ordinal))
        {
            var parts = expression.Split(" && ");
            result = parts.All(part =>
            {
                var val = EvaluateExpression(part.Trim(), variables);
                return val switch
                {
                    bool b => b,
                    null => false,
                    _ => true
                };
            });
            return true;
        }

        // OR operator
        if (expression.Contains(" || ", StringComparison.Ordinal))
        {
            var parts = expression.Split(" || ");
            result = parts.Any(part =>
            {
                var val = EvaluateExpression(part.Trim(), variables);
                return val switch
                {
                    bool b => b,
                    null => false,
                    _ => true
                };
            });
            return true;
        }

        // NOT operator
        if (expression.StartsWith('!') || expression.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
        {
            var inner = expression.StartsWith('!') ? expression[1..].Trim() : expression[4..].Trim();
            var val = EvaluateExpression(inner, variables);
            result = val switch
            {
                bool b => !b,
                null => true,
                _ => false
            };
            return true;
        }

        return false;
    }

    private static int Compare(object? left, object? right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        // Try numeric comparison
        if (TryConvertToDouble(left, out var leftNum) && TryConvertToDouble(right, out var rightNum))
        {
            return leftNum.CompareTo(rightNum);
        }

        // String comparison
        return string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal);
    }

    private static bool TryConvertToDouble(object value, out double result)
    {
        result = 0;

        return value switch
        {
            double d => (result = d) == d,
            int i => (result = i) == i,
            long l => (result = l) == l,
            float f => (result = f) == f,
            decimal dec => (result = (double)dec) == (double)dec,
            string s => double.TryParse(s, out result),
            _ => false
        };
    }

    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex InterpolationRegex();
}
