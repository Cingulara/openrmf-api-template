
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using openstig_template_api.Models;
using System.IO;
using System.Text;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using System.Xml.Serialization;
using System.Xml;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using openstig_template_api.Data;
using openstig_template_api.Classes;

namespace openstig_template_api.Controllers
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
        public async Task<IActionResult> UploadNewChecklist(IFormFile checklistFile, STIGtype checklistType, string title = "New Uploaded Template Checklist", string description = "")
        {
            try {
                var name = checklistFile.FileName;
                string rawChecklist =  string.Empty;
                using (var reader = new StreamReader(checklistFile.OpenReadStream()))
                {
                    rawChecklist = reader.ReadToEnd();  
                }
                await _TemplateRepo.AddTemplate(new Template () {
                    title = title,
                    description = description + "\n\nUploaded filename: " + name,
                    created = DateTime.Now,
                    updatedOn = DateTime.Now,
                    type = checklistType,
                    rawChecklist = rawChecklist
                });

                return Ok();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error uploading template checklist file");
                return BadRequest();
            }
        }

        // PUT as update
        [HttpPut]
        public async Task<IActionResult> UpdateChecklist(string id, IFormFile checklistFile, STIGtype checklistType, string title = "New Uploaded Template Checklist", string description = "")
        {
            try {

                var name = checklistFile.FileName;
                string rawChecklist =  string.Empty;
                using (var reader = new StreamReader(checklistFile.OpenReadStream()))
                {
                    rawChecklist = reader.ReadToEnd();  
                }
                await _TemplateRepo.UpdateTemplate(id, new Template () {
                    updatedOn = DateTime.Now,
                    title = title,
                    description = description,
                    type = checklistType,
                    rawChecklist = rawChecklist
                });

                return Ok();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Uploading updated Template Checklist file");
                return BadRequest();
            }
        }
        
        // GET the listing with Ids of the Checklist Templates, but without all the extra XML
        [HttpGet]
        public async Task<IActionResult> ListTemplates()
        {
            try {
                IEnumerable<Template> Templates;
                Templates = await _TemplateRepo.GetAllTemplates();
                foreach (Template a in Templates) {
                    a.rawChecklist = string.Empty;
                }
                return Ok(Templates);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error listing all Templates and deserializing the checklist XML");
                return BadRequest();
            }
        }

        // GET /value
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTemplate(string id)
        {
            try {
                Template template = new Template();
                template = await _TemplateRepo.GetTemplate(id);
                template.CHECKLIST = ChecklistLoader.LoadChecklist(template.rawChecklist);
                template.rawChecklist = string.Empty;
                return Ok(template);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Retrieving Template");
                return NotFound();
            }
        }
        
        // GET /value
        [HttpGet("download/{id}")]
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
