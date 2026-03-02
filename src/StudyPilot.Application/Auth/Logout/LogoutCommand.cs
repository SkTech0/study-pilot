using StudyPilot.Application.Common.Models;
using MediatR;

namespace StudyPilot.Application.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest<Result<Unit>>;
