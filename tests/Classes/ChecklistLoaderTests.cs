using System.Linq;
using System.Xml;
using openrmf_templates_api.Classes;
using tests.TestData;
using Xunit;

namespace tests.Classes;

public class ChecklistLoaderTests
{
    [Fact]
    public void LoadChecklist_ParsesAssetInfoAndVulnerabilities()
    {
        var checklist = ChecklistLoader.LoadChecklist(XmlSamples.Checklist);

        Assert.NotNull(checklist);
        Assert.Equal("host1", checklist.ASSET.HOST_NAME);
        Assert.Equal("Microsoft Windows Security Technical Implementation Guide",
            checklist.STIGS.iSTIG.STIG_INFO.SI_DATA.First(x => x.SID_NAME == "title").SID_DATA);
        Assert.Single(checklist.STIGS.iSTIG.VULN);
        Assert.Equal("Not_Reviewed", checklist.STIGS.iSTIG.VULN[0].STATUS);
    }

    [Fact]
    public void LoadChecklist_ThrowsXmlException_WhenXmlInvalid()
    {
        var ex = Record.Exception(() => ChecklistLoader.LoadChecklist("<CHECKLIST><ASSET>"));

        Assert.NotNull(ex);
        Assert.IsType<XmlException>(ex);
    }
}
