using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace dotNet_Project.Services
{
    public class OpenAiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public OpenAiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<AiSuggestion> GenerateMetadata(string title, string description, List<string> availableCategories)
        {
            var apiKey = _configuration["OpenAi:ApiKey"];
            if (string.IsNullOrEmpty(apiKey)) return null;

            var categoriesString = string.Join(", ", availableCategories);

            var prompt = $@"
            Analyze this bookmark:
            Title: {title}
            Description: {description}

            Context:
            The user already has these categories: [{categoriesString}].

            Your Goal: 
            Suggest exactly 2-3 tags and 2-3 categories.

            Rules:
            1. PRIORITY: Check the user's existing categories list first. If relevant, USE THEM.
            2. If existing categories don't fit, invent NEW generic ones (e.g., 'Coding', 'Travel').
            3. TAGS: Generate 3 relevant keywords as tags.

            IMPORTANT: Return ONLY valid JSON. Do not add explanations. Use this structure:
            {{
                ""tags"": [""tag1"", ""tag2"", ""tag3""],
                ""categories"": [""ExistingCategoryMatch"", ""NewGeneratedCategory""]
            }}";

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = "You are a JSON-only generator." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.3
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode) return null;

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var openAiResponse = JsonSerializer.Deserialize<OpenAiResponseRoot>(jsonResponse);
            var rawContent = openAiResponse?.choices?.FirstOrDefault()?.message?.content;

            if (string.IsNullOrEmpty(rawContent)) return null;

            // FOLOSIM METODA DE CURATARE AVANSATA
            rawContent = ExtractJson(rawContent);

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<AiSuggestion>(rawContent, options);
            }
            catch
            {
                return null;
            }
        }

        // Metoda care extrage doar JSON-ul dintre acolade
        private string ExtractJson(string content)
        {
            if (string.IsNullOrEmpty(content)) return content;

            int firstBrace = content.IndexOf('{');
            int lastBrace = content.LastIndexOf('}');

            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return content.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            return content;
        }
    }

    // Am schimbat Tags in List<string> pentru a fi mai sigur la deserializare
    public class AiSuggestion
    {
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new List<string>();
    }

    internal class OpenAiResponseRoot
    {
        public Choice[] choices { get; set; }
    }
    internal class Choice
    {
        public Message message { get; set; }
    }
    internal class Message
    {
        public string content { get; set; }
    }
}