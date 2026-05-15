using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NATS.Client;
using openrmf_templates_api.Controllers;
using openrmf_templates_api.Data;
using openrmf_templates_api.Models;
using tests.TestData;
using Xunit;

namespace tests.Controllers;

public class TemplateControllerTests
{
    [Fact]
    public async Task UploadNewChecklist_ReturnsOk_AndPublishesAudit()
    {
        var templateRepo = new Mock<ITemplateRepository>();
        var logger = new Mock<ILogger<TemplateController>>();
        var msgConnection = new Mock<IConnection>();

        templateRepo
            .Setup(r => r.AddTemplate(It.IsAny<Template>()))
            .ReturnsAsync((Template t) => t);

        var controller = CreateController(templateRepo.Object, logger.Object, msgConnection.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = BuildUser(Guid.NewGuid().ToString())
            }
        };

        var formFile = BuildChecklistFile(XmlSamples.Checklist);

        var result = await controller.UploadNewChecklist(formFile, "My &amp; Title", "Desc &amp; More");

        var ok = Assert.IsType<OkObjectResult>(result);
        var savedTemplate = Assert.IsType<Template>(ok.Value);
        Assert.Equal("My & Title", savedTemplate.title);
        Assert.Equal("Desc & More", savedTemplate.description);
        msgConnection.Verify(c => c.Publish("openrmf.audit.save", It.IsAny<byte[]>()), Times.Once);
        msgConnection.Verify(c => c.Flush(), Times.Once);
    }

    [Fact]
    public async Task UploadNewChecklist_ReturnsBadRequest_WhenXmlIsInvalid()
    {
        var templateRepo = new Mock<ITemplateRepository>();
        var logger = new Mock<ILogger<TemplateController>>();
        var msgConnection = new Mock<IConnection>();

        var controller = CreateController(templateRepo.Object, logger.Object, msgConnection.Object);

        var result = await controller.UploadNewChecklist(BuildChecklistFile("<bad-xml>"), "title");

        Assert.IsType<BadRequestResult>(result);
        templateRepo.Verify(r => r.AddTemplate(It.IsAny<Template>()), Times.Never);
    }

    [Fact]
    public async Task UpdateChecklist_ReturnsNotFound_WhenOriginalRecordMissing()
    {
        var templateRepo = new Mock<ITemplateRepository>();
        var logger = new Mock<ILogger<TemplateController>>();
        var msgConnection = new Mock<IConnection>();

        templateRepo.Setup(r => r.GetTemplate("missing-id")).ReturnsAsync((Template)null);

        var controller = CreateController(templateRepo.Object, logger.Object, msgConnection.Object);

        var result = await controller.UpdateChecklist("missing-id", BuildChecklistFile(XmlSamples.Checklist), "desc");

        Assert.IsType<NotFoundResult>(result);
        templateRepo.Verify(r => r.UpdateTemplate(It.IsAny<string>(), It.IsAny<Template>()), Times.Never);
    }

    [Fact]
    public async Task ListTemplates_ClearsRawChecklist_InResponse()
    {
        var templateRepo = new Mock<ITemplateRepository>();
        var logger = new Mock<ILogger<TemplateController>>();
        var msgConnection = new Mock<IConnection>();

        templateRepo
            .Setup(r => r.GetAllTemplates())
            .ReturnsAsync(new List<Template>
            {
                new() { title = "one", rawChecklist = "<xml>" },
                new() { title = "two", rawChecklist = "<xml2>" }
            });

        var controller = CreateController(templateRepo.Object, logger.Object, msgConnection.Object);

        var result = await controller.ListTemplates();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<Template>>(ok.Value);
        Assert.All(list, t => Assert.Equal(string.Empty, t.rawChecklist));
    }

    [Fact]
    public async Task GetTemplate_ReturnsNotFound_WhenRecordDoesNotExist()
    {
        var templateRepo = new Mock<ITemplateRepository>();
        var logger = new Mock<ILogger<TemplateController>>();
        var msgConnection = new Mock<IConnection>();

        templateRepo.Setup(r => r.GetTemplate("404")).ReturnsAsync((Template)null);
        var controller = CreateController(templateRepo.Object, logger.Object, msgConnection.Object);

        var result = await controller.GetTemplate("404");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetTemplate_ReturnsTemplate_WhenRecordExists()
    {
        var templateRepo = new Mock<ITemplateRepository>();
        var logger = new Mock<ILogger<TemplateController>>();
        var msgConnection = new Mock<IConnection>();

        templateRepo.Setup(r => r.GetTemplate("1")).ReturnsAsync(new Template
        {
            title = "t",
            rawChecklist = XmlSamples.Checklist
        });

        var controller = CreateController(templateRepo.Object, logger.Object, msgConnection.Object);

        var result = await controller.GetTemplate("1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var template = Assert.IsType<Template>(ok.Value);
        Assert.NotNull(template.CHECKLIST);
        Assert.Single(template.CHECKLIST.STIGS.iSTIG.VULN);
    }

    [Fact]
    public async Task DownloadTemplate_ReturnsRawChecklist()
    {
        var templateRepo = new Mock<ITemplateRepository>();
        var logger = new Mock<ILogger<TemplateController>>();
        var msgConnection = new Mock<IConnection>();

        templateRepo.Setup(r => r.GetTemplate("7")).ReturnsAsync(new Template { rawChecklist = "<xml/>" });

        var controller = CreateController(templateRepo.Object, logger.Object, msgConnection.Object);

        var result = await controller.DownloadTemplate("7");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("<xml/>", ok.Value);
    }

    [Fact]
    public async Task DeleteTemplate_ReturnsNotFound_WhenRecordMissing()
    {
        var templateRepo = new Mock<ITemplateRepository>();
        var logger = new Mock<ILogger<TemplateController>>();
        var msgConnection = new Mock<IConnection>();

        templateRepo.Setup(r => r.GetTemplate("x")).ReturnsAsync((Template)null);
        var controller = CreateController(templateRepo.Object, logger.Object, msgConnection.Object);

        var result = await controller.DeleteTemplate("x");

        Assert.IsType<NotFoundResult>(result);
        templateRepo.Verify(r => r.RemoveTemplate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CountEndpoints_ReturnOk_WhenRepositoryReturnsCounts()
    {
        var templateRepo = new Mock<ITemplateRepository>();
        var logger = new Mock<ILogger<TemplateController>>();
        var msgConnection = new Mock<IConnection>();

        templateRepo.Setup(r => r.CountTemplates()).ReturnsAsync(10);
        templateRepo.Setup(r => r.CountUserTemplates()).ReturnsAsync(7);
        templateRepo.Setup(r => r.CountSystemTemplates()).ReturnsAsync(3);

        var controller = CreateController(templateRepo.Object, logger.Object, msgConnection.Object);

        var all = Assert.IsType<OkObjectResult>(await controller.CountTemplates());
        var users = Assert.IsType<OkObjectResult>(await controller.CountUserTemplates());
        var systems = Assert.IsType<OkObjectResult>(await controller.CountSystemTemplates());

        Assert.Equal(10L, all.Value);
        Assert.Equal(7L, users.Value);
        Assert.Equal(3L, systems.Value);
    }

    private static TemplateController CreateController(
        ITemplateRepository repository,
        ILogger<TemplateController> logger,
        IConnection messageConnection)
    {
        var server = new NATSServer { connection = messageConnection };
        var options = Microsoft.Extensions.Options.Options.Create(server);
        return new TemplateController(repository, logger, options);
    }

    private static IFormFile BuildChecklistFile(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "checklistFile", "sample.ckl");
    }

    private static ClaimsPrincipal BuildUser(string userId)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("name", "Test User"),
                new Claim("preferred_username", "tester"),
                new Claim(ClaimTypes.Email, "test@example.com")
            },
            authenticationType: "test");

        return new ClaimsPrincipal(identity);
    }
}
