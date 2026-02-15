using Atlas.Api.DTOs;
using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Tests;

public class AdminCalendarDtoValidationTests
{
    [Fact]
    public void AdminCalendarAvailabilityCellUpsertDto_FailsValidation_ForNegativeRoomsAndPriceOverride()
    {
        var dto = new AdminCalendarAvailabilityCellUpsertDto
        {
            ListingId = 1,
            Date = new DateTime(2025, 1, 1),
            RoomsAvailable = -1,
            PriceOverride = -10m
        };

        var validationResults = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(dto, new ValidationContext(dto), validationResults, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(AdminCalendarAvailabilityCellUpsertDto.RoomsAvailable)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(AdminCalendarAvailabilityCellUpsertDto.PriceOverride)));
    }
}
