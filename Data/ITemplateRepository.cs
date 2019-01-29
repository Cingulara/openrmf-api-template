using openstig_template_api.Models;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace openstig_template_api.Data {
    public interface ITemplateRepository
    {
        Task<IEnumerable<Template>> GetAllTemplates();
        Task<Template> GetTemplate(string id);

        // query after multiple parameters
        Task<IEnumerable<Template>> GetTemplate(string bodyText, DateTime updatedFrom, long headerSizeLimit);

        // add new note document
        Task AddTemplate(Template item);

        // remove a single document
        Task<bool> RemoveTemplate(string id);

        // update just a single document
        Task<bool> UpdateTemplate(string id, Template body);

        // should be used with high cautious, only in relation with demo setup
        // Task<bool> RemoveAllArtifacts();
    }
}