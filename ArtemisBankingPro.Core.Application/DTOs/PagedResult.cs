namespace ArtemisBankingPro.Core.Application.DTOs
{
    public class PagedResult<T>
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public required List<T> Items { get; set; }
        public required int Page { get; set; }
        public required int PageSize { get; set; }
        public required int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalRecords / (double)PageSize);
    }
}
