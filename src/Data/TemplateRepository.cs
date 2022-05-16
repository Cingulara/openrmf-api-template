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
            return await _context.Templates.Find(_ => true).ToListAsync();
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
            return await _context.Templates.Find(Template => Template.InternalId == GetInternalId(id)).FirstOrDefaultAsync();
        }

        // query after body text, updated time, and header image size
        //
        public async Task<IEnumerable<Template>> GetTemplate(string bodyText, DateTime updatedFrom, long headerSizeLimit)
        {
            var query = _context.Templates.Find(Template => Template.title.Contains(bodyText) &&
                                Template.updatedOn >= updatedFrom);

            return await query.ToListAsync();
        }
        
        public async Task<Template> AddTemplate(Template item)
        {
            await _context.Templates.InsertOneAsync(item);
            return item;
        }

        public async Task<bool> RemoveTemplate(string id)
        {
            var filter = Builders<Template>.Filter.Eq(s => s.InternalId, GetInternalId(id));
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

        public async Task<bool> RemoveSystemTemplates()
        {
            DeleteResult actionResult 
                = await _context.Templates.DeleteManyAsync(Builders<Template>.Filter.Ne("templateType", "USER"));
            return actionResult.IsAcknowledged;
        }

        public async Task<bool> UpdateTemplate(string id, Template body)
        {
            var filter = Builders<Template>.Filter.Eq(s => s.InternalId, GetInternalId(id));
            body.InternalId = GetInternalId(id);
            var actionResult = await _context.Templates.ReplaceOneAsync(filter, body);
            return actionResult.IsAcknowledged && actionResult.ModifiedCount > 0;
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
            var query = _context.Templates.Find(Template => Template.title == title);
            return await query.SortByDescending(y => y.version).ThenByDescending(z => z.stigRelease).FirstOrDefaultAsync();
        }

        public async Task<long> CountTemplates(){
            long result = await _context.Templates.CountDocumentsAsync(Builders<Template>.Filter.Ne("templateType", ""));
            return result;
        }

        public async Task<long> CountUserTemplates(){
            long result = await _context.Templates.CountDocumentsAsync(Builders<Template>.Filter.Eq("templateType", "USER"));
            return result;
        }

        public async Task<long> CountSystemTemplates(){
            long result = await _context.Templates.CountDocumentsAsync(Builders<Template>.Filter.Eq("templateType", "SYSTEM"));
            return result;
        }
    }
}