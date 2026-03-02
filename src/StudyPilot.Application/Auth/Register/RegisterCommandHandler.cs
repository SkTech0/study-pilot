using StudyPilot.Application.Abstractions.Auth;
using StudyPilot.Application.Abstractions.Persistence;
using StudyPilot.Application.Auth;
using StudyPilot.Application.Common.Errors;
using StudyPilot.Application.Common.Models;
using MediatR;
using StudyPilot.Domain.Entities;
using StudyPilot.Domain.Enums;
using StudyPilot.Domain.ValueObjects;

namespace StudyPilot.Application.Auth.Register;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResult>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenGenerator tokenGenerator, IRefreshTokenRepository refreshTokenRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResult>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<AuthResult>.Failure(ValidationErrorFactory.Create(ErrorCodes.ValidationRequired, "Email is required.", "email"));
        if (!Email.TryCreate(request.Email, out var email, out var emailError))
            return Result<AuthResult>.Failure(ValidationErrorFactory.Create(ErrorCodes.ValidationEmailInvalid, emailError ?? "Invalid email.", "email"));

        var existing = await _userRepository.GetByEmailAsync(email!.Value, cancellationToken);
        if (existing is not null)
            return Result<AuthResult>.Failure(new AppError(ErrorCodes.AuthUserExists, "Email already registered.", "email", ErrorSeverity.Business));
        var user = new User(email!, _passwordHasher.Hash(request.Password), UserRole.Student);
        await _userRepository.AddAsync(user, cancellationToken);
        var (accessToken, expiresAt) = _tokenGenerator.GenerateAccessToken(user.Id, user.Email.Value, user.Role.ToString());
        var (refreshToken, refreshExpires) = _tokenGenerator.GenerateRefreshToken();
        await _refreshTokenRepository.AddAsync(user.Id, refreshToken, refreshExpires, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AuthResult>.Success(new AuthResult(accessToken, refreshToken, expiresAt, user.Id));
    }
}
