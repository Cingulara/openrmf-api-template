// Copyright (c) Cingulara LLC 2019 and Tutela LLC 2019. All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007 license. See LICENSE file in the project root for full license information.
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
        Task<Template> AddTemplate(Template item);

        // remove a single document
        Task<bool> RemoveTemplate(string id);

        // remove a single document
        Task<bool> RemoveSystemTemplates();

        // update just a single document
        Task<bool> UpdateTemplate(string id, Template body);

        // see what the most recent Template is by version and release
        Task<Template> GetLatestTemplate(string title);


        /******************************************** 
         Dashboard specific calls
        ********************************************/
        // get the # of checklists for the dashboard listing
        Task<long> CountUserTemplates();

        Task<long> CountSystemTemplates();
    }
}