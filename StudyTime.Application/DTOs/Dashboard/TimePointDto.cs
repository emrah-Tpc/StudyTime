namespace StudyTime.Application.DTOs.Dashboard
{
    public sealed class TimePointDto
    {
        public string Label { get; init; } = ""; // Mon, Tue, Wed...
        public double Hours { get; init; }
    }
}
