using MediatR;
using StudyPilot.Application.Common.Models;

namespace StudyPilot.Application.Progress.GetWeakConcepts;

public sealed record GetWeakConceptsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<WeakConceptItem>>>;
