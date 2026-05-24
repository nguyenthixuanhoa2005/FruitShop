using Meilisearch;
using FruitShop.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System;

namespace FruitShop.Services
{
    public class MeiliSearchService : ISearchService
    {
        private readonly MeilisearchClient _client;

        public MeiliSearchService(IConfiguration configuration)
        {
            var url = configuration["Meilisearch:Url"] ?? "http://localhost:7700";
            var apiKey = configuration["Meilisearch:ApiKey"];
            _client = new MeilisearchClient(url, apiKey);
        }

        public async Task IndexDocumentAsync<T>(string indexName, T document)
        {
            var index = _client.Index(indexName);
            await index.AddDocumentsAsync(new[] { document });
        }

        public async Task IndexDocumentsAsync<T>(string indexName, IEnumerable<T> documents)
        {
            var index = _client.Index(indexName);

            if (indexName == "products")
            {
                // 1. Chỉ tìm kiếm trong các trường nội dung, không đưa ID vào searchable
                await index.UpdateSearchableAttributesAsync(new[] { "name", "sku", "description", "categoryName", "supplierName", "origin" });

                // 2. Cấu hình Ranking Rules: Ưu tiên số từ khớp và ít lỗi nhất lên đầu
                await index.UpdateRankingRulesAsync(new[] { 
                    "words",       // Khớp nhiều từ nhất lên đầu (Dành cho kq giống hệt)
                    "typo",        // Ít lỗi chính tả nhất
                    "attribute",   // Khớp trong Name > Description
                    "proximity",   // Các từ đứng gần nhau
                    "exactness"    // Khớp chính xác tuyệt đối
                });
            }

            await index.AddDocumentsAsync(documents);
        }

        public async Task DeleteDocumentAsync(string indexName, string id)
        {
            var index = _client.Index(indexName);
            await index.DeleteOneDocumentAsync(id);
        }

        public async Task DeleteDocumentsAsync(string indexName, IEnumerable<string> ids)
        {
            var index = _client.Index(indexName);
            await index.DeleteDocumentsAsync(ids);
        }

        public async Task<IEnumerable<string>> SearchIdsAsync(string indexName, string query, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<string>();
            
            try 
            {
                var index = _client.Index(indexName);
                
                // Sử dụng MatchingStrategy.All để loại bỏ các kết quả ko khớp đủ từ khóa
                var searchResults = await index.SearchAsync<Dictionary<string, object>>(query, new SearchQuery 
                { 
                    Limit = limit,
                    MatchingStrategy = "all" 
                });
                
                return searchResults.Hits.Select(h => h["id"].ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
            }
            catch (Exception)
            {
                // Fallback về SQL search trong controller nếu Meilisearch lỗi
                return new List<string>();
            }
        }
    }
}
