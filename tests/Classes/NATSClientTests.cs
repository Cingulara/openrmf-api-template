using System;
using openrmf_templates_api.Classes;
using Xunit;

namespace tests.Classes;

public class NATSClientTests
{
    [Fact]
    public void GetCurrentChecklist_Throws_WhenNatsUrlIsInvalid()
    {
        var previous = Environment.GetEnvironmentVariable("NATSSERVERURL");

        try
        {
            Environment.SetEnvironmentVariable("NATSSERVERURL", "not-a-valid-nats-url");

            var ex = Record.Exception(() => NATSClient.GetCurrentChecklist("sys", "art"));

            Assert.NotNull(ex);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NATSSERVERURL", previous);
        }
    }

    [Fact]
    public void NATSClient_Type_IsAvailableForCallers()
    {
        Assert.NotNull(typeof(NATSClient));
        Assert.NotNull(typeof(NATSClient).GetMethod("GetCurrentChecklist"));
    }
}
