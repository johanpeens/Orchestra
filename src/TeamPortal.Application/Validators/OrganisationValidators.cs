using FluentValidation;
using TeamPortal.Application.Requests;

namespace TeamPortal.Application.Validators;

/// <summary>Rules shared by every form. Keeps the lengths in one place.</summary>
public static class FieldRules
{
    public const int NameMaxLength = 150;
    public const int DepartmentNameMaxLength = 100;
    public const int ShortDescriptionMaxLength = 500;
    public const int LongDescriptionMaxLength = 1000;
    public const int StepDescriptionMaxLength = 2000;
    public const int EmailMaxLength = 256;
    public const int CodeMaxLength = 10;

    /// <summary>Uppercase 6-digit hex, e.g. <c>#4A2767</c>. Matches the column constraint.</summary>
    public const string ColourHexPattern = "^#[0-9A-F]{6}$";

    public const string ColourHexMessage =
        "Pick a colour from the swatches. It must look like #4A2767 - a hash and six uppercase hex digits.";
}

public sealed class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    public CreateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Give the department a name.")
            .MinimumLength(2).WithMessage("The name needs at least 2 characters.")
            .MaximumLength(FieldRules.DepartmentNameMaxLength);

        RuleFor(x => x.ColourHex)
            .NotEmpty().WithMessage("Pick a theme colour.")
            .Matches(FieldRules.ColourHexPattern).WithMessage(FieldRules.ColourHexMessage);
    }
}

public sealed class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Give the department a name.")
            .MinimumLength(2).WithMessage("The name needs at least 2 characters.")
            .MaximumLength(FieldRules.DepartmentNameMaxLength);

        RuleFor(x => x.ColourHex)
            .NotEmpty().WithMessage("Pick a theme colour.")
            .Matches(FieldRules.ColourHexPattern).WithMessage(FieldRules.ColourHexMessage);
    }
}

public sealed class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Enter the employee's name.")
            .MinimumLength(2).WithMessage("The name needs at least 2 characters.")
            .MaximumLength(FieldRules.NameMaxLength);

        RuleFor(x => x.DepartmentId)
            .GreaterThan(0).WithMessage("Choose a department.");

        RuleFor(x => x.JobTitle)
            .MaximumLength(FieldRules.NameMaxLength);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("That does not look like an email address.")
            .MaximumLength(FieldRules.EmailMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.StaffType).IsInEnum();

        RuleFor(x => x.Photo!)
            .SetValidator(new FileUploadValidator())
            .When(x => x.Photo is not null);
    }
}

public sealed class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Enter the employee's name.")
            .MinimumLength(2).WithMessage("The name needs at least 2 characters.")
            .MaximumLength(FieldRules.NameMaxLength);

        RuleFor(x => x.DepartmentId)
            .GreaterThan(0).WithMessage("Choose a department.");

        RuleFor(x => x.JobTitle)
            .MaximumLength(FieldRules.NameMaxLength);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("That does not look like an email address.")
            .MaximumLength(FieldRules.EmailMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.StaffType).IsInEnum();

        RuleFor(x => x.Photo!)
            .SetValidator(new FileUploadValidator())
            .When(x => x.Photo is not null);
    }
}
