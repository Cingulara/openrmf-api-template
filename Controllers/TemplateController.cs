
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
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using openstig_template_api.Data;

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
        
    }
}
