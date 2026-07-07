using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AppServicios.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AppServicios.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class AiController : ControllerBase
    {
        private const string SystemPrompt = """
            Eres ASI, asistente de SERVILABS/AppServicios para Latinoamerica.
            Tu tarea es ayudar a clientes, profesionales, cuadrillas y trabajadores a usar la app.
            Responde en español claro, cálido y breve.
            Prioriza inclusión laboral, oficios, servicios reales, seguridad entre partes y pasos accionables.
            No prometas resultados garantizados fuera de la app.
            Si hablan de servicios funerarios, responde con tono sobrio, respetuoso y práctico.
            Si preguntan por pagos, explica Plan Fundadores Pro: alta 2500 ARS, mensualidad 2500 ARS por 3 meses, comisión de pago protegido 2%.
            Si quieren inscribirse, guíalos a registrarse como Cliente, Profesional o Empresa/Cuadrilla según el caso.
            """;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AiController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpPost("chat")]
        public async Task<ActionResult<AiChatResponseDto>> Chat([FromBody] AiChatRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var apiKey = _configuration["OpenAI:ApiKey"] ?? _configuration["OPENAI_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Ok(new AiChatResponseDto(BuildLocalResponse(request.Message), false, "local"));
            }

            var model = _configuration["OpenAI:Model"] ?? "gpt-4.1-mini";
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                model,
                input = new object[]
                {
                    new
                    {
                        role = "system",
                        content = new[] { new { type = "input_text", text = SystemPrompt } }
                    },
                    new
                    {
                        role = "user",
                        content = new[] { new { type = "input_text", text = request.Message.Trim() } }
                    }
                },
                max_output_tokens = 350
            };

            using var response = await client.PostAsync(
                "https://api.openai.com/v1/responses",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return Ok(new AiChatResponseDto(BuildLocalResponse(request.Message), false, $"openai-{(int)response.StatusCode}"));
            }

            var text = ExtractResponseText(content);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = BuildLocalResponse(request.Message);
                return Ok(new AiChatResponseDto(text, false, "local-empty-openai-response"));
            }

            return Ok(new AiChatResponseDto(text.Trim(), true, "openai"));
        }

        private static string ExtractResponseText(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
            {
                return outputText.GetString() ?? string.Empty;
            }

            if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    {
                        parts.Add(text.GetString() ?? string.Empty);
                    }
                }
            }

            return string.Join("\n", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private static string BuildLocalResponse(string message)
        {
            var normalized = message.ToLowerInvariant();
            if (normalized.Contains("funer") || normalized.Contains("sepelio") || normalized.Contains("duelo"))
            {
                return "Podemos ayudarte con servicios funerarios desde un lugar respetuoso: traslado, sala velatoria, sepelio, cremacion, gestiones y acompanamiento. Indica tu ciudad y la urgencia para orientar la solicitud.";
            }

            if (normalized.Contains("inscrib") || normalized.Contains("registr") || normalized.Contains("trabaj") || normalized.Contains("ofrecer"))
            {
                return "Para sumarte, elige Registrarme y crea tu perfil como Profesional o Empresa/Cuadrilla. El Plan Fundadores Pro tiene alta de 2500 ARS y mensualidad de 2500 ARS por 3 meses para los primeros 100.";
            }

            if (normalized.Contains("pago") || normalized.Contains("precio") || normalized.Contains("comision"))
            {
                return "El alta del Plan Fundadores Pro es de 2500 ARS, con mensualidad de 2500 ARS por 3 meses. Para pagos protegidos por servicios, la comision prevista es del 2%.";
            }

            return "Estoy para ayudarte a encontrar servicios, registrarte, publicar una solicitud o armar tu perfil profesional. Cuéntame qué necesitas y te guío paso a paso.";
        }
    }
}
