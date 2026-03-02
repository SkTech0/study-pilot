using MediatR;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using StudyPilot.Domain.Entities;

namespace StudyPilot.Application.Chat.CreateChatSession;

public sealed class CreateChatSessionCommandHandler : IRequestHandler<CreateChatSessionCommand, Result<CreateChatSessionResult>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IChatSessionRepository _chatSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateChatSessionCommandHandler(
        IDocumentRepository documentRepository,
        IChatSessionRepository chatSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _documentRepository = documentRepository;
        _chatSessionRepository = chatSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateChatSessionResult>> Handle(CreateChatSessionCommand request, CancellationToken cancellationToken)
    {
        if (request.DocumentId.HasValue)
        {
            var doc = await _documentRepository.GetByIdAsync(request.DocumentId.Value, cancellationToken);
            if (doc is null)
                return Result<CreateChatSessionResult>.Failure(new AppError(ErrorCodes.DocumentNotFound, "Document not found.", "documentId", ErrorSeverity.Business));
            if (doc.UserId != request.UserId)
                return Result<CreateChatSessionResult>.Failure(new AppError(ErrorCodes.ChatSessionAccessDenied, "You do not have access to this document.", "documentId", ErrorSeverity.Business));
        }

        var session = new ChatSession(request.UserId, request.DocumentId);
        await _chatSessionRepository.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<CreateChatSessionResult>.Success(new CreateChatSessionResult(session.Id));
    }
}

