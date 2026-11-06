using FluentValidation;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;

namespace TeamPortal.Application.Validators;

/// <summary>
/// Upload rules from the Add Step modal: PNG, JPG or WEBP up to 5 MB.
/// The content type is checked here; the file's actual byte signature is checked by
/// the file store, because an extension can be renamed and a signature cannot.
/// </summary>
public sealed class FileUploadValidator : AbstractValidator<FileUpload>
{
    public const long MaxSizeBytes = 5 * 1024 * 1024;

    public static readonly IReadOnlyList<string> AllowedContentTypes =
        ["image/png", "image/jpeg", "image/webp"];

    public FileUploadValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("The file needs a name.")
            .MaximumLength(260);

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("That file is empty.")
            .LessThanOrEqualTo(MaxSizeBytes)
            .WithMessage($"That file is too big. The limit is {MaxSizeBytes / 1024 / 1024} MB.");

        RuleFor(x => x.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Only PNG, JPG and WEBP images can be uploaded.");
    }
}

public sealed class CreateProcessFolderRequestValidator : AbstractValidator<CreateProcessFolderRequest>
{
    public CreateProcessFolderRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Give the folder a name.")
            .MinimumLength(2).WithMessage("The name needs at least 2 characters.")
            .MaximumLength(FieldRules.DepartmentNameMaxLength);
    }
}

public sealed class CreateProcessRequestValidator : AbstractValidator<CreateProcessRequest>
{
    public CreateProcessRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Give the process a name.")
            .MinimumLength(3).WithMessage("The name needs at least 3 characters.")
            .MaximumLength(FieldRules.NameMaxLength);

        RuleFor(x => x.ProcessFolderId)
            .GreaterThan(0).WithMessage("Choose a folder for this process.");

        RuleFor(x => x.Description)
            .MaximumLength(FieldRules.LongDescriptionMaxLength);
    }
}

public sealed class UpdateProcessRequestValidator : AbstractValidator<UpdateProcessRequest>
{
    public UpdateProcessRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Give the process a name.")
            .MinimumLength(3).WithMessage("The name needs at least 3 characters.")
            .MaximumLength(FieldRules.NameMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(FieldRules.LongDescriptionMaxLength);
    }
}

public sealed class CreateStepRequestValidator : AbstractValidator<CreateStepRequest>
{
    public CreateStepRequestValidator()
    {
        RuleFor(x => x.ProcessId)
            .GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Give the step a name.")
            .MinimumLength(3).WithMessage("The name needs at least 3 characters.")
            .MaximumLength(FieldRules.NameMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(FieldRules.StepDescriptionMaxLength);

        RuleFor(x => x.Owners)
            .Must(HaveNoDuplicates)
            .OverridePropertyName(nameof(CreateStepRequest.OwnerIds))
            .WithMessage("That person is already on this step.");

        RuleForEach(x => x.Owners)
            .GreaterThan(0)
            .OverridePropertyName(nameof(CreateStepRequest.OwnerIds));

        RuleFor(x => x.Screenshot!)
            .SetValidator(new FileUploadValidator())
            .When(x => x.Screenshot is not null);
    }

    internal static bool HaveNoDuplicates(IReadOnlyList<int> ids) =>
        ids.Distinct().Count() == ids.Count;
}

public sealed class UpdateStepRequestValidator : AbstractValidator<UpdateStepRequest>
{
    public UpdateStepRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Give the step a name.")
            .MinimumLength(3).WithMessage("The name needs at least 3 characters.")
            .MaximumLength(FieldRules.NameMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(FieldRules.StepDescriptionMaxLength);

        RuleFor(x => x.Owners)
            .Must(CreateStepRequestValidator.HaveNoDuplicates)
            .OverridePropertyName(nameof(UpdateStepRequest.OwnerIds))
            .WithMessage("That person is already on this step.");
    }
}

public sealed class MoveStepRequestValidator : AbstractValidator<MoveStepRequest>
{
    public MoveStepRequestValidator()
    {
        RuleFor(x => x.StepId).GreaterThan(0);

        RuleFor(x => x.NewStepNumber)
            .GreaterThan(0).WithMessage("Steps are numbered from 1.");
    }
}
