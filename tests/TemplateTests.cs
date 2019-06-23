using Xunit;
using openrmf_templates_api.Models;
using System;

namespace tests.Models
{
    public class ArtifactTests
    {
        [Fact]
        public void Test_NewTemplateIsValid()
        {
            Template t = new Template();
            Assert.True(t != null);
        }
    
        [Fact]
        public void Test_TemplateWithDataIsValid()
        {
            Template t = new Template();
            t.created = DateTime.Now;
            t.updatedOn = DateTime.Now;
            t.stigType = "Google Chrome";
            t.stigRelease = "Version 1";
            t.rawChecklist = "<XML>";
            t.updatedOn = DateTime.Now;

            // test things out
            Assert.True(t != null);
            Assert.True (!string.IsNullOrEmpty(t.created.ToShortDateString()));
            Assert.True (!string.IsNullOrEmpty(t.stigType));
            Assert.True (!string.IsNullOrEmpty(t.stigRelease));
            Assert.True (!string.IsNullOrEmpty(t.rawChecklist));
            Assert.True (!string.IsNullOrEmpty(t.title));  // readonly from other fields
            Assert.True (t.updatedOn.HasValue);
            Assert.True (!string.IsNullOrEmpty(t.updatedOn.Value.ToShortDateString()));
            Assert.True (t.CHECKLIST != null);
        }
    }
}
