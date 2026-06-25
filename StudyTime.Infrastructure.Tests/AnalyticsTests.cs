using System;
using StudyTime.Application;
using Xunit;

namespace StudyTime.Infrastructure.Tests
{
    public class AnalyticsTests
    {
        [Theory]
        [InlineData(59, 0)]
        [InlineData(60, 1)]
        [InlineData(90, 2)] // 1.5 minutes rounds to 2
        [InlineData(119, 2)] // 1.98 minutes rounds to 2
        [InlineData(150, 2)] // 2.5 minutes rounds to 2 (Banker's rounding)
        public void ToChartMinutes_CalculatesCorrectly(int seconds, int expectedMinutes)
        {
            // Arrange
            var duration = TimeSpan.FromSeconds(seconds);

            // Act
            var result = StudyDurationMetrics.ToChartMinutes(duration);

            // Assert
            Assert.Equal(expectedMinutes, result);
        }

        [Theory]
        [InlineData(59, "59sn")]
        [InlineData(60, "1dk")]
        [InlineData(65, "1dk 5sn")]
        [InlineData(3600, "1s")]
        [InlineData(5400, "1,5s")]
        [InlineData(0, "0dk")]
        public void FormatDisplay_FormatsCorrectly(int seconds, string expectedDisplay)
        {
            // Arrange
            var duration = TimeSpan.FromSeconds(seconds);

            // Act
            var result = StudyDurationMetrics.FormatDisplay(duration);

            // Assert
            Assert.Equal(expectedDisplay, result);
        }

        [Fact]
        public void SumChartMinutes_SumsCorrectly()
        {
            // Arrange
            var durations = new[]
            {
                TimeSpan.FromSeconds(50), // 0 min
                TimeSpan.FromSeconds(70), // 1 min
                TimeSpan.FromSeconds(150) // 2 min
            };

            // Act
            var sum = StudyDurationMetrics.SumChartMinutes(durations);

            // Assert
            Assert.Equal(3, sum); // 0 + 1 + 2 = 3
        }
    }
}
