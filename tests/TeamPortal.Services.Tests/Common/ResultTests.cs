using FluentValidation;
using TeamPortal.Application.Common;
using TeamPortal.Application.Requests;
using TeamPortal.Application.Validators;

namespace TeamPortal.Services.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_HasNoErrors()
    {
        Result result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Invalid_CarriesTheErrors()
    {
        Result result = Result.Invalid("FullName", "Enter the employee's name.");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ValidationError("FullName", "Enter the employee's name."));
    }

    [Fact]
    public void Invalid_WithNoErrors_Throws()
    {
        // A failed result with nothing to show the user is a bug, not a state.
        Action act = () => Result.Invalid();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenericSuccess_CarriesTheValue()
    {
        Result<int> result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void GenericInvalid_HasNoValue()
    {
        Result<int> result = Result<int>.Invalid("Name", "Required.");

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Be(default);
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public void NotFound_ProducesAFormLevelError()
    {
        Result result = Result.NotFound("That employee");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
            .Which.Field.Should().Be(ValidationError.FormLevel,
                "a missing record belongs to no single field, so it shows above the form");
    }

    [Fact]
    public void ErrorSummary_JoinsEveryMessage()
    {
        Result result = Result.Invalid(
            new ValidationError("Name", "Enter a name."),
            new ValidationError("DepartmentId", "Choose a department."));

        result.ErrorSummary.Should().Contain("Enter a name.").And.Contain("Choose a department.");
    }
}

public class ValidationExtensionsTests
{
    private readonly IValidator<CreateEmployeeRequest> _validator = new CreateEmployeeRequestValidator();

    [Fact]
    public async Task CheckAsync_WhenValid_ReturnsNull()
    {
        var request = new CreateEmployeeRequest("Priya", DepartmentId: 1);

        Result? failure = await _validator.CheckAsync(request);

        failure.Should().BeNull("null means 'carry on', which is what the calling service checks for");
    }

    [Fact]
    public async Task CheckAsync_WhenInvalid_ReturnsAFailedResult()
    {
        var request = new CreateEmployeeRequest("", DepartmentId: 0);

        Result? failure = await _validator.CheckAsync(request);

        failure.Should().NotBeNull();
        failure!.IsFailure.Should().BeTrue();
        failure.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CheckAsync_FlattensIndexedPropertyPaths()
    {
        // FluentValidation reports "OwnerIds[2]" for a collection item. The UI binds
        // the error to a field name, so the index has to be stripped or the message
        // lands on nothing and the user sees a form that fails silently.
        var validator = new CreateStepRequestValidator();
        var request = new CreateStepRequest(1, "Initial Contact", OwnerIds: [1, 2, -5]);

        Result<int>? failure = await validator.CheckAsync<CreateStepRequest, int>(request);

        failure.Should().NotBeNull();
        failure!.Errors.Should().OnlyContain(e => !e.Field.Contains('[', StringComparison.Ordinal));
        failure.Errors.Should().Contain(e => e.Field == nameof(CreateStepRequest.OwnerIds));
    }
}
