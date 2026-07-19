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

        [HttpPost("cosa-del-cosito")]
        [RequestSizeLimit(5_000_000)]
        public async Task<ActionResult<AiCositoResponseDto>> CosaDelCosito([FromForm] AiCositoRequestDto request)
        {
            var description = request.Description?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(description) && request.Photo is null)
            {
                return BadRequest("Escribe qué ves o adjunta una foto para que ASI pueda orientarte.");
            }

            if (request.Photo is { Length: > 0 })
            {
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
                if (!allowedTypes.Contains(request.Photo.ContentType, StringComparer.OrdinalIgnoreCase))
                {
                    return BadRequest("La foto debe ser JPG, PNG o WEBP.");
                }

                if (request.Photo.Length > 4_000_000)
                {
                    return BadRequest("La foto no puede superar 4 MB.");
                }
            }

            var apiKey = _configuration["OpenAI:ApiKey"] ?? _configuration["OPENAI_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Ok(BuildLocalCositoResponse(description, "local"));
            }

            try
            {
                var model = _configuration["OpenAI:VisionModel"] ?? _configuration["OpenAI:Model"] ?? "gpt-4.1-mini";
                var contentParts = new List<object>
                {
                    new
                    {
                        type = "input_text",
                        text = BuildCositoPrompt(description)
                    }
                };

                if (request.Photo is { Length: > 0 })
                {
                    await using var stream = request.Photo.OpenReadStream();
                    using var memory = new MemoryStream();
                    await stream.CopyToAsync(memory);
                    var base64 = Convert.ToBase64String(memory.ToArray());
                    contentParts.Add(new
                    {
                        type = "input_image",
                        image_url = $"data:{request.Photo.ContentType};base64,{base64}"
                    });
                }

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
                            content = new[]
                            {
                                new
                                {
                                    type = "input_text",
                                    text = """
                                        Eres ASI en el modo "La cosa del cosito" de SERVILABS.
                                        Ayudas a una persona cliente a describir mejor un problema domestico o laboral a partir de texto y/o foto.
                                        No diagnostiques con certeza, no reemplaces al profesional y no indiques reparaciones peligrosas paso a paso.
                                        Devuelve solo JSON valido con las claves: orientation, suggestedTrade, suggestedService, suggestedPost, safetyNote.
                                        El suggestedPost debe ser un anuncio breve, claro y editable para publicar una solicitud.
                                        Si hay riesgo de gas, electricidad, fuego, humo, agua cerca de electricidad o estructura insegura, incluye una advertencia simple de seguridad.
                                        """
                                }
                            }
                        },
                        new
                        {
                            role = "user",
                            content = contentParts
                        }
                    },
                    max_output_tokens = 450
                };

                using var response = await client.PostAsync(
                    "https://api.openai.com/v1/responses",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return Ok(BuildLocalCositoResponse(description, $"openai-{(int)response.StatusCode}"));
                }

                var text = ExtractResponseText(content);
                var parsed = TryParseCositoResponse(text);
                return Ok(parsed with { AiEnabled = true, Source = "openai" });
            }
            catch
            {
                return Ok(BuildLocalCositoResponse(description, "local-error"));
            }
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

        private static string BuildCositoPrompt(string description)
        {
            var safeDescription = string.IsNullOrWhiteSpace(description)
                ? "La persona adjunto una foto y no sabe como describir el problema."
                : description;

            return $"""
                La persona cliente dice: "{safeDescription}".
                Ayudala a pasar de "la cosa del cosito" a una solicitud clara para SERVILABS.
                Si no hay suficiente informacion, aclara que es una orientacion probable.
                No afirmes marcas, piezas exactas ni materiales si no son evidentes.
                """;
        }

        private static AiCositoResponseDto TryParseCositoResponse(string text)
        {
            try
            {
                var start = text.IndexOf('{');
                var end = text.LastIndexOf('}');
                var json = start >= 0 && end > start ? text[start..(end + 1)] : text;
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                return new AiCositoResponseDto(
                    ReadJsonString(root, "orientation", "ASI puede orientarte, pero el profesional confirma el diagnostico en persona."),
                    ReadJsonString(root, "suggestedTrade", "Profesional del rubro adecuado"),
                    ReadJsonString(root, "suggestedService", "Revision tecnica"),
                    ReadJsonString(root, "suggestedPost", "Necesito una revision tecnica. Adjunto foto como referencia para que el profesional pueda orientarse antes de venir."),
                    ReadJsonString(root, "safetyNote", "La sugerencia de ASI es orientativa y no reemplaza la evaluacion profesional."),
                    true,
                    "openai");
            }
            catch
            {
                return new AiCositoResponseDto(
                    "ASI puede orientarte, pero el profesional confirma el diagnostico en persona.",
                    "Profesional del rubro adecuado",
                    "Revision tecnica",
                    string.IsNullOrWhiteSpace(text) ? "Necesito una revision tecnica. Adjunto foto como referencia para orientar la visita." : text,
                    "La sugerencia de ASI es orientativa y no reemplaza la evaluacion profesional.",
                    true,
                    "openai-text");
            }
        }

        private static string ReadJsonString(JsonElement root, string propertyName, string fallback)
        {
            return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? fallback
                : fallback;
        }

        private static AiCositoResponseDto BuildLocalCositoResponse(string description, string source)
        {
            var normalized = description.ToLowerInvariant();
            var suggestedTrade = "Profesional del rubro adecuado";
            var suggestedService = "Revision tecnica";
            var safetyNote = "La sugerencia de ASI es orientativa y no reemplaza la evaluacion profesional.";

            if (normalized.Contains("gas") || normalized.Contains("calefon") || normalized.Contains("calefón") || normalized.Contains("olor"))
            {
                suggestedTrade = "Gasista matriculado o plomero";
                suggestedService = "Revision de calefon, conexion o posible perdida";
                safetyNote = "Si hay olor a gas, ventila el ambiente, evita encender luces o artefactos y contacta a un gasista matriculado o emergencias.";
            }
            else if (normalized.Contains("cable") || normalized.Contains("enchufe") || normalized.Contains("luz") || normalized.Contains("chispa") || normalized.Contains("electric"))
            {
                suggestedTrade = "Electricista";
                suggestedService = "Revision electrica";
                safetyNote = "Si hay chispas, olor a quemado o cables expuestos, evita manipular la zona y corta la energia si puedes hacerlo con seguridad.";
            }
            else if (normalized.Contains("agua") || normalized.Contains("caño") || normalized.Contains("cano") || normalized.Contains("perdida") || normalized.Contains("pérdida"))
            {
                suggestedTrade = "Plomero";
                suggestedService = "Revision de perdida o cañeria";
                safetyNote = "Si la perdida es grande, intenta cerrar la llave de paso y aleja artefactos electricos.";
            }

            var post = $"Necesito {suggestedTrade.ToLowerInvariant()} para {suggestedService.ToLowerInvariant()}. ";
            post += string.IsNullOrWhiteSpace(description)
                ? "Adjunto foto como referencia para que el profesional pueda orientarse antes de venir."
                : $"Lo que veo es: {description.Trim()}. Adjunto foto como referencia si corresponde.";

            return new AiCositoResponseDto(
                "ASI te ayuda a ponerle nombre al problema para publicar una solicitud mas clara. El diagnostico final lo confirma el profesional.",
                suggestedTrade,
                suggestedService,
                post,
                safetyNote,
                false,
                source);
        }
    }
}
