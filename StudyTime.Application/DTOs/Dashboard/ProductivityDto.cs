namespace StudyTime.Application.DTOs.Dashboard
{
    public sealed class ProductivityDto
    {
        public int Score { get; init; }              // 0–100
        public string Status { get; init; } = "";    // Good / Average / Low
        public int TrendPercentage { get; init; }    // +12 / -5
    }
}
