using System.Collections.Generic;
using System.Threading.Tasks;

namespace FruitShop.Interfaces
{
    public interface ISearchService
    {
        Task IndexDocumentAsync<T>(string indexName, T document);
        Task IndexDocumentsAsync<T>(string indexName, IEnumerable<T> documents);
        Task DeleteDocumentAsync(string indexName, string id);
        Task DeleteDocumentsAsync(string indexName, IEnumerable<string> ids);
        Task<IEnumerable<string>> SearchIdsAsync(string indexName, string query, int limit = 20);
    }
}
