using System.Runtime.CompilerServices;
using MediatR;
using StudyPilot.Application.Common.Models;
using StudyPilot.Application.Tutor.TutorRespond;

namespace StudyPilot.Application.Tutor.StreamTutor;

public sealed class StreamTutorQueryHandler : IRequestHandler<StreamTutorQuery, Result<StreamTutorResult>>
{
    private readonly IMediator _mediator;

    public StreamTutorQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<Result<StreamTutorResult>> Handle(StreamTutorQuery request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new TutorRespondCommand(request.UserId, request.SessionId, request.Message), cancellationToken);
        if (!result.IsSuccess)
            return Result<StreamTutorResult>.Failure(result.Errors);

        var v = result.Value!;
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async IAsyncEnumerable<string> Tokens([EnumeratorCancellation] CancellationToken ct)
        {
            var words = (v.AssistantMessage ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                ct.ThrowIfCancellationRequested();
                yield return word + " ";
            }
            tcs.TrySetResult();
        }
        return Result<StreamTutorResult>.Success(new StreamTutorResult(
            Tokens(cancellationToken),
            tcs.Task,
            v.NextStep,
            v.OptionalExercise != null ? new TutorStreamExerciseResult(v.OptionalExercise.ExerciseId, v.OptionalExercise.Question, v.OptionalExercise.ExpectedAnswer, v.OptionalExercise.Difficulty) : null,
            v.CitedChunkIds ?? new List<Guid>()));
    }
}
