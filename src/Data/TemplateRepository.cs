// Copyright (c) Cingulara LLC 2019 and Tutela LLC 2019. All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007 license. See LICENSE file in the project root for full license information.
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
        
        public async Task<Template> AddTemplate(Template item)
        {
            try
            {
                await _context.Templates.InsertOneAsync(item);
                return item;
            }
            catch (Exception ex)
            {
                // log or manage the exception
                throw ex;
            }
        }

        public async Task<bool> RemoveTemplate(string id)
        {
            var filter = Builders<Template>.Filter.Eq(s => s.InternalId, GetInternalId(id));
            try
            {
                Template art = new Template();
                art.InternalId = GetInternalId(id);
                // only save the data outside of the checklist, update the date
                var currentRecord = await _context.Templates.Find(t => t.InternalId == art.InternalId).FirstOrDefaultAsync();
                if (currentRecord != null){
                    DeleteResult actionResult = await _context.Templates.DeleteOneAsync(Builders<Template>.Filter.Eq("_id", art.InternalId));
                    return actionResult.IsAcknowledged && actionResult.DeletedCount > 0;
                } 
                else {
                    throw new KeyNotFoundException();
                }
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

        // check that the database is responding and it returns at least one collection name
        public bool HealthStatus(){
            var result = _context.Templates.Database.ListCollectionNamesAsync().GetAwaiter().GetResult().FirstOrDefault();
            if (!string.IsNullOrEmpty(result)) // we are good to go
                return true;
            return false;
        }

        // get the most recent Template record based on title, version, and release
        public async Task<Template> GetLatestTemplate(string title) {
            try
            {
                var query = _context.Templates.Find(Template => Template.title == title);
                return await query.SortByDescending(y => y.version).ThenByDescending(z => z.stigRelease).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                // log or manage the exception
                throw ex;
            }
        }

        public async Task<long> CountUserTemplates(){
            try {
                long result = await _context.Templates.CountDocumentsAsync(Builders<Template>.Filter.Eq("templateType", "USER"));
                return result;
            }
            catch (Exception ex)
            {
                // log or manage the exception
                throw ex;
            }
        }
        public async Task<long> CountSystemTemplates(){
            try {
                long result = await _context.Templates.CountDocumentsAsync(Builders<Template>.Filter.Eq("templateType", "SYSTEM"));
                return result;
            }
            catch (Exception ex)
            {
                // log or manage the exception
                throw ex;
            }
        }
    }
}