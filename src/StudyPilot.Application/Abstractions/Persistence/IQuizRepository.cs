namespace StudyPilot.Application.Abstractions.Persistence;

public interface IQuizRepository
{
    Task<Domain.Entities.Quiz?> GetByIdWithQuestionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Domain.Entities.Quiz?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Domain.Entities.Question?> GetQuestionByQuizAndIndexAsync(Guid quizId, int questionIndex, CancellationToken cancellationToken = default);
    Task AddAsync(Domain.Entities.Quiz quiz, CancellationToken cancellationToken = default);
    /// <summary>Adds a question. Returns false if unique (QuizId, QuestionIndex) constraint would be violated.</summary>
    Task<bool> TryAddQuestionAsync(Domain.Entities.Question question, CancellationToken cancellationToken = default);
    Task AddQuestionAsync(Domain.Entities.Question question, CancellationToken cancellationToken = default);
    Task UpdateAsync(Domain.Entities.Quiz quiz, CancellationToken cancellationToken = default);
    Task UpdateQuestionAsync(Domain.Entities.Question question, CancellationToken cancellationToken = default);
}
