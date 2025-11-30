using FluentAssertions;
using OrbitMesh.Client;
using OrbitMesh.Core.Enums;

namespace OrbitMesh.Client.Tests;

public class OrbitMeshClientTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_With_ValidUri_Creates_Instance()
    {
        // Arrange & Act
        using var client = new OrbitMeshClient("https://localhost:5000");

        // Assert
        client.Should().NotBeNull();
        client.ServerUri.Should().Be("https://localhost:5000");
    }

    [Fact]
    public void Constructor_With_NullUri_Throws_ArgumentNullException()
    {
        // Arrange & Act
        var act = () => new OrbitMeshClient((string)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serverUri");
    }

    [Fact]
    public void Constructor_With_EmptyUri_Throws_ArgumentException()
    {
        // Arrange & Act
        var act = () => new OrbitMeshClient(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("serverUri");
    }

    #endregion

    #region Connection State Tests

    [Fact]
    public void IsConnected_When_NotConnected_Returns_False()
    {
        // Arrange
        using var client = new OrbitMeshClient("https://localhost:5000");

        // Act & Assert
        client.IsConnected.Should().BeFalse();
    }

    #endregion

    #region JobRequest Builder Tests

    [Fact]
    public void CreateJobRequest_Returns_Builder()
    {
        // Arrange
        using var client = new OrbitMeshClient("https://localhost:5000");

        // Act
        var builder = client.CreateJobRequest("test-command");

        // Assert
        builder.Should().NotBeNull();
    }

    [Fact]
    public void CreateJobRequest_With_NullCommand_Throws()
    {
        // Arrange
        using var client = new OrbitMeshClient("https://localhost:5000");

        // Act
        var act = () => client.CreateJobRequest(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("command");
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task DisposeAsync_Can_Be_Called_Multiple_Times()
    {
        // Arrange
        var client = new OrbitMeshClient("https://localhost:5000");

        // Act
        await client.DisposeAsync();
        await client.DisposeAsync();

        // Assert
        // Should not throw
    }

    [Fact]
    public void Dispose_Can_Be_Called_Multiple_Times()
    {
        // Arrange
        var client = new OrbitMeshClient("https://localhost:5000");

        // Act
        client.Dispose();
        client.Dispose();

        // Assert
        // Should not throw
    }

    #endregion
}

public class JobRequestBuilderTests
{
    #region Build Tests

    [Fact]
    public void Build_Creates_JobRequest_With_Command()
    {
        // Arrange
        var builder = new JobRequestBuilder("test-command");

        // Act
        var request = builder.Build();

        // Assert
        request.Should().NotBeNull();
        request.Command.Should().Be("test-command");
        request.Id.Should().NotBeNullOrEmpty();
        request.IdempotencyKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void WithIdempotencyKey_Sets_Key()
    {
        // Arrange
        var builder = new JobRequestBuilder("test-command");

        // Act
        var request = builder
            .WithIdempotencyKey("custom-key")
            .Build();

        // Assert
        request.IdempotencyKey.Should().Be("custom-key");
    }

    [Fact]
    public void WithParameters_Sets_Serialized_Parameters()
    {
        // Arrange
        var builder = new JobRequestBuilder("test-command");
        var parameters = new { Name = "test", Value = 42 };

        // Act
        var request = builder
            .WithParameters(parameters)
            .Build();

        // Assert
        request.Parameters.Should().NotBeNull();
        request.Parameters.Should().NotBeEmpty();
    }

    [Fact]
    public void WithPriority_Sets_Priority()
    {
        // Arrange
        var builder = new JobRequestBuilder("test-command");

        // Act
        var request = builder
            .WithPriority(10)
            .Build();

        // Assert
        request.Priority.Should().Be(10);
    }

    [Fact]
    public void WithTimeout_Sets_Timeout()
    {
        // Arrange
        var builder = new JobRequestBuilder("test-command");
        var timeout = TimeSpan.FromMinutes(5);

        // Act
        var request = builder
            .WithTimeout(timeout)
            .Build();

        // Assert
        request.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void WithRetries_Sets_MaxRetries()
    {
        // Arrange
        var builder = new JobRequestBuilder("test-command");

        // Act
        var request = builder
            .WithRetries(3)
            .Build();

        // Assert
        request.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void WithTargetAgent_Sets_TargetAgentId()
    {
        // Arrange
        var builder = new JobRequestBuilder("test-command");

        // Act
        var request = builder
            .WithTargetAgent("agent-123")
            .Build();

        // Assert
        request.TargetAgentId.Should().Be("agent-123");
    }

    [Fact]
    public void WithRequiredCapabilities_Sets_Capabilities()
    {
        // Arrange
        var builder = new JobRequestBuilder("test-command");

        // Act
        var request = builder
            .WithRequiredCapabilities("gpu", "llm")
            .Build();

        // Assert
        request.RequiredCapabilities.Should().BeEquivalentTo(["gpu", "llm"]);
    }

    [Fact]
    public void WithCorrelationId_Sets_CorrelationId()
    {
        // Arrange
        var builder = new JobRequestBuilder("test-command");

        // Act
        var request = builder
            .WithCorrelationId("correlation-123")
            .Build();

        // Assert
        request.CorrelationId.Should().Be("correlation-123");
    }

    [Fact]
    public void WithMetadata_Sets_Metadata()
    {
        // Arrange
        var builder = new JobRequestBuilder("test-command");
        var metadata = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        // Act
        var request = builder
            .WithMetadata(metadata)
            .Build();

        // Assert
        request.Metadata.Should().BeEquivalentTo(metadata);
    }

    [Fact]
    public void WithPattern_Sets_ExecutionPattern()
    {
        // Arrange
        var builder = new JobRequestBuilder("test-command");

        // Act
        var request = builder
            .WithPattern(ExecutionPattern.FireAndForget)
            .Build();

        // Assert
        request.Pattern.Should().Be(ExecutionPattern.FireAndForget);
    }

    [Fact]
    public void Fluent_Chain_Works_Correctly()
    {
        // Arrange
        var builder = new JobRequestBuilder("complex-command");

        // Act
        var request = builder
            .WithIdempotencyKey("idem-key")
            .WithPriority(5)
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithRetries(2)
            .WithTargetAgent("target-agent")
            .WithCorrelationId("trace-123")
            .WithPattern(ExecutionPattern.RequestResponse)
            .Build();

        // Assert
        request.Command.Should().Be("complex-command");
        request.IdempotencyKey.Should().Be("idem-key");
        request.Priority.Should().Be(5);
        request.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        request.MaxRetries.Should().Be(2);
        request.TargetAgentId.Should().Be("target-agent");
        request.CorrelationId.Should().Be("trace-123");
        request.Pattern.Should().Be(ExecutionPattern.RequestResponse);
    }

    #endregion
}

public class OrbitMeshClientOptionsTests
{
    [Fact]
    public void Default_Values_Are_Set()
    {
        // Arrange & Act
        var options = new OrbitMeshClientOptions();

        // Assert
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.AutoReconnect.Should().BeTrue();
        options.MaxReconnectAttempts.Should().Be(5);
        options.ReconnectDelay.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ServerUri_Can_Be_Set()
    {
        // Arrange
        var options = new OrbitMeshClientOptions
        {
            ServerUri = "https://localhost:5000"
        };

        // Assert
        options.ServerUri.Should().Be("https://localhost:5000");
    }

    [Fact]
    public void HubPath_Default_Is_Agent()
    {
        // Arrange & Act
        var options = new OrbitMeshClientOptions();

        // Assert
        options.HubPath.Should().Be("/agent");
    }
}
