using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using openrmf_templates_api.Controllers;
using openrmf_templates_api.Data;
using Xunit;

namespace tests.Controllers;

public class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsOk_WhenRepositoryIsHealthy()
    {
        var logger = new Mock<ILogger<HealthController>>();
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(x => x.HealthStatus()).Returns(true);

        var controller = new HealthController(repo.Object, logger.Object);

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("ok", ok.Value);
        repo.Verify(x => x.HealthStatus(), Times.Once);
    }

    [Fact]
    public void Get_ReturnsBadRequest_WhenRepositoryIsUnhealthy()
    {
        var logger = new Mock<ILogger<HealthController>>();
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(x => x.HealthStatus()).Returns(false);

        var controller = new HealthController(repo.Object, logger.Object);

        var result = controller.Get();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("database error", badRequest.Value);
        repo.Verify(x => x.HealthStatus(), Times.Once);
    }

    [Fact]
    public void Get_ReturnsBadRequest_WhenRepositoryThrows()
    {
        var logger = new Mock<ILogger<HealthController>>();
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(x => x.HealthStatus()).Throws(new InvalidOperationException("boom"));

        var controller = new HealthController(repo.Object, logger.Object);

        var result = controller.Get();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Improper API configuration", badRequest.Value);
        repo.Verify(x => x.HealthStatus(), Times.Once);
    }
}
