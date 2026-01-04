// ClassmateQRapi/Services/AIService.cs
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace ClassmateQRapi.Services
{
    public interface IAIService
    {
        Task<List<AIQuestionResponse>> GenerateQuestionsAsync(string subject, string difficulty, int numberOfQuestions);
    }

    public class AIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public AIService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;

            // Lấy URL từ appsettings.json
            var aiApiUrl = _configuration["AIService:BaseUrl"] ?? "http://127.0.0.1:8000";
            _httpClient.BaseAddress = new Uri(aiApiUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<AIQuestionResponse>> GenerateQuestionsAsync(string subject, string difficulty, int numberOfQuestions)
        {
            try
            {
                var request = new
                {
                    subject,
                    difficulty,
                    number_of_question = numberOfQuestions
                };

                var response = await _httpClient.PostAsJsonAsync("/llm/mcqa/generate", request);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"AI Service returned {response.StatusCode}");
                }

                var questions = await response.Content.ReadFromJsonAsync<List<AIQuestionResponse>>();
                return questions ?? new List<AIQuestionResponse>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to generate questions from AI: {ex.Message}", ex);
            }
        }
    }

    public class AIQuestionResponse
    {
        public string Question { get; set; } = null!;
        public string A { get; set; } = null!;
        public string B { get; set; } = null!;
        public string C { get; set; } = null!;
        public string D { get; set; } = null!;
        public int CorrectAnswer { get; set; }
    }
}