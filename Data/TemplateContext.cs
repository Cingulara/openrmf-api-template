using MongoDB.Driver;
using openstig_template_api.Models;
using Microsoft.Extensions.Options;

namespace openstig_template_api.Data
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