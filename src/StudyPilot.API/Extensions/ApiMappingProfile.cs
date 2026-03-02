using AutoMapper;
using StudyPilot.API.Contracts.Requests;
using StudyPilot.API.Contracts.Responses;
using StudyPilot.Application.Auth;
using StudyPilot.Application.Auth.Login;
using StudyPilot.Application.Auth.Register;
using StudyPilot.Application.Chat.CreateChatSession;
using StudyPilot.Application.Chat.GetChatHistory;
using StudyPilot.Application.Chat.SendChatMessage;
using StudyPilot.Application.Common.Models;
using StudyPilot.Application.Documents.GetDocuments;
using StudyPilot.Application.Documents.UploadDocument;
using StudyPilot.Application.Progress.GetWeakConcepts;
using StudyPilot.Application.Quiz.StartQuiz;
using StudyPilot.Application.Auth.Logout;
using StudyPilot.Application.Auth.Refresh;
using StudyPilot.Application.Quiz.GetQuizQuestion;
using StudyPilot.Application.Quiz.SubmitQuiz;

namespace StudyPilot.API.Extensions;

public sealed class ApiMappingProfile : Profile
{
    public ApiMappingProfile()
    {
        CreateMap<RegisterRequest, RegisterCommand>();
        CreateMap<LoginRequest, LoginCommand>();
        CreateMap<AuthResult, AuthResponse>()
            .ConstructUsing(s => new AuthResponse(s.AccessToken, s.RefreshToken, s.AccessTokenExpiresAtUtc, s.UserId));
        CreateMap<UploadDocumentResult, UploadDocumentResponse>()
            .ForMember(d => d.Status, o => o.MapFrom(_ => "processing"));
        CreateMap<DocumentListItem, DocumentResponse>()
            .ConstructUsing(s => new DocumentResponse(s.Id, s.FileName, s.Status, s.CreatedAtUtc, s.FailureReason));
        CreateMap<StartQuizRequest, StartQuizCommand>().ForMember(c => c.UserId, o => o.Ignore());
        CreateMap<StartQuizQuestionSummary, QuizQuestionResponse>();
        CreateMap<StartQuizResult, StartQuizResponse>();
        CreateMap<GetQuizQuestionResult, GetQuizQuestionResponse>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
        CreateMap<QuizAnswerRequest, QuizAnswerInput>();
        CreateMap<SubmitQuizRequest, SubmitQuizCommand>().ForMember(c => c.UserId, o => o.Ignore());
        CreateMap<SubmitQuizResult, SubmitQuizResponse>();
        CreateMap<QuestionResultItem, QuestionResultResponse>();
        CreateMap<WeakConceptItem, WeakTopicResponse>();
        CreateMap<RefreshTokenRequest, RefreshTokenCommand>();
        CreateMap<RefreshTokenRequest, LogoutCommand>();
        CreateMap<CreateChatSessionResult, CreateChatSessionResponse>();
        CreateMap<SendChatMessageResult, SendChatMessageResponse>();
        CreateMap<GetChatHistoryResult, GetChatHistoryResponse>();
        CreateMap<ChatMessageItem, ChatMessageItemResponse>()
            .ForMember(d => d.Role, o => o.MapFrom(s => s.Role.ToString()));
    }
}
