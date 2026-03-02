using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Abstractions.Persistence;

public interface IUserAnswerRepository
{
    Task AddAsync(UserAnswer answer, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<UserAnswer> answers, CancellationToken cancellationToken = default);
}
