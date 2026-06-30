using cpms_Application.Authorization;
using Xunit;

namespace cpms_Application.Tests;

public class AuthServiceTests
{
    [Fact]
    public void NormalizeRole_UsesDefaultCustomer_WhenRoleIsUnknown()
    {
        var normalized = AppRoles.Normalize("UnknownRole");

        Assert.Equal(AppRoles.Customer, normalized);
    }

    [Fact]
    public void IsValidRole_ReturnsTrue_ForSupportedRoles()
    {
        Assert.True(AppRoles.IsValid(AppRoles.Admin));
        Assert.True(AppRoles.IsValid(AppRoles.WarehouseManager));
        Assert.True(AppRoles.IsValid(AppRoles.ProjectManager));
        Assert.True(AppRoles.IsValid(AppRoles.Customer));
    }
}
