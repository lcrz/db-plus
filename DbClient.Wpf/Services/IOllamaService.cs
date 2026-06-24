using System.Threading.Tasks;

namespace DbClient.Wpf.Services
{
    public class AiResponse
    {
        public string Text { get; set; }
        public double DurationSeconds { get; set; }
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
    }

    public interface IOllamaService
    {
        Task<AiResponse> GenerateSqlAsync(string prompt, string schemaContext);
        Task<AiResponse> GenerateResponseAsync(string systemPrompt, string userPrompt);
    }
}
