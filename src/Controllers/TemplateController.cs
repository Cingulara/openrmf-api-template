// Copyright (c) Cingulara LLC 2019 and Tutela LLC 2019. All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007 license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using openrmf_templates_api.Models;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using System.Xml;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using NATS.Client;
using Newtonsoft.Json;

using openrmf_templates_api.Data;
using openrmf_templates_api.Classes;

namespace openrmf_templates_api.Controllers
{
    [Route("/")]
    public class TemplateController : Controller
    {
	    private readonly ITemplateRepository _TemplateRepo;
        private readonly ILogger<TemplateController> _logger;
        private readonly IConnection _msgServer;

        public TemplateController(ITemplateRepository TemplateRepo, ILogger<TemplateController> logger, IOptions<NATSServer> msgServer)
        {
            _logger = logger;
            _TemplateRepo = TemplateRepo;
            _msgServer = msgServer.Value.connection;
        }

        /// <summary>
        /// POST Called from the OpenRMF UI (or external access) to add a new template via a POST if you 
        /// have the correct roles in your JWT.`
        /// </summary>
        /// <param name="checklistFile">The actual template CKL file uploaded</param>
        /// <param name="description">A description of the template</param>
        /// <returns>
        /// HTTP Status showing it was created or that there is an error.
        /// </returns>
        /// <response code="200">Returns the newly created item</response>
        /// <response code="400">If the item did not create correctly</response>     
        [HttpPost]
        [Authorize(Roles = "Administrator,Editor")]
        public async Task<IActionResult> UploadNewChecklist(IFormFile checklistFile, string description = "")
        {
            try {
                _logger.LogInformation("Calling UploadNewChecklist()");
                var name = checklistFile.FileName;
                string rawChecklist =  string.Empty;
                using (var reader = new StreamReader(checklistFile.OpenReadStream()))
                {
                    rawChecklist = reader.ReadToEnd();  
                }
                _logger.LogInformation("UploadNewChecklist() Making the template");
                Template t = MakeTemplateRecord(rawChecklist);
                if (t != null && !string.IsNullOrEmpty(description)) {
                    t.description = description;
                }
                
                // grab the user/system ID from the token if there which is *should* always be
                var claim = this.User.Claims.Where(x => x.Type == System.Security.Claims.ClaimTypes.NameIdentifier).FirstOrDefault();
                if (claim != null) { // get the value
                    t.createdBy = Guid.Parse(claim.Value);
                }
                _logger.LogInformation("UploadNewChecklist() template created, saving to the database");
                var record = await _TemplateRepo.AddTemplate(t);
                _logger.LogInformation("Called UploadNewChecklist() and added checklists successfully");
                if (record != null) 
                    record.rawChecklist = ""; // remove this we do not need it here
                    
                // publish an audit event
                _logger.LogInformation("UploadNewChecklist() publish an audit message on a new template {0}.", name);
                Audit newAudit = GenerateAuditMessage(claim, "upload new template");
                newAudit.message = string.Format("UploadNewChecklist() delete a single template {0}.", name);
                newAudit.url = string.Format("POST checklistFile={0}",name);
                _msgServer.Publish("openrmf.audit.save", Encoding.UTF8.GetBytes(Compression.CompressString(JsonConvert.SerializeObject(newAudit))));
                _msgServer.Flush();
                
                return Ok(record);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error uploading template checklist file");
                return BadRequest();
            }
        }

        /// <summary>
        /// PUT Called from the OpenRMF UI (or external access) to update a current template via a PUT if you 
        /// have the correct roles in your JWT.
        /// </summary>
        /// <param name="id">The ID of the record template to update</param>
        /// <param name="checklistFile">The actual template CKL file uploaded</param>
        /// <param name="description">A description of the template</param>
        /// <returns>
        /// HTTP Status showing it was updated or that there is an error.
        /// </returns>
        /// <response code="200">Returns the newly updated item</response>
        /// <response code="400">If the item did not update correctly</response>
        /// <response code="404">If the ID passed in is not valid</response>
        [HttpPut]
        [Authorize(Roles = "Administrator,Editor")]
        public async Task<IActionResult> UpdateChecklist(string id, IFormFile checklistFile, string description = "")
        {
            try {
                _logger.LogInformation("Calling UpdateChecklist({0})", id);
                var name = checklistFile.FileName;
                string rawChecklist =  string.Empty;
                using (var reader = new StreamReader(checklistFile.OpenReadStream()))
                {
                    rawChecklist = reader.ReadToEnd();  
                }
                _logger.LogInformation("UpdateChecklist() Getting the template record ready to update");
                Template newTemplate = MakeTemplateRecord(rawChecklist);
                Template oldTemplate = await _TemplateRepo.GetTemplate(id);

                // if this is empty, the ID passed is bad
                if (oldTemplate == null) {
                    _logger.LogWarning("UpdateChecklist() called with invalid artifact Id {0}", id);
                    return NotFound();
                }

                if (oldTemplate != null && oldTemplate.createdBy != Guid.Empty){
                    // this is an update of an older one, keep the createdBy intact
                    newTemplate.createdBy = oldTemplate.createdBy;
                }
                oldTemplate = null;
                _logger.LogInformation("UpdateChecklist() made new template record from old one");

                // grab the user/system ID from the token if there which is *should* always be
                var claim = this.User.Claims.Where(x => x.Type == System.Security.Claims.ClaimTypes.NameIdentifier).FirstOrDefault();
                if (claim != null) { // get the value
                    newTemplate.updatedBy = Guid.Parse(claim.Value);
                }
                
                _logger.LogInformation("UpdateChecklist() saving the updated template record");
                await _TemplateRepo.UpdateTemplate(id, newTemplate);
                _logger.LogInformation("Called UpdateChecklist({0}) successfully", id);

                return Ok();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Uploading updated Template Checklist file");
                return BadRequest();
            }
        }
                
        /// <summary>
        /// parses the text and system, generates the pieces, and returns the artifact to save
        /// </summary>
        /// <param name="rawChecklist">The raw XML string of the CKL template file</param>
        /// <returns>
        /// a Template Record for saving into the database
        /// </returns>
        private Template MakeTemplateRecord(string rawChecklist) {
            _logger.LogInformation("Calling MakeTemplateRecord()");
            Template newArtifact = new Template();
            newArtifact.created = DateTime.Now;
            newArtifact.updatedOn = DateTime.Now;
            newArtifact.rawChecklist = rawChecklist;

            // parse the checklist and get the data needed
            rawChecklist = rawChecklist.Replace("\n","").Replace("\t","");
            _logger.LogInformation("MakeTemplateRecord() loading the XML passed in");
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(rawChecklist);

            _logger.LogInformation("MakeTemplateRecord() getting the ASSET information");
            XmlNodeList assetList = xmlDoc.GetElementsByTagName("ASSET");
            // get the title and release which is a list of children of child nodes buried deeper
            _logger.LogInformation("MakeTemplateRecord() getting the STIG_INFO information");
            XmlNodeList stiginfoList = xmlDoc.GetElementsByTagName("STIG_INFO");
            foreach (XmlElement child in stiginfoList.Item(0).ChildNodes) {
                if (child.FirstChild.InnerText == "releaseinfo")
                    newArtifact.stigRelease = child.LastChild.InnerText;
                else if (child.FirstChild.InnerText == "title")
                    newArtifact.stigType = child.LastChild.InnerText;
                else if (child.FirstChild.InnerText == "version")
                    newArtifact.version = child.LastChild.InnerText;
            }

            _logger.LogInformation("MakeTemplateRecord() shortening names a bit to trim titles and such");
            // shorten the names a bit for USER templates
            if (newArtifact != null && !string.IsNullOrEmpty(newArtifact.stigType)){
                newArtifact.title = newArtifact.stigType;
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
            _logger.LogInformation("Called MakeTemplateRecord() successfully...returning template record");
            return newArtifact;
        }
    
        /// <summary>
        /// GET the user listing with Ids of the Checklist Templates, but without all the extra XML
        /// </summary>
        /// <returns>
        /// HTTP Status and the list of all Template records for non-SYSTEM templates
        /// </returns>
        /// <response code="200">Returns the list of all non system templates</response>
        /// <response code="400">If the search did not work correctly</response>
        [HttpGet]
        [Authorize(Roles = "Administrator,Reader,Editor,Assessor")]
        public async Task<IActionResult> ListTemplates()
        {
            try {
                _logger.LogInformation("Calling ListTemplates()");
                IEnumerable<Template> Templates;
                Templates = await _TemplateRepo.GetAllTemplates();
                _logger.LogInformation("Called ListTemplates() successfully");
                return Ok(Templates);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error listing all Templates and deserializing the checklist XML");
                return BadRequest();
            }
        }

        /// <summary>
        /// GET a specific template record based on the ID for viewing
        /// </summary>
        /// <param name="id">The id of the template database record</param>
        /// <returns>
        /// HTTP Status and the Template being requested. Or a 404.
        /// </returns>
        /// <response code="200">Returns the searched for template by ID</response>
        /// <response code="404">If the search did not work correctly</response>
        [HttpGet("{id}")]
        [Authorize(Roles = "Administrator,Reader,Editor,Assessor")]
        public async Task<IActionResult> GetTemplate(string id)
        {
            try {
                _logger.LogInformation("Calling GetTemplate({0})", id);
                Template template = new Template();
                template = await _TemplateRepo.GetTemplate(id);
                if (template == null) {
                    _logger.LogWarning("GetTemplate({0}) is not a valid ID", id);
                    return NotFound();
                }
                template.CHECKLIST = ChecklistLoader.LoadChecklist(template.rawChecklist);
                _logger.LogInformation("Called GetTemplate({0}) successfully", id);
                return Ok(template);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Retrieving Template");
                return BadRequest();
            }
        }
        
        /// <summary>
        /// GET a specific template record based on the ID to download into a CKL file
        /// </summary>
        /// <param name="id">The id of the template database record</param>
        /// <returns>
        /// HTTP Status and the Template being requested in CKL form. Or a 404.
        /// </returns>
        /// <response code="200">Returns the XML of the template for template by ID</response>
        /// <response code="404">If the search did not work correctly</response>
        [HttpGet("download/{id}")]
        [Authorize(Roles = "Administrator,Reader,Editor,Assessor")]
        public async Task<IActionResult> DownloadTemplate(string id)
        {
            try {
                _logger.LogInformation("Calling DownloadTemplate({0})", id);
                Template template = new Template();
                template = await _TemplateRepo.GetTemplate(id);
                if (template == null) {
                    _logger.LogWarning("DownloadTemplate({0}) is not a valid ID", id);
                    return NotFound();
                }
                _logger.LogInformation("Called DownloadTemplate({0}) successfully", id);
                return Ok(template.rawChecklist);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Retrieving Template for Download");
                return NotFound();
            }
        }

        /// <summary>
        /// DELETE Called from the OpenRMF UI (or external access) to delete a template by its ID.
        /// </summary>
        /// <param name="id">The ID of the template passed in</param>
        /// <returns>
        /// HTTP Status showing it was deleted or that there is an error.
        /// </returns>
        /// <response code="200">Returns success</response>
        /// <response code="400">If the item did not delete correctly</response>
        /// <response code="404">If the ID was not found</response>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator,Editor")]
        public async Task<IActionResult> DeleteTemplate(string id)
        {
            try {
                _logger.LogInformation("Calling DeleteTemplate({0})", id);
                Template template = _TemplateRepo.GetTemplate(id).Result;
                if (template != null) {
                    _logger.LogInformation("Deleting Template {0}", id);
                    var deleted = await _TemplateRepo.RemoveTemplate(id);
                    if (deleted)  {
                        var claim = this.User.Claims.Where(x => x.Type == System.Security.Claims.ClaimTypes.NameIdentifier).FirstOrDefault();         
                        // publish an audit event
                        _logger.LogInformation("DeleteTemplate() publish an audit message on a deleted template {0}.", id);
                        Audit newAudit = GenerateAuditMessage(claim, "delete template");
                        newAudit.message = string.Format("DeleteArtifact() delete a single template {0}.", id);
                        newAudit.url = string.Format("DELETE /{0}",id);
                        _msgServer.Publish("openrmf.audit.save", Encoding.UTF8.GetBytes(Compression.CompressString(JsonConvert.SerializeObject(newAudit))));
                        _msgServer.Flush();
                        return Ok();
                    }
                    else {
                        _logger.LogWarning("DeleteTemplate() Template id {0} not deleted correctly", id);
                        return NotFound();
                    }
                }
                else {
                    _logger.LogWarning("DeleteTemplate() Template id {0} not found", id);
                    return NotFound();
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "DeleteTemplate() Error Deleting Template {0}", id);
                return BadRequest();
            }
        }


        #region Dashboard APIs
        
        /// <summary>
        /// GET a count of the non-system templates for the dashboard number
        /// </summary>
        /// <returns>
        /// HTTP Status and the count of user templates
        /// </returns>
        /// <response code="200">Returns the number</response>
        /// <response code="404">If the count query did not work correctly</response>
        [HttpGet("count/templates")]
        [Authorize(Roles = "Administrator,Reader,Editor,Assessor")]
        public async Task<IActionResult> CountUserTemplates()
        {
            try {
                _logger.LogInformation("Calling CountUserTemplates()");
                long result = await _TemplateRepo.CountUserTemplates();
                _logger.LogInformation("Called CountUserTemplates()");
                return Ok(result);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Retrieving User Template Count in MongoDB");
                return NotFound();
            }
        }
        
        /// <summary>
        /// GET a count of the system templates for the dashboard number
        /// </summary>
        /// <returns>
        /// HTTP Status and the count of system templates
        /// </returns>
        /// <response code="200">Returns the number</response>
        /// <response code="404">If the count query did not work correctly</response>
        [HttpGet("count/systemtemplates")]
        [Authorize(Roles = "Administrator,Reader,Editor,Assessor")]
        public async Task<IActionResult> CountSystemTemplates()
        {
            try {
                _logger.LogInformation("Calling CountSystemTemplates()");
                long result = await _TemplateRepo.CountSystemTemplates();
                _logger.LogInformation("Called CountSystemTemplates()");
                return Ok(result);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Retrieving System Template Count in MongoDB");
                return NotFound();
            }
        }
        #endregion

        #region Private Functions
        private Audit GenerateAuditMessage(System.Security.Claims.Claim claim, string action) {
            Audit audit = new Audit();
            audit.program = "Template API";
            audit.created = DateTime.Now;
            audit.action = action;
            if (claim != null) {
            audit.userid = claim.Value;
            var fullname = claim.Subject.Claims.Where(x => x.Type == "name").FirstOrDefault();
            if (fullname != null) 
                audit.fullname = fullname.Value;
            var username = claim.Subject.Claims.Where(x => x.Type == "preferred_username").FirstOrDefault();
            if (username != null) 
                audit.username = username.Value;
            var useremail = claim.Subject.Claims.Where(x => x.Type.Contains("emailaddress")).FirstOrDefault();
            if (useremail != null) 
                audit.email = useremail.Value;
            }
            return audit;
        }

        #endregion
    }
}
