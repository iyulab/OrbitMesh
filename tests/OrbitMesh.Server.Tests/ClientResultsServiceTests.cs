using OrbitMesh.Core.Models;
using OrbitMesh.Server.Services;

namespace OrbitMesh.Server.Tests;

public class ClientResultsServiceTests
{
    #region Interface Tests

    [Fact]
    public void IClientResultsService_Interface_Exists()
    {
        // Arrange & Act
        var interfaceType = typeof(IClientResultsService);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void ClientResultsService_Implements_Interface()
    {
        // Arrange & Act
        var serviceType = typeof(ClientResultsService);

        // Assert
        serviceType.GetInterfaces().Should().Contain(typeof(IClientResultsService));
    }

    #endregion

    #region AgentCallbackRequest Tests

    [Fact]
    public void AgentCallbackRequest_Create_Generates_Unique_Id()
    {
        // Act
        var request1 = AgentCallbackRequest.Create(AgentCallbackType.HealthCheck);
        var request2 = AgentCallbackRequest.Create(AgentCallbackType.HealthCheck);

        // Assert
        request1.CallbackId.Should().NotBeNullOrEmpty();
        request2.CallbackId.Should().NotBeNullOrEmpty();
        request1.CallbackId.Should().NotBe(request2.CallbackId);
    }

    [Fact]
    public void AgentCallbackRequest_Create_Sets_Properties()
    {
        // Act
        var request = AgentCallbackRequest.Create(
            AgentCallbackType.GetCapabilities,
            jobId: "job-123",
            payload: [1, 2, 3],
            timeout: TimeSpan.FromMinutes(5));

        // Assert
        request.Type.Should().Be(AgentCallbackType.GetCapabilities);
        request.JobId.Should().Be("job-123");
        request.Payload.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
        request.Timeout.Should().Be(TimeSpan.FromMinutes(5));
        request.IssuedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AgentCallbackRequest_Default_Timeout_Is_30_Seconds()
    {
        // Act
        var request = AgentCallbackRequest.Create(AgentCallbackType.HealthCheck);

        // Assert
        request.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    #endregion

    #region AgentCallbackResponse Tests

    [Fact]
    public void AgentCallbackResponse_Succeeded_Creates_Success_Response()
    {
        // Act
        var response = AgentCallbackResponse.Succeeded("callback-123", [1, 2, 3]);

        // Assert
        response.CallbackId.Should().Be("callback-123");
        response.Success.Should().BeTrue();
        response.Payload.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
        response.ErrorMessage.Should().BeNull();
        response.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void AgentCallbackResponse_Failed_Creates_Failure_Response()
    {
        // Act
        var response = AgentCallbackResponse.Failed(
            "callback-123",
            "Something went wrong",
            "ERR_001");

        // Assert
        response.CallbackId.Should().Be("callback-123");
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be("Something went wrong");
        response.ErrorCode.Should().Be("ERR_001");
        response.Payload.Should().BeNull();
    }

    #endregion

    #region AgentResourceUsage Tests

    [Fact]
    public void AgentResourceUsage_Properties_Are_Readable()
    {
        // Arrange & Act
        var usage = new AgentResourceUsage
        {
            CpuPercentage = 50.5,
            MemoryBytes = 1024 * 1024 * 512, // 512 MB
            ActiveJobs = 5,
            QueueDepth = 10,
            AvailableWorkers = 3
        };

        // Assert
        usage.CpuPercentage.Should().Be(50.5);
        usage.MemoryBytes.Should().Be(536870912);
        usage.ActiveJobs.Should().Be(5);
        usage.QueueDepth.Should().Be(10);
        usage.AvailableWorkers.Should().Be(3);
    }

    [Fact]
    public void AgentResourceUsage_Has_AdditionalMetrics()
    {
        // Arrange & Act
        var usage = new AgentResourceUsage
        {
            CpuPercentage = 25,
            MemoryBytes = 100,
            AdditionalMetrics = new Dictionary<string, double>
            {
                ["disk_usage"] = 80.0,
                ["network_io"] = 1500.5
            }
        };

        // Assert
        usage.AdditionalMetrics.Should().NotBeNull();
        usage.AdditionalMetrics!["disk_usage"].Should().Be(80.0);
        usage.AdditionalMetrics["network_io"].Should().Be(1500.5);
    }

    #endregion

    #region AgentHealthResponse Tests

    [Fact]
    public void AgentHealthResponse_Can_Be_Created()
    {
        // Arrange & Act
        var response = new AgentHealthResponse
        {
            Status = AgentHealthStatus.Healthy,
            Message = "All systems operational",
            Uptime = TimeSpan.FromHours(48)
        };

        // Assert
        response.Status.Should().Be(AgentHealthStatus.Healthy);
        response.Message.Should().Be("All systems operational");
        response.Uptime.Should().Be(TimeSpan.FromHours(48));
        response.CheckedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AgentHealthResponse_Can_Include_Individual_Checks()
    {
        // Arrange & Act
        var response = new AgentHealthResponse
        {
            Status = AgentHealthStatus.Degraded,
            Message = "Database connection slow",
            Checks = new Dictionary<string, bool>
            {
                ["memory"] = true,
                ["cpu"] = true,
                ["database"] = false,
                ["network"] = true
            }
        };

        // Assert
        response.Checks.Should().NotBeNull();
        response.Checks!["database"].Should().BeFalse();
        response.Checks["memory"].Should().BeTrue();
    }

    #endregion

    #region AgentCallbackType Enum Tests

    [Fact]
    public void AgentCallbackType_Has_Expected_Values()
    {
        // Assert
        Enum.GetValues<AgentCallbackType>().Should().Contain([
            AgentCallbackType.HealthCheck,
            AgentCallbackType.GetCapabilities,
            AgentCallbackType.GetConfiguration,
            AgentCallbackType.CustomOperation,
            AgentCallbackType.Confirmation,
            AgentCallbackType.GetResourceUsage,
            AgentCallbackType.ValidateJob,
            AgentCallbackType.GetAgentData
        ]);
    }

    #endregion

    #region AgentHealthStatus Enum Tests

    [Fact]
    public void AgentHealthStatus_Has_Expected_Values()
    {
        // Assert
        Enum.GetValues<AgentHealthStatus>().Should().Contain([
            AgentHealthStatus.Healthy,
            AgentHealthStatus.Degraded,
            AgentHealthStatus.Unhealthy
        ]);
    }

    [Fact]
    public void AgentHealthStatus_Healthy_Is_Default()
    {
        // Arrange & Act
        var response = new AgentHealthResponse
        {
            // Status not set explicitly
        };

        // Assert - default value should be Healthy (0)
        response.Status.Should().Be(AgentHealthStatus.Healthy);
    }

    #endregion
}
