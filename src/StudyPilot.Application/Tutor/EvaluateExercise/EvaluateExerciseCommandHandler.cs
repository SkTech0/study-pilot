using MediatR;
using StudyPilot.Application.Abstractions.Learning;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Abstractions.Tutor;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using StudyPilot.Application.Tutor.Models;

namespace StudyPilot.Application.Tutor.EvaluateExercise;

public sealed class EvaluateExerciseCommandHandler : IRequestHandler<EvaluateExerciseCommand, Result<EvaluateExerciseResult>>
{
    private readonly ITutorExerciseRepository _exerciseRepository;
    private readonly ITutorSessionRepository _sessionRepository;
    private readonly ITutorService _tutorService;
    private readonly IMasteryEngine _masteryEngine;

    public EvaluateExerciseCommandHandler(
        ITutorExerciseRepository exerciseRepository,
        ITutorSessionRepository sessionRepository,
        ITutorService tutorService,
        IMasteryEngine masteryEngine)
    {
        _exerciseRepository = exerciseRepository;
        _sessionRepository = sessionRepository;
        _tutorService = tutorService;
        _masteryEngine = masteryEngine;
    }

    public async Task<Result<EvaluateExerciseResult>> Handle(EvaluateExerciseCommand request, CancellationToken cancellationToken)
    {
        var exercise = await _exerciseRepository.GetByIdAsync(request.ExerciseId, cancellationToken);
        if (exercise is null)
            return Result<EvaluateExerciseResult>.Failure(new AppError(ErrorCodes.NotFound, "Exercise not found.", null, ErrorSeverity.Business));

        var session = await _sessionRepository.GetByIdAndUserIdAsync(exercise.TutorSessionId, request.UserId, cancellationToken);
        if (session is null)
            return Result<EvaluateExerciseResult>.Failure(new AppError(ErrorCodes.NotFound, "Session not found.", null, ErrorSeverity.Business));

        var evalRequest = new ExerciseEvaluationRequest(
            exercise.Id,
            exercise.Question,
            exercise.ExpectedAnswer,
            (request.UserAnswer ?? "").Trim());
        var result = await _tutorService.EvaluateExerciseAsync(evalRequest, cancellationToken);

        var quizResult = new QuizResultForMastery(request.UserId,
            new[] { new ConceptAnswerResult(exercise.ConceptId, result.IsCorrect) });
        await _masteryEngine.UpdateFromQuizResultAsync(quizResult, cancellationToken);

        return Result<EvaluateExerciseResult>.Success(new EvaluateExerciseResult(result.IsCorrect, result.Explanation));
    }
}
