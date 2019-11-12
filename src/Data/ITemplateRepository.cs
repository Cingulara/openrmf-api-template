using openrmf_templates_api.Models;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace openrmf_templates_api.Data {
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

        // remove a single document
        Task<bool> RemoveSystemTemplates();

        // update just a single document
        Task<bool> UpdateTemplate(string id, Template body);

    }
}