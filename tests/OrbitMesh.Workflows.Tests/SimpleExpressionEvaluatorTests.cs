using OrbitMesh.Workflows.Execution;

namespace OrbitMesh.Workflows.Tests;

/// <summary>
/// Tests for the simple expression evaluator.
/// </summary>
public class SimpleExpressionEvaluatorTests
{
    private readonly SimpleExpressionEvaluator _evaluator = new();

    [Fact]
    public async Task EvaluateBoolAsync_TrueLiteral_ReturnsTrue()
    {
        // Act
        var result = await _evaluator.EvaluateBoolAsync("true", new Dictionary<string, object?>());

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateBoolAsync_FalseLiteral_ReturnsFalse()
    {
        // Act
        var result = await _evaluator.EvaluateBoolAsync("false", new Dictionary<string, object?>());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateBoolAsync_BoolVariable_ReturnsVariableValue()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["isEnabled"] = true
        };

        // Act
        var result = await _evaluator.EvaluateBoolAsync("isEnabled", variables);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateBoolAsync_VariableReference_ReturnsVariableValue()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["status"] = true
        };

        // Act
        var result = await _evaluator.EvaluateBoolAsync("${status}", variables);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateBoolAsync_NullVariable_ReturnsFalse()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["value"] = null
        };

        // Act
        var result = await _evaluator.EvaluateBoolAsync("value", variables);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateBoolAsync_NonEmptyString_ReturnsTrue()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["text"] = "hello"
        };

        // Act
        var result = await _evaluator.EvaluateBoolAsync("text", variables);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateBoolAsync_EmptyString_ReturnsFalse()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["text"] = ""
        };

        // Act
        var result = await _evaluator.EvaluateBoolAsync("text", variables);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateBoolAsync_EqualityComparison_ReturnsCorrectResult()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["status"] = "success"
        };

        // Act
        var result = await _evaluator.EvaluateBoolAsync("status == 'success'", variables);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateBoolAsync_InequalityComparison_ReturnsCorrectResult()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["status"] = "error"
        };

        // Act
        var result = await _evaluator.EvaluateBoolAsync("status != 'success'", variables);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateBoolAsync_NumericComparison_ReturnsCorrectResult()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["count"] = 10
        };

        // Act
        var greaterThan = await _evaluator.EvaluateBoolAsync("count > 5", variables);
        var lessThan = await _evaluator.EvaluateBoolAsync("count < 20", variables);
        var greaterOrEqual = await _evaluator.EvaluateBoolAsync("count >= 10", variables);
        var lessOrEqual = await _evaluator.EvaluateBoolAsync("count <= 10", variables);

        // Assert
        greaterThan.Should().BeTrue();
        lessThan.Should().BeTrue();
        greaterOrEqual.Should().BeTrue();
        lessOrEqual.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateBoolAsync_AndOperator_ReturnsCorrectResult()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["a"] = true,
            ["b"] = true
        };

        // Act
        var result = await _evaluator.EvaluateBoolAsync("a && b", variables);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateBoolAsync_OrOperator_ReturnsCorrectResult()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["a"] = false,
            ["b"] = true
        };

        // Act
        var result = await _evaluator.EvaluateBoolAsync("a || b", variables);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateBoolAsync_NotOperator_ReturnsCorrectResult()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["value"] = false
        };

        // Act
        var result = await _evaluator.EvaluateBoolAsync("!value", variables);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_IntegerLiteral_ReturnsInteger()
    {
        // Act
        var result = await _evaluator.EvaluateAsync("42", new Dictionary<string, object?>());

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task EvaluateAsync_DoubleLiteral_ReturnsDouble()
    {
        // Act
        var result = await _evaluator.EvaluateAsync("3.14", new Dictionary<string, object?>());

        // Assert
        result.Should().Be(3.14);
    }

    [Fact]
    public async Task EvaluateAsync_StringLiteral_ReturnsString()
    {
        // Act
        var result = await _evaluator.EvaluateAsync("\"hello\"", new Dictionary<string, object?>());

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public async Task EvaluateAsync_NestedPropertyAccess_ReturnsValue()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["order"] = new Dictionary<string, object?>
            {
                ["customer"] = new Dictionary<string, object?>
                {
                    ["name"] = "John Doe"
                }
            }
        };

        // Act
        var result = await _evaluator.EvaluateAsync("order.customer.name", variables);

        // Assert
        result.Should().Be("John Doe");
    }

    [Fact]
    public async Task InterpolateAsync_SimpleTemplate_ReplacesVariables()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["name"] = "Alice",
            ["age"] = 30
        };

        // Act
        var result = await _evaluator.InterpolateAsync("Hello, ${name}! You are ${age} years old.", variables);

        // Assert
        result.Should().Be("Hello, Alice! You are 30 years old.");
    }

    [Fact]
    public async Task InterpolateAsync_MissingVariable_ReturnsEmptyString()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["name"] = "Bob"
        };

        // Act
        var result = await _evaluator.InterpolateAsync("Hello, ${name}! Your age is ${age}.", variables);

        // Assert
        result.Should().Be("Hello, Bob! Your age is .");
    }

    [Fact]
    public async Task InterpolateAsync_NestedPropertyInTemplate_ReplacesCorrectly()
    {
        // Arrange
        var variables = new Dictionary<string, object?>
        {
            ["user"] = new Dictionary<string, object?>
            {
                ["email"] = "test@example.com"
            }
        };

        // Act
        var result = await _evaluator.InterpolateAsync("Email: ${user.email}", variables);

        // Assert
        result.Should().Be("Email: test@example.com");
    }

    [Fact]
    public async Task EvaluateAsync_ObjectPropertyAccess_ReturnsPropertyValue()
    {
        // Arrange
        var testObject = new TestClass { Name = "TestValue", Count = 100 };
        var variables = new Dictionary<string, object?>
        {
            ["obj"] = testObject
        };

        // Act
        var nameResult = await _evaluator.EvaluateAsync("obj.Name", variables);
        var countResult = await _evaluator.EvaluateAsync("obj.Count", variables);

        // Assert
        nameResult.Should().Be("TestValue");
        countResult.Should().Be(100);
    }

    private sealed class TestClass
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
