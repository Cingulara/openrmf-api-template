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
        [Authorize(Roles = "Administrator")]
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
                _logger.LogError(ex, "UploadNewChecklist() Error uploading template checklist file");
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
        [Authorize(Roles = "Administrator")]
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
                _logger.LogError(ex, "UpdateChecklist({0}) Error Uploading updated Template Checklist file", id);
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
            rawChecklist = rawChecklist.Replace("\t","");
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
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
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
                _logger.LogError(ex, "ListTemplates() Error listing all Templates and deserializing the checklist XML");
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
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new [] {"id"})]
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
                _logger.LogError(ex, "GetLatestTemplate({0}) Error Retrieving Template", id);
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
                _logger.LogError(ex, "DownloadTemplate({0}) Error Retrieving Template for Download", id);
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
        [Authorize(Roles = "Administrator")]
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

        /// <summary>
        /// See if a specific checklist version/release has an update
        /// </summary>
        /// <param name="systemGroupId">The id of the system record for this checklist</param>
        /// <param name="artifactId">The id of the checklist</param>
        /// <returns>
        /// HTTP Status and the Template information being requested (Template record). Or a 404.
        /// </returns>
        /// <response code="200">Returns the searched for template information</response>
        /// <response code="404">If the search did not work correctly</response>
        [HttpGet("checklistupdate/system/{systemGroupId}/artifact/{artifactId}")]
        [Authorize(Roles = "Administrator,Reader,Editor,Assessor")]
        //[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new [] {"systemGroupId", "artifactId"})]
        public async Task<IActionResult> GetLatestTemplate(string systemGroupId, string artifactId)
        {
            try {
                _logger.LogInformation("Calling GetLatestTemplate({0}, {1})", systemGroupId, artifactId);
                Artifact art = NATSClient.GetCurrentChecklist(systemGroupId, artifactId);
                if (art != null) {
                    Template template = new Template();
                    string stigType = "";
                    SI_DATA data = art.CHECKLIST.STIGS.iSTIG.STIG_INFO.SI_DATA.Where(x => x.SID_NAME == "title").FirstOrDefault();
                    if (data != null) {
                        // get the artifact checklists's actual checklist type from DISA
                        stigType = data.SID_DATA;
                        template = await _TemplateRepo.GetLatestTemplate(stigType);
                        if (template == null) {
                            _logger.LogWarning("GetLatestTemplate({0}, {1}) is not a valid ID", systemGroupId, artifactId);
                            return NotFound();
                        }
                        
                        // now let's compare versions and releases and see if there is a new one
                        if (Convert.ToInt16(template.version) > Convert.ToInt16(art.version)) {
                            _logger.LogInformation("Called GetLatestTemplate({0}, {1}) successfully and returned a new template version {2}", systemGroupId, artifactId, art.version);
                            // new version, send it
                            return Ok(template);
                        }
                        if (Convert.ToInt16(template.version) == Convert.ToInt16(art.version)) {
                            // same version, let's check the release
                            // checklist Release = Rxx where xx is a number like "R18 dated 25 Oct 2019"
                            // template release is the full text "Release: 6 Benchmark Date: 24 Jan 2020" 
                            // so we need to remove Release:, trim it, then take the first "word" up to the space
                            // convert to INT if possible, and then test the template release > checklist release
                            int artifactRelease = GetReleaseValue(art.stigRelease);
                            int templateRelease = GetReleaseValue(template.stigRelease);
                            if (templateRelease > artifactRelease) {
                                _logger.LogInformation("Called GetLatestTemplate({0}, {1}) successfully and returned a new template release {2}", systemGroupId, artifactId, template.stigRelease);
                                return Ok(template);
                            } else {
                                _logger.LogInformation("Called GetLatestTemplate({0}, {1}) successfully and already at the correct version {2} and release {3}", systemGroupId, artifactId, art.version, art.stigRelease);
                                return NotFound("No New Template");
                            }
                        }
                        else {
                            // this is not valid, return not found
                            _logger.LogWarning("Called GetLatestTemplate({0}, {1}) with an odd version and release", systemGroupId, artifactId);
                            return NotFound("The checklist passed in had an Invalid Version and/or Release");
                        }
                    } else {
                        _logger.LogWarning("Called GetLatestTemplate({0}, {1}) with an invalid systemId or artifactId checklist type", systemGroupId, artifactId);
                        return BadRequest("The checklist passed in had an Invalid System Artifact/Checklist Type");
                    }
                } else {
                    _logger.LogWarning("Called GetLatestTemplate({0}, {1}) with an invalid systemId or artifactId", systemGroupId, artifactId);
                    return BadRequest("The checklist passed in had an Invalid System Artifact/Checklist");
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "GetLatestTemplate({0}, {1}) Error Retrieving Latest Template", systemGroupId, artifactId);
                return BadRequest();
            }
        }
        
        private short GetReleaseValue(string release) {
            short rel = 0;
            string value = release.Replace("Release: ",""); // remove this for templates
            if (value.Substring(0,1) == "R") {// this is for checklists or for R12 kind of designations for release
                value = value.Substring(1); // keep the rest
            }
            // now that the beginning "release" labels are gone let's see what number is left
            int spacing = value.IndexOf(" ",0); // where is the first space
            value = value.Substring(0,spacing); // this should be the release number
            Int16.TryParse(value, out rel);
            return rel;
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
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> CountTemplates()
        {
            try {
                _logger.LogInformation("Calling CountTemplates()");
                long result = await _TemplateRepo.CountTemplates();
                _logger.LogInformation("Called CountTemplates()");
                return Ok(result);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Retrieving Template Count in MongoDB");
                return NotFound();
            }
        }
        
        /// <summary>
        /// GET a count of the non-system templates for the dashboard number
        /// </summary>
        /// <returns>
        /// HTTP Status and the count of user templates
        /// </returns>
        /// <response code="200">Returns the number</response>
        /// <response code="404">If the count query did not work correctly</response>
        [HttpGet("count/usertemplates")]
        [Authorize(Roles = "Administrator,Reader,Editor,Assessor")]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
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
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
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
