using openrmf_templates_api.Models;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Options;

namespace openrmf_templates_api.Data {
    public class TemplateRepository : ITemplateRepository
    {
        private readonly TemplateContext _context = null;

        public TemplateRepository(IOptions<Settings> settings)
        {
            _context = new TemplateContext(settings);
        }

        public TemplateRepository(Settings settings)
        {
            _context = new TemplateContext(settings);
        }

        public async Task<IEnumerable<Template>> GetAllTemplates()
        {
            try
            {
                return await _context.Templates.Find(Template => Template.templateType == "USER" || Template.templateType == null)
                        .ToListAsync();
            }
            catch (Exception ex)
            {
                // log or manage the exception
                throw ex;
            }
        }
        private ObjectId GetInternalId(string id)
        {
            ObjectId internalId;
            if (!ObjectId.TryParse(id, out internalId))
                internalId = ObjectId.Empty;

            return internalId;
        }

        // query after Id or InternalId (BSonId value)
        //
        public async Task<Template> GetTemplate(string id)
        {
            try
            {
                return await _context.Templates.Find(Template => Template.InternalId == GetInternalId(id)).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                // log or manage the exception
                throw ex;
            }
        }

        // query after body text, updated time, and header image size
        //
        public async Task<IEnumerable<Template>> GetTemplate(string bodyText, DateTime updatedFrom, long headerSizeLimit)
        {
            try
            {
                var query = _context.Templates.Find(Template => Template.title.Contains(bodyText) &&
                                    Template.updatedOn >= updatedFrom);

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                // log or manage the exception
                throw ex;
            }
        }
        
        public async Task AddTemplate(Template item)
        {
            try
            {
                await _context.Templates.InsertOneAsync(item);
            }
            catch (Exception ex)
            {
                // log or manage the exception
                throw ex;
            }
        }

        public async Task<bool> RemoveTemplate(string id)
        {
            try
            {
                DeleteResult actionResult 
                    = await _context.Templates.DeleteOneAsync(
                        Builders<Template>.Filter.Eq("Id", id));

                return actionResult.IsAcknowledged 
                    && actionResult.DeletedCount > 0;
            }
            catch (Exception ex)
            {
                // log or manage the exception
                throw ex;
            }
        }

        public async Task<bool> RemoveSystemTemplates()
        {
            try
            {
                DeleteResult actionResult 
                    = await _context.Templates.DeleteManyAsync(Builders<Template>.Filter.Eq("templateType", "SYSTEM"));
                return actionResult.IsAcknowledged;
            }
            catch (Exception ex)
            {
                // log or manage the exception
                throw ex;
            }
        }

        public async Task<bool> UpdateTemplate(string id, Template body)
        {
            var filter = Builders<Template>.Filter.Eq(s => s.InternalId, GetInternalId(id));

            try
            {
                body.InternalId = GetInternalId(id);
                var actionResult = await _context.Templates.ReplaceOneAsync(filter, body);
                return actionResult.IsAcknowledged && actionResult.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                // log or manage the exception
                throw ex;
            }
        }
    }
}