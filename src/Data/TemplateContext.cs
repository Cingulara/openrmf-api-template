using MongoDB.Driver;
using openrmf_templates_api.Models;
using Microsoft.Extensions.Options;

namespace openrmf_templates_api.Data
{
    public class TemplateContext
    {
        private readonly IMongoDatabase _database = null;

        public TemplateContext(IOptions<Settings> settings)
        {
            var client = new MongoClient(settings.Value.ConnectionString);
            if (client != null)
                _database = client.GetDatabase(settings.Value.Database);
        }

        public IMongoCollection<Template> Templates
        {
            get
            {
                return _database.GetCollection<Template>("Templates");
            }
        }
    }
}