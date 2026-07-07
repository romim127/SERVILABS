using System.ComponentModel.DataAnnotations;

namespace AppServicios.Api.DTOs
{
    public sealed class AiChatRequestDto
    {
        [Required]
        [StringLength(1200, MinimumLength = 1)]
        public string Message { get; set; } = string.Empty;

        [StringLength(40)]
        public string Context { get; set; } = "onboarding";
    }

    public sealed record AiChatResponseDto(
        string Message,
        bool AiEnabled,
        string Source);
}
