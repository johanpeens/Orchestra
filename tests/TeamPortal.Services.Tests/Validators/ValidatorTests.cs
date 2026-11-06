using FluentValidation.Results;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;
using TeamPortal.Application.Requests;
using TeamPortal.Application.Validators;

namespace TeamPortal.Services.Tests.Validators;

public class CreateEmployeeValidatorTests
{
    private readonly CreateEmployeeRequestValidator _sut = new();

    private static CreateEmployeeRequest Valid(
        string name = "Priya", int departmentId = 1, string? email = null, FileUpload? photo = null) =>
        new(name, departmentId, JobTitle: "UX/Software Engineer", Email: email, Photo: photo);

    [Fact]
    public void CreateEmployeeValidator_WithAValidRequest_Passes()
    {
        ValidationResult result = _sut.Validate(Valid());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public void CreateEmployeeValidator_WithBlankOrTooShortName_Fails(string name)
    {
        ValidationResult result = _sut.Validate(Valid(name: name));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateEmployeeRequest.FullName));
    }

    [Fact]
    public void CreateEmployeeValidator_WithNameOver150Chars_Fails()
    {
        ValidationResult result = _sut.Validate(Valid(name: new string('a', 151)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateEmployeeRequest.FullName));
    }

    [Fact]
    public void CreateEmployeeValidator_WithExactly150CharName_Passes()
    {
        ValidationResult result = _sut.Validate(Valid(name: new string('a', 150)));

        result.IsValid.Should().BeTrue("150 is the column length, so it must be allowed");
    }

    [Fact]
    public void CreateEmployeeValidator_WithNoDepartment_Fails()
    {
        ValidationResult result = _sut.Validate(Valid(departmentId: 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateEmployeeRequest.DepartmentId));
    }

    [Fact]
    public void CreateEmployeeValidator_WithNoEmail_Passes()
    {
        ValidationResult result = _sut.Validate(Valid(email: null));

        result.IsValid.Should().BeTrue("email is optional on the Add Employee modal");
    }

    [Fact]
    public void CreateEmployeeValidator_WithAMalformedEmail_Fails()
    {
        ValidationResult result = _sut.Validate(Valid(email: "not-an-address"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateEmployeeRequest.Email));
    }

    [Fact]
    public void CreateEmployeeValidator_WithAnOversizePhoto_Fails()
    {
        var photo = new FileUpload("big.png", "image/png", 6 * 1024 * 1024, Stream.Null);

        ValidationResult result = _sut.Validate(Valid(photo: photo));

        result.IsValid.Should().BeFalse();
    }
}

public class UpdateDepartmentValidatorTests
{
    private readonly UpdateDepartmentRequestValidator _sut = new();

    [Theory]
    [InlineData("#4A2767")]
    [InlineData("#E55218")]
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    public void UpdateDepartmentValidator_WithAValidColour_Passes(string colour)
    {
        ValidationResult result = _sut.Validate(new UpdateDepartmentRequest(1, "Sales", colour));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("4A2767")]        // no hash
    [InlineData("#4a2767")]       // lowercase
    [InlineData("#4A276")]        // five digits
    [InlineData("#4A27677")]      // seven digits
    [InlineData("#GGGGGG")]       // not hex
    [InlineData("purple")]
    [InlineData("")]
    public void UpdateDepartmentValidator_WithABadColour_Fails(string colour)
    {
        ValidationResult result = _sut.Validate(new UpdateDepartmentRequest(1, "Sales", colour));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateDepartmentRequest.ColourHex));
    }

    [Fact]
    public void UpdateDepartmentValidator_WithNameOver100Chars_Fails()
    {
        ValidationResult result = _sut.Validate(
            new UpdateDepartmentRequest(1, new string('a', 101), "#4A2767"));

        result.IsValid.Should().BeFalse();
    }
}

public class CreateProcessValidatorTests
{
    private readonly CreateProcessRequestValidator _sut = new();

    [Fact]
    public void CreateProcessValidator_WithAValidRequest_Passes()
    {
        ValidationResult result = _sut.Validate(
            new CreateProcessRequest("Onboarding Process", ProcessFolderId: 1));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void CreateProcessValidator_WithNameUnder3Chars_Fails(string name)
    {
        ValidationResult result = _sut.Validate(new CreateProcessRequest(name, ProcessFolderId: 1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessRequest.Name));
    }

    [Fact]
    public void CreateProcessValidator_WithNoFolder_Fails()
    {
        ValidationResult result = _sut.Validate(new CreateProcessRequest("Onboarding", ProcessFolderId: 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessRequest.ProcessFolderId));
    }

    [Fact]
    public void CreateProcessValidator_WithDescriptionOver1000Chars_Fails()
    {
        ValidationResult result = _sut.Validate(
            new CreateProcessRequest("Onboarding", 1, new string('a', 1001)));

        result.IsValid.Should().BeFalse();
    }
}

public class CreateStepValidatorTests
{
    private readonly CreateStepRequestValidator _sut = new();

    private static CreateStepRequest Valid(
        IReadOnlyList<int>? owners = null, FileUpload? screenshot = null) =>
        new(ProcessId: 1, Name: "Initial Contact", OwnerIds: owners, Screenshot: screenshot);

    [Fact]
    public void CreateStepValidator_WithAValidRequest_Passes()
    {
        ValidationResult result = _sut.Validate(Valid());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateStepValidator_WithNoOwners_Passes()
    {
        ValidationResult result = _sut.Validate(Valid(owners: []));

        result.IsValid.Should().BeTrue("a step can be created before anyone is assigned");
    }

    [Fact]
    public void CreateStepValidator_WithThreeDistinctOwners_Passes()
    {
        ValidationResult result = _sut.Validate(Valid(owners: [1, 2, 3]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateStepValidator_WithDuplicateOwnerIds_Fails()
    {
        ValidationResult result = _sut.Validate(Valid(owners: [1, 2, 1]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateStepRequest.OwnerIds));
    }

    [Fact]
    public void CreateStepValidator_WithDescriptionOver2000Chars_Fails()
    {
        var request = Valid() with { Description = new string('a', 2001) };

        ValidationResult result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateStepValidator_WithAPdfScreenshot_Fails()
    {
        var pdf = new FileUpload("spec.pdf", "application/pdf", 1024, Stream.Null);

        ValidationResult result = _sut.Validate(Valid(screenshot: pdf));

        result.IsValid.Should().BeFalse();
    }
}

public class FileUploadValidatorTests
{
    private readonly FileUploadValidator _sut = new();

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public void FileUploadValidator_WithAnAllowedType_Passes(string contentType)
    {
        ValidationResult result = _sut.Validate(new FileUpload("shot.png", contentType, 1024, Stream.Null));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/gif")]
    [InlineData("text/html")]
    [InlineData("application/x-msdownload")]
    public void FileUploadValidator_WithADisallowedType_Fails(string contentType)
    {
        ValidationResult result = _sut.Validate(new FileUpload("f", contentType, 1024, Stream.Null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FileUploadValidator_WithExactly5Mb_Passes()
    {
        var file = new FileUpload("shot.png", "image/png", FileUploadValidator.MaxSizeBytes, Stream.Null);

        ValidationResult result = _sut.Validate(file);

        result.IsValid.Should().BeTrue("5 MB is the stated limit, so it must be allowed");
    }

    [Fact]
    public void FileUploadValidator_WithOneByteOver5Mb_Fails()
    {
        var file = new FileUpload("shot.png", "image/png", FileUploadValidator.MaxSizeBytes + 1, Stream.Null);

        ValidationResult result = _sut.Validate(file);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FileUploadValidator_WithAnEmptyFile_Fails()
    {
        ValidationResult result = _sut.Validate(new FileUpload("shot.png", "image/png", 0, Stream.Null));

        result.IsValid.Should().BeFalse();
    }
}

public class SignInValidatorTests
{
    private readonly SignInRequestValidator _sut = new();

    [Fact]
    public void SignInValidator_WithAValidRequest_Passes()
    {
        ValidationResult result = _sut.Validate(new SignInRequest("sarah@company.com", "anything"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SignInValidator_WithAMalformedEmail_Fails()
    {
        ValidationResult result = _sut.Validate(new SignInRequest("nope", "anything"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SignInValidator_WithAShortPassword_Passes()
    {
        ValidationResult result = _sut.Validate(new SignInRequest("sarah@company.com", "x"));

        result.IsValid.Should().BeTrue(
            "the login form must not apply the password policy - that would reveal it " +
            "and lock out anyone whose password predates a policy change");
    }
}

public class ResetPasswordValidatorTests
{
    private readonly ResetPasswordRequestValidator _sut = new();

    private static ResetPasswordRequest Request(string password, string? confirm = null) =>
        new("sarah@company.com", "token-123", password, confirm ?? password);

    [Fact]
    public void ResetPasswordValidator_WithACompliantPassword_Passes()
    {
        ValidationResult result = _sut.Validate(Request("Portal!2026x"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Short!1a")]           // under 10
    [InlineData("alllowercase!1")]     // no uppercase
    [InlineData("ALLUPPERCASE!1")]     // no lowercase
    [InlineData("NoDigitsHere!")]      // no digit
    [InlineData("NoSymbols12345")]     // no symbol
    public void ResetPasswordValidator_WithANonCompliantPassword_Fails(string password)
    {
        ValidationResult result = _sut.Validate(Request(password));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ResetPasswordValidator_WhenConfirmDoesNotMatch_Fails()
    {
        ValidationResult result = _sut.Validate(Request("Portal!2026x", "Portal!2026y"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ResetPasswordRequest.ConfirmPassword));
    }

    [Fact]
    public void ResetPasswordValidator_WithNoToken_Fails()
    {
        var request = new ResetPasswordRequest("sarah@company.com", "", "Portal!2026x", "Portal!2026x");

        ValidationResult result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
    }
}

public class StaffTypeTests
{
    [Fact]
    public void StaffType_HasTheExpectedStoredValues()
    {
        // These numbers go into the database. Renumbering them silently reclassifies
        // every existing row, so pin them.
        ((int)StaffType.Employee).Should().Be(1);
        ((int)StaffType.Management).Should().Be(2);
    }

    [Fact]
    public void ActivityAction_HasTheExpectedStoredValues()
    {
        ((int)ActivityAction.Created).Should().Be(1);
        ((int)ActivityAction.Updated).Should().Be(2);
        ((int)ActivityAction.Deleted).Should().Be(3);
        ((int)ActivityAction.Viewed).Should().Be(4);
        ((int)ActivityAction.LoggedIn).Should().Be(5);
        ((int)ActivityAction.LoggedOut).Should().Be(6);
        ((int)ActivityAction.Uploaded).Should().Be(7);
    }
}
