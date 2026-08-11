namespace ArtemisBankingPro.Core.Application.DTOs.User
{
    public class ClientForAssignmentDto : BaseDto<string>
    {
        public required string Identification { get; set; }
        public required string FullName { get; set; }
        public required string Email { get; set; }
        public decimal TotalDebt { get; set; }
    }
}
