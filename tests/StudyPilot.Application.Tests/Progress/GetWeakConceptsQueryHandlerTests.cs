using Moq;
using StudyPilot.Application.Abstractions.Caching;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Models;
using StudyPilot.Application.Progress.GetWeakConcepts;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.ValueObjects;
using Xunit;

namespace StudyPilot.Application.Tests.Progress;

public sealed class GetWeakConceptsQueryHandlerTests
{
    private readonly Mock<IUserConceptProgressRepository> _progressRepo = new();
    private readonly Mock<IConceptRepository> _conceptRepo = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly GetWeakConceptsQueryHandler _handler;

    public GetWeakConceptsQueryHandlerTests()
    {
        _handler = new GetWeakConceptsQueryHandler(_progressRepo.Object, _conceptRepo.Object, _cache.Object);
    }

    [Fact]
    public async Task Handle_ReturnsCachedResult_WhenCacheHasValue()
    {
        var userId = Guid.NewGuid();
        var cached = new List<WeakConceptItem> { new(Guid.NewGuid(), "Cached", 30) };
        _cache.Setup(c => c.GetAsync<IReadOnlyList<WeakConceptItem>>(It.Is<string>(s => s.Contains(userId.ToString())), default))
            .ReturnsAsync(cached);

        var result = await _handler.Handle(new GetWeakConceptsQuery(userId), default);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("Cached", result.Value![0].Name);
        _progressRepo.Verify(p => p.GetWeakByUserIdAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoWeakProgress()
    {
        var userId = Guid.NewGuid();
        _cache.Setup(c => c.GetAsync<IReadOnlyList<WeakConceptItem>>(It.IsAny<string>(), default)).ReturnsAsync((IReadOnlyList<WeakConceptItem>?)null);
        _progressRepo.Setup(p => p.GetWeakByUserIdAsync(userId, 40, It.IsAny<CancellationToken>())).ReturnsAsync(new List<StudyPilot.Domain.Entities.UserConceptProgress>());

        var result = await _handler.Handle(new GetWeakConceptsQuery(userId), default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Handle_ReturnsWeakConcepts_WhenProgressAndConceptsExist()
    {
        var userId = Guid.NewGuid();
        var conceptId = Guid.NewGuid();
        var progress = new StudyPilot.Domain.Entities.UserConceptProgress(userId, conceptId);
        var concept = new Concept(conceptId, Guid.NewGuid(), "Test Concept", null, DateTime.UtcNow, DateTime.UtcNow);

        _cache.Setup(c => c.GetAsync<IReadOnlyList<WeakConceptItem>>(It.IsAny<string>(), default)).ReturnsAsync((IReadOnlyList<WeakConceptItem>?)null);
        _progressRepo.Setup(p => p.GetWeakByUserIdAsync(userId, 40, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudyPilot.Domain.Entities.UserConceptProgress> { progress });
        _conceptRepo.Setup(c => c.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Concept> { concept });

        var result = await _handler.Handle(new GetWeakConceptsQuery(userId), default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value);
        Assert.Equal(conceptId, result.Value[0].ConceptId);
        Assert.Equal("Test Concept", result.Value[0].Name);
    }
}
