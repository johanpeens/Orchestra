using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Validators;
using TeamPortal.Infrastructure.Data;
using TeamPortal.Infrastructure.Tests.Common;
using TeamPortal.Mock;

namespace TeamPortal.Infrastructure.Tests;

public class SeederTests
{
    [Fact]
    public async Task Seeder_OnAnEmptyDatabase_FillsTheReferenceTables()
    {
        await using var fixture = new DatabaseFixture();

        await using (PortalDbContext db = fixture.NewContext())
        {
            await DbSeeder.SeedAsync(db);
        }

        await using PortalDbContext verify = fixture.NewContext();

        (await verify.StepStatuses.CountAsync()).Should().Be(4);
        (await verify.Departments.CountAsync()).Should().Be(7);
        (await verify.Employees.CountAsync()).Should().Be(8);
        (await verify.ProcessFolders.CountAsync()).Should().Be(2);
        (await verify.Processes.CountAsync()).Should().Be(7);
    }

    [Fact]
    public async Task Seeder_RunTwice_DoesNotDuplicateRows()
    {
        // It runs on every start-up in DEV. If it were not idempotent the demo
        // data would double every time somebody pressed F5.
        await using var fixture = new DatabaseFixture();

        await using (PortalDbContext first = fixture.NewContext())
        {
            await DbSeeder.SeedAsync(first);
        }

        await using (PortalDbContext second = fixture.NewContext())
        {
            await DbSeeder.SeedAsync(second);
        }

        await using PortalDbContext verify = fixture.NewContext();

        (await verify.StepStatuses.CountAsync()).Should().Be(4);
        (await verify.Departments.CountAsync()).Should().Be(7);
        (await verify.Employees.CountAsync()).Should().Be(8);
        (await verify.ProcessFolders.CountAsync()).Should().Be(2);
        (await verify.Processes.CountAsync()).Should().Be(7);
        (await verify.ProcessSteps.CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task Seeder_ExactlyOneStepStatus_IsTheDefault()
    {
        // Every new step and process picks this one up. Two defaults, or none,
        // and the pick becomes arbitrary.
        await using var fixture = new DatabaseFixture();

        await using (PortalDbContext db = fixture.NewContext())
        {
            await DbSeeder.SeedAsync(db);
        }

        await using PortalDbContext verify = fixture.NewContext();

        (await verify.StepStatuses.CountAsync(s => s.IsDefault)).Should().Be(1);
    }

    [Fact]
    public async Task Seeder_TheFirstProcess_HasItsFourSteps()
    {
        await using var fixture = new DatabaseFixture();

        await using (PortalDbContext db = fixture.NewContext())
        {
            await DbSeeder.SeedAsync(db);
        }

        await using PortalDbContext verify = fixture.NewContext();

        IReadOnlyList<int> numbers =
        [
            .. await verify.ProcessSteps
                .OrderBy(s => s.StepNumber)
                .Select(s => s.StepNumber)
                .ToListAsync()
        ];

        numbers.Should().Equal([1, 2, 3, 4], "the flow strip numbers 1..n with no gaps");
    }

    [Fact]
    public async Task Seeder_ProducesTheSameDepartmentsAsTheMockStore()
    {
        // The seam only holds if both sides start from the same world.
        await using var fixture = new DatabaseFixture();

        await using (PortalDbContext db = fixture.NewContext())
        {
            await DbSeeder.SeedAsync(db);
        }

        IReadOnlyList<DepartmentListItemDto> fromMock = await MockDepartmentsAsync();

        await using PortalDbContext verify = fixture.NewContext();

        IReadOnlyList<string> seeded =
        [
            .. await verify.Departments.OrderBy(d => d.Name).Select(d => d.Name).ToListAsync()
        ];

        seeded.Should().Equal([.. fromMock.Select(d => d.Name).Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public async Task Seeder_ProducesTheSameEmployeesAsTheMockStore()
    {
        await using var fixture = new DatabaseFixture();

        await using (PortalDbContext db = fixture.NewContext())
        {
            await DbSeeder.SeedAsync(db);
        }

        IReadOnlyList<EmployeeCardDto> fromMock = await MockEmployeesAsync();

        await using PortalDbContext verify = fixture.NewContext();

        IReadOnlyList<string> seeded =
        [
            .. await verify.Employees.OrderBy(e => e.FullName).Select(e => e.FullName).ToListAsync()
        ];

        seeded.Should().Equal([.. fromMock.Select(e => e.FullName).Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public async Task Seeder_PutsEachPersonInTheSameDepartmentAsTheMockStore()
    {
        // Matching names is not enough - the org chart has to match too.
        await using var fixture = new DatabaseFixture();

        await using (PortalDbContext db = fixture.NewContext())
        {
            await DbSeeder.SeedAsync(db);
        }

        IReadOnlyList<EmployeeCardDto> fromMock = await MockEmployeesAsync();

        await using PortalDbContext verify = fixture.NewContext();

        List<Placement> seeded = await verify.Employees
            .OrderBy(e => e.FullName)
            .Select(e => new Placement(e.FullName, e.Department!.Name))
            .ToListAsync();

        IReadOnlyList<Placement> expected =
        [
            .. fromMock
                .OrderBy(e => e.FullName, StringComparer.Ordinal)
                .Select(e => new Placement(e.FullName, e.DepartmentName))
        ];

        seeded.Should().Equal(expected);
    }

    [Fact]
    public async Task SeededDepartments_UseOnlyColoursThePickerOffers()
    {
        // Underwriting was Purple80 and Corporate was Neutral60, and neither is
        // one of the eight swatches in the Edit Department modal. Opening that
        // modal on either showed NO colour selected, and the tag on the process
        // card was a colour nobody could choose again once they changed it.
        await using var fixture = new DatabaseFixture();

        await using (PortalDbContext db = fixture.NewContext())
        {
            await DbSeeder.SeedAsync(db);
        }

        await using PortalDbContext verify = fixture.NewContext();

        IReadOnlyList<string> offPalette =
        [
            .. await verify.Departments
                .Where(d => !BrandPalette.DepartmentSwatches.Contains(d.ColourHex))
                .Select(d => $"{d.Name} ({d.ColourHex})")
                .ToListAsync()
        ];

        offPalette.Should().BeEmpty(
            "every department colour has to be one the picker can produce");
    }

    [Fact]
    public void TheDefaultDepartmentColour_IsOneOfTheSwatches()
    {
        // A new department starts on this. If it is off-palette, every department
        // anyone creates is off-palette from the moment it exists.
        BrandPalette.DepartmentSwatches.Should().Contain(BrandPalette.DefaultDepartmentColour);
    }

    [Fact]
    public void TheSwatches_AreAllDistinct()
    {
        // Two identical swatches mean one of them can never be selected - the
        // picker compares by value.
        BrandPalette.DepartmentSwatches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Should().HaveCount(BrandPalette.DepartmentSwatches.Count);
    }

    [Fact]
    public void TheSwatches_AreUppercaseSixDigitHex()
    {
        // The column constraint and the validator both expect that shape, so a
        // lowercase swatch would be offered and then refused on save.
        BrandPalette.DepartmentSwatches.Should().OnlyContain(
            c => System.Text.RegularExpressions.Regex.IsMatch(
                c, FieldRules.ColourHexPattern, System.Text.RegularExpressions.RegexOptions.None,
                TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    /// Spins up the Part A mock behind its real DI registrations, so this compares
    /// against what the app actually ran rather than against a copy of the seed list.
    /// </summary>
    private static ServiceProvider BuildMockProvider() =>
        new ServiceCollection()
            .AddMockData(o =>
            {
                o.ReadDelay = TimeSpan.Zero;
                o.WriteDelay = TimeSpan.Zero;
            })
            .BuildServiceProvider();

    private static async Task<IReadOnlyList<DepartmentListItemDto>> MockDepartmentsAsync()
    {
        await using ServiceProvider provider = BuildMockProvider();
        using IServiceScope scope = provider.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IDepartmentService>()
            .GetWithCountsAsync();
    }

    private static async Task<IReadOnlyList<EmployeeCardDto>> MockEmployeesAsync()
    {
        await using ServiceProvider provider = BuildMockProvider();
        using IServiceScope scope = provider.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IEmployeeService>()
            .GetAllAsync();
    }

    /// <summary>One person and the department they sit in.</summary>
    private sealed record Placement(string Person, string Department);
}
