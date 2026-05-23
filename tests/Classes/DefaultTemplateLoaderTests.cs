using System.Reflection;
using openrmf_templates_api.Models;
using tests.TestData;
using Xunit;

namespace tests.Classes;

public class DefaultTemplateLoaderTests
{
    [Fact]
    public void MakeTemplateSystemRecord_CreatesSystemTemplate()
    {
        var method = GetFactoryMethod();

        var result = method.Invoke(null, new object[] { XmlSamples.Xccdf, "sample-manual-xccdf.xml" });

        var template = Assert.IsType<Template>(result);
        Assert.Equal("SYSTEM", template.templateType);
        Assert.Equal("sample-manual-xccdf.xml", template.filename);
        Assert.Equal("Windows Security Technical Implementation Guide", template.stigType);
        Assert.NotNull(template.rawChecklist);
        Assert.Equal("11111111-1111-1111-1111-111111111111", template.createdBy.ToString());
    }

    [Fact]
    public void MakeTemplateSystemRecord_ThrowsOnInvalidXml()
    {
        var method = GetFactoryMethod();

        var ex = Record.Exception(() => method.Invoke(null, new object[] { "<bad>", "x.xml" }));

        Assert.NotNull(ex);
        Assert.IsType<TargetInvocationException>(ex);
    }

    private static MethodInfo GetFactoryMethod()
    {
        var type = typeof(openrmf_templates_api.Classes.DefaultTemplateLoader);
        var method = type.GetMethod("MakeTemplateSystemRecord", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!;
    }
}
