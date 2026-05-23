using System;
using MongoDB.Bson;
using NATS.Client;
using openrmf_templates_api.Models;
using Xunit;

namespace tests.Models;

public class ModelContractTests
{
    [Fact]
    public void Asset_Constructor_InitializesDefaultStrings()
    {
        var asset = new ASSET();

        Assert.Equal(string.Empty, asset.ROLE);
        Assert.Equal(string.Empty, asset.ASSET_TYPE);
        Assert.Equal(string.Empty, asset.WEB_DB_INSTANCE);
    }

    [Fact]
    public void Asset_AllowsAssignedValues()
    {
        var asset = new ASSET
        {
            ROLE = "Web",
            HOST_NAME = "host01",
            TARGET_KEY = "TK-1"
        };

        Assert.NotEqual(string.Empty, asset.ROLE);
        Assert.Equal("host01", asset.HOST_NAME);
        Assert.Equal("TK-1", asset.TARGET_KEY);
    }

    [Fact]
    public void Checklist_Constructor_InitializesNestedObjects()
    {
        var checklist = new CHECKLIST();

        Assert.NotNull(checklist.ASSET);
        Assert.NotNull(checklist.STIGS);
        Assert.NotNull(checklist.STIGS.iSTIG);
    }

    [Fact]
    public void SI_DATA_StoresNameAndValue()
    {
        var data = new SI_DATA { SID_NAME = "title", SID_DATA = "Windows STIG" };

        Assert.Equal("title", data.SID_NAME);
        Assert.NotEqual("Linux STIG", data.SID_DATA);
    }

    [Fact]
    public void STIG_DATA_StoresAttributeDataPair()
    {
        var data = new STIG_DATA { VULN_ATTRIBUTE = "Rule_ID", ATTRIBUTE_DATA = "SV-1000r1_rule" };

        Assert.Equal("Rule_ID", data.VULN_ATTRIBUTE);
        Assert.NotNull(data.ATTRIBUTE_DATA);
    }

    [Fact]
    public void STIG_INFO_Constructor_InitializesCollection()
    {
        var info = new STIG_INFO();

        Assert.NotNull(info.SI_DATA);
        Assert.Empty(info.SI_DATA);
    }

    [Fact]
    public void STIGS_Constructor_InitializesInnerObject()
    {
        var stigs = new STIGS();

        Assert.NotNull(stigs.iSTIG);
        Assert.NotNull(stigs.iSTIG.STIG_INFO);
    }

    [Fact]
    public void iSTIG_Constructor_InitializesInfoAndVulnCollection()
    {
        var stig = new iSTIG();

        Assert.NotNull(stig.STIG_INFO);
        Assert.NotNull(stig.VULN);
        Assert.Empty(stig.VULN);
    }

    [Fact]
    public void VULN_Constructor_InitializesCollection()
    {
        var vuln = new VULN();

        Assert.NotNull(vuln.STIG_DATA);
        Assert.Empty(vuln.STIG_DATA);
    }

    [Fact]
    public void Vulnerability_Constructor_SetsDefaultClass()
    {
        var vulnerability = new Vulnerability();

        Assert.Equal("Unclass", vulnerability.Class);
        Assert.Null(vulnerability.Vuln_Num);
    }

    [Fact]
    public void Template_Constructor_SetsDefaultsAndComputedProperties()
    {
        var template = new Template
        {
            title = "Windows STIG",
            version = "3",
            stigRelease = "Release: 2"
        };

        Assert.Equal("USER", template.templateType);
        Assert.NotNull(template.CHECKLIST);
        Assert.Equal("Windows STIG-V3-R2", template.fullTitle);
    }

    [Fact]
    public void Template_InternalIdString_ReturnsObjectIdText()
    {
        var template = new Template { InternalId = ObjectId.GenerateNewId() };

        Assert.Equal(template.InternalId.ToString(), template.InternalIdString);
        Assert.NotEqual(string.Empty, template.InternalIdString);
    }

    [Fact]
    public void Artifact_Constructor_InitializesChecklistAndComputedTitle()
    {
        var artifact = new Artifact
        {
            hostName = "host01",
            stigType = "Windows",
            version = "2",
            stigRelease = "R10"
        };

        Assert.NotNull(artifact.CHECKLIST);
        Assert.Equal("host01-Windows-V2-R10", artifact.title);
    }

    [Fact]
    public void Audit_Constructor_SetsNonEmptyAuditId()
    {
        var audit = new Audit();

        Assert.NotEqual(Guid.Empty, audit.auditId);
        Assert.Null(audit.message);
    }

    [Fact]
    public void Settings_HoldsConnectionAndDatabase()
    {
        var settings = new Settings
        {
            ConnectionString = "mongodb://localhost:27017",
            Database = "openrmf"
        };

        Assert.Equal("openrmf", settings.Database);
        Assert.NotNull(settings.ConnectionString);
    }

    [Fact]
    public void NATSServer_AllowsConnectionAssignment()
    {
        var server = new NATSServer();
        var connection = new Moq.Mock<IConnection>();

        server.connection = connection.Object;

        Assert.NotNull(server.connection);
        Assert.Same(connection.Object, server.connection);
    }
}
