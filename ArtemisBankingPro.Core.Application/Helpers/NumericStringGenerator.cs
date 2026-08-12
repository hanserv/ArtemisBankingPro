namespace ArtemisBankingPro.Core.Application.Helpers
{
    public static class NumericStringGenerator
    {
        public static string Generate(int length)
        {
            return string.Concat(Enumerable.Range(0, length).Select(_ => Random.Shared.Next(0, 10)));
        }
    }
}
