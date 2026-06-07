using CallCenterAssistant.Models.Request;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace CallCenterAssistant.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _model;
        private readonly IValidator<ChatRequest> _validator;

        public ChatController(IConfiguration configuration, HttpClient httpClient, IValidator<ChatRequest> validator)
        {
            _httpClient = httpClient;
            _validator = validator;
            
            var section = configuration.GetSection("Ollama");
            _baseUrl = section["BaseUrl"] ?? "http://localhost:11434";
            _model = section["Model"] ?? "llama3";

            if (_baseUrl.Contains("localhost") && Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
            {
                _baseUrl = _baseUrl.Replace("localhost", "host.docker.internal");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest request)
        {
            var validationResult = await _validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
            }

            string url = $"{_baseUrl.TrimEnd('/')}/api/generate";

            var ollamaRequest = new
            {
                model = _model,
                system = "Sen Türkçe konuşan profesyonel bir çağrı merkezi müşteri temsilcisisin." +
                " Lütfen her zaman sadece Türkçe yanıt ver. Kesinlikle İngilizce veya başka bir dilde cevap verme." +
                " Yanıtların kısa ve net olsun.",
                prompt = request.Message,
                stream = false
            };

            var jsonPayload = JsonSerializer.Serialize(ollamaRequest);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, $"Ollama API hatası: {response.ReasonPhrase}");
                }

                var responseString = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseString);
                var botResponse = doc.RootElement.GetProperty("response").GetString();

                return Ok(new { response = botResponse?.Trim() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Sunucu hatası: {ex.Message}. Lütfen Ollama servisinin ayakta olduğundan emin olun.");
            }
        }
    }
}
