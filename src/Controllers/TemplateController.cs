using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using openrmf_templates_api.Models;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

using openrmf_templates_api.Data;
using openrmf_templates_api.Classes;

namespace openrmf_templates_api.Controllers
{
    [Route("/")]
    public class TemplateController : Controller
    {
	    private readonly ITemplateRepository _TemplateRepo;
        private readonly ILogger<TemplateController> _logger;

        public TemplateController(ITemplateRepository TemplateRepo, ILogger<TemplateController> logger)
        {
            _logger = logger;
            _TemplateRepo = TemplateRepo;
        }

        // POST as new
        [HttpPost]
        [Authorize(Roles = "Administrator,Editor")]
        public async Task<IActionResult> UploadNewChecklist(IFormFile checklistFile, string description = "")
        {
            try {
                var name = checklistFile.FileName;
                string rawChecklist =  string.Empty;
                using (var reader = new StreamReader(checklistFile.OpenReadStream()))
                {
                    rawChecklist = reader.ReadToEnd();  
                }
                Template t = MakeTemplateRecord(rawChecklist);
                
                // grab the user/system ID from the token if there which is *should* always be
                var claim = this.User.Claims.Where(x => x.Type == System.Security.Claims.ClaimTypes.NameIdentifier).FirstOrDefault();
                if (claim != null) { // get the value
                    t.createdBy = Guid.Parse(claim.Value);
                }
                await _TemplateRepo.AddTemplate(t);

                return Ok();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error uploading template checklist file");
                return BadRequest();
            }
        }

        // PUT as update
        [HttpPut]
        [Authorize(Roles = "Administrator,Editor")]
        public async Task<IActionResult> UpdateChecklist(string id, IFormFile checklistFile, string description = "")
        {
            try {
                var name = checklistFile.FileName;
                string rawChecklist =  string.Empty;
                using (var reader = new StreamReader(checklistFile.OpenReadStream()))
                {
                    rawChecklist = reader.ReadToEnd();  
                }
                Template newTemplate = MakeTemplateRecord(rawChecklist);
                Template oldTemplate = await _TemplateRepo.GetTemplate(id);
                
                if (oldTemplate != null && oldTemplate.createdBy != Guid.Empty){
                    // this is an update of an older one, keep the createdBy intact
                    newTemplate.createdBy = oldTemplate.createdBy;
                }
                
                oldTemplate = null;

                // grab the user/system ID from the token if there which is *should* always be
                var claim = this.User.Claims.Where(x => x.Type == System.Security.Claims.ClaimTypes.NameIdentifier).FirstOrDefault();
                if (claim != null) { // get the value
                    newTemplate.updatedBy = Guid.Parse(claim.Value);
                }
                
                await _TemplateRepo.UpdateTemplate(id, newTemplate);

                return Ok();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Uploading updated Template Checklist file");
                return BadRequest();
            }
        }
        
        
      // this parses the text and system, generates the pieces, and returns the artifact to save
      private Template MakeTemplateRecord(string rawChecklist) {
            Template newArtifact = new Template();
            newArtifact.created = DateTime.Now;
            newArtifact.updatedOn = DateTime.Now;
            newArtifact.rawChecklist = rawChecklist;

            // parse the checklist and get the data needed
            rawChecklist = rawChecklist.Replace("\n","").Replace("\t","");
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(rawChecklist);

            XmlNodeList assetList = xmlDoc.GetElementsByTagName("ASSET");
            // get the title and release which is a list of children of child nodes buried deeper :face-palm-emoji:
            XmlNodeList stiginfoList = xmlDoc.GetElementsByTagName("STIG_INFO");
            foreach (XmlElement child in stiginfoList.Item(0).ChildNodes) {
            if (child.FirstChild.InnerText == "releaseinfo")
                newArtifact.stigRelease = child.LastChild.InnerText;
            else if (child.FirstChild.InnerText == "title")
                newArtifact.stigType = child.LastChild.InnerText;
            }

            // shorten the names a bit
            if (newArtifact != null && !string.IsNullOrEmpty(newArtifact.stigType)){
            newArtifact.stigType = newArtifact.stigType.Replace("Security Technical Implementation Guide", "STIG");
            newArtifact.stigType = newArtifact.stigType.Replace("Windows", "WIN");
            newArtifact.stigType = newArtifact.stigType.Replace("Application Security and Development", "ASD");
            newArtifact.stigType = newArtifact.stigType.Replace("Microsoft Internet Explorer", "MSIE");
            newArtifact.stigType = newArtifact.stigType.Replace("Red Hat Enterprise Linux", "REL");
            newArtifact.stigType = newArtifact.stigType.Replace("MS SQL Server", "MSSQL");
            newArtifact.stigType = newArtifact.stigType.Replace("Server", "SVR");
            newArtifact.stigType = newArtifact.stigType.Replace("Workstation", "WRK");
            }
            if (newArtifact != null && !string.IsNullOrEmpty(newArtifact.stigRelease)) {
            newArtifact.stigRelease = newArtifact.stigRelease.Replace("Release: ", "R"); // i.e. R11, R2 for the release number
            newArtifact.stigRelease = newArtifact.stigRelease.Replace("Benchmark Date:","dated");
            }
            return newArtifact;
        }
    
        // GET the listing with Ids of the Checklist Templates, but without all the extra XML
        [HttpGet]
        [Authorize(Roles = "Administrator,Reader,Editor,Assessor")]
        public async Task<IActionResult> ListTemplates()
        {
            try {
                IEnumerable<Template> Templates;
                Templates = await _TemplateRepo.GetAllTemplates();
                return Ok(Templates);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error listing all Templates and deserializing the checklist XML");
                return BadRequest();
            }
        }

        // GET /value
        [HttpGet("{id}")]
        [Authorize(Roles = "Administrator,Reader,Editor,Assessor")]
        public async Task<IActionResult> GetTemplate(string id)
        {
            try {
                Template template = new Template();
                template = await _TemplateRepo.GetTemplate(id);
                template.CHECKLIST = ChecklistLoader.LoadChecklist(template.rawChecklist);
                return Ok(template);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Retrieving Template");
                return NotFound();
            }
        }
        
        // GET /value
        [HttpGet("download/{id}")]
        [Authorize(Roles = "Administrator,Reader,Editor,Assessor")]
        public async Task<IActionResult> DownloadChecklist(string id)
        {
            try {
                Template template = new Template();
                template = await _TemplateRepo.GetTemplate(id);
                return Ok(template.rawChecklist);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Retrieving Template for Download");
                return NotFound();
            }
        }        
    }
}
