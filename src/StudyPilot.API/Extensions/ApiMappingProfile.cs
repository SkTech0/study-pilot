using AutoMapper;
using StudyPilot.API.Contracts.Requests;
using StudyPilot.API.Contracts.Responses;
using StudyPilot.Application.Auth;
using StudyPilot.Application.Auth.Login;
using StudyPilot.Application.Auth.Register;
using StudyPilot.Application.Common.Models;
using StudyPilot.Application.Documents.GetDocuments;
using StudyPilot.Application.Documents.UploadDocument;
using StudyPilot.Application.Progress.GetWeakConcepts;
using StudyPilot.Application.Quiz.StartQuiz;
using StudyPilot.Application.Auth.Logout;
using StudyPilot.Application.Auth.Refresh;
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
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.CreatedAtUtc));
        CreateMap<StartQuizRequest, StartQuizCommand>().ForMember(c => c.UserId, o => o.Ignore());
        CreateMap<StartQuizQuestionSummary, QuizQuestionResponse>();
        CreateMap<StartQuizResult, StartQuizResponse>();
        CreateMap<QuizAnswerRequest, QuizAnswerInput>();
        CreateMap<SubmitQuizRequest, SubmitQuizCommand>().ForMember(c => c.UserId, o => o.Ignore());
        CreateMap<SubmitQuizResult, SubmitQuizResponse>();
        CreateMap<WeakConceptItem, WeakTopicResponse>();
        CreateMap<RefreshTokenRequest, RefreshTokenCommand>();
        CreateMap<RefreshTokenRequest, LogoutCommand>();
    }
}
