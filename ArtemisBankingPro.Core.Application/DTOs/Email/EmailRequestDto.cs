namespace ArtemisBankingPro.Core.Application.DTOs.Email
{
    public class EmailRequestDto
    {
        public string? To { get; set; }
        public required string Subject { get; set; }
        public required string BodyHtml { get; set; }
        public List<string> ToRange { get; set; } = [];
    }
}
