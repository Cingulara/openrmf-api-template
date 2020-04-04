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
            Assert.True(t.templateType == "USER");
            Assert.True(t.CHECKLIST != null);
            Assert.False(t.updatedOn.HasValue);
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
            t.title = "This is my Google Chrome title";
            t.description = "This is my description";
            t.updatedOn = DateTime.Now;
            t.templateType = "SYSTEM";
            t.version = "2";
            t.filename = "This-is-my-checklist-manual-xccdf.xml";
            t.stigDate = DateTime.Now.ToShortDateString();
            t.stigId = "myId";

            // test things out
            Assert.True(t != null);
            Assert.True (!string.IsNullOrEmpty(t.created.ToShortDateString()));
            Assert.True (!string.IsNullOrEmpty(t.stigType));
            Assert.True (!string.IsNullOrEmpty(t.stigRelease));
            Assert.True (!string.IsNullOrEmpty(t.rawChecklist));
            Assert.True (!string.IsNullOrEmpty(t.title));
            Assert.True (!string.IsNullOrEmpty(t.description));
            Assert.True (!string.IsNullOrEmpty(t.templateType));
            Assert.True (!string.IsNullOrEmpty(t.version));
            Assert.True (!string.IsNullOrEmpty(t.filename));
            Assert.True (!string.IsNullOrEmpty(t.stigDate));
            Assert.True (!string.IsNullOrEmpty(t.stigId));
            Assert.True (t.updatedOn.HasValue);
            Assert.True (!string.IsNullOrEmpty(t.updatedOn.Value.ToShortDateString()));
            Assert.True (t.CHECKLIST != null);
        }
    }
}
