using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Lingarr.Core.Data;
using Lingarr.Core.Entities;
using Lingarr.Core.Enum;
using Lingarr.Server.Hubs;
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Interfaces.Services.Translation;
using Lingarr.Server.Jobs;
using Lingarr.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Lingarr.Server.Tests.Services;

public class TranslationRequestServiceTests
{
    [Fact]
    public async Task RescheduleRequest_SchedulesDelayedJobAndResetsRequestToPending()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<LingarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new LingarrDbContext(options);
        var request = new TranslationRequest
        {
            Title = "test",
            SourceLanguage = "en",
            TargetLanguage = "es",
            MediaType = MediaType.Movie,
            Status = TranslationStatus.InProgress,
            JobId = "old-job"
        };
        dbContext.TranslationRequests.Add(request);
        await dbContext.SaveChangesAsync();

        Job? scheduledJob = null;
        IState? scheduledState = null;
        var backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        backgroundJobClientMock
            .Setup(client => client.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((job, state) =>
            {
                scheduledJob = job;
                scheduledState = state;
            })
            .Returns("new-job");

        var eventServiceMock = new Mock<ITranslationRequestEventService>();
        var service = new TranslationRequestService(
            dbContext,
            backgroundJobClientMock.Object,
            new Mock<IHubContext<TranslationRequestsHub>> { DefaultValue = DefaultValue.Mock }.Object,
            Mock.Of<ITranslationServiceFactory>(),
            Mock.Of<IProgressService>(),
            Mock.Of<IStatisticsService>(),
            Mock.Of<IMediaService>(),
            Mock.Of<ISettingService>(),
            Mock.Of<ISubtitleService>(),
            eventServiceMock.Object,
            NullLogger<TranslationRequestService>.Instance);

        // Act
        var jobId = await service.RescheduleRequest(request, TimeSpan.FromMinutes(10));

        // Assert
        Assert.Equal("new-job", jobId);
        Assert.Equal(typeof(TranslationJob), scheduledJob!.Type);
        Assert.Equal(nameof(TranslationJob.Execute), scheduledJob.Method.Name);
        var scheduled = Assert.IsType<ScheduledState>(scheduledState);
        Assert.InRange(scheduled.EnqueueAt,
            DateTime.UtcNow.AddMinutes(9),
            DateTime.UtcNow.AddMinutes(11));

        var stored = await dbContext.TranslationRequests.FindAsync(request.Id);
        Assert.Equal(TranslationStatus.Pending, stored!.Status);
        Assert.Equal("new-job", stored.JobId);
        eventServiceMock.Verify(
            eventService => eventService.LogEvent(request.Id, TranslationStatus.Pending, It.IsAny<string?>()),
            Times.Once);
    }
}
