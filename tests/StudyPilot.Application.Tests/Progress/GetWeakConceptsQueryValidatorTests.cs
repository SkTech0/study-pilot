using StudyPilot.Application.Progress.GetWeakConcepts;
using Xunit;

namespace StudyPilot.Application.Tests.Progress;

public sealed class GetWeakConceptsQueryValidatorTests
{
    private readonly GetWeakConceptsQueryValidator _validator = new();

    [Fact]
    public void Should_HaveError_When_UserId_IsEmpty()
    {
        var query = new GetWeakConceptsQuery(Guid.Empty);
        var result = _validator.Validate(query);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetWeakConceptsQuery.UserId));
    }

    [Fact]
    public void Should_NotHaveError_When_UserId_IsValid()
    {
        var query = new GetWeakConceptsQuery(Guid.NewGuid());
        var result = _validator.Validate(query);
        Assert.True(result.IsValid);
    }
}
