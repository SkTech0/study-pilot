using FluentValidation;
using StudyPilot.Application.Common.Errors;

namespace StudyPilot.Application.Documents.UploadDocument;

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired);
        RuleFor(x => x.FileName).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired).MaximumLength(500);
        RuleFor(x => x.StoragePath).NotEmpty().WithErrorCode(ErrorCodes.ValidationRequired).MaximumLength(2000);
    }
}
