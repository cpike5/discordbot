using DiscordBot.Core.Authorization;
using FluentAssertions;

namespace DiscordBot.Tests.Core.Authorization;

/// <summary>
/// Unit tests for the Roles constants class.
/// </summary>
public class RolesTests
{
    [Fact]
    public void SuperAdmin_HasCorrectValue()
    {
        // Arrange & Act
        var role = Roles.SuperAdmin;

        // Assert
        role.Should().Be("SuperAdmin", "SuperAdmin role name should match the constant");
    }

    [Fact]
    public void Admin_HasCorrectValue()
    {
        // Arrange & Act
        var role = Roles.Admin;

        // Assert
        role.Should().Be("Admin", "Admin role name should match the constant");
    }

    [Fact]
    public void Moderator_HasCorrectValue()
    {
        // Arrange & Act
        var role = Roles.Moderator;

        // Assert
        role.Should().Be("Moderator", "Moderator role name should match the constant");
    }

    [Fact]
    public void Viewer_HasCorrectValue()
    {
        // Arrange & Act
        var role = Roles.Viewer;

        // Assert
        role.Should().Be("Viewer", "Viewer role name should match the constant");
    }

    [Fact]
    public void All_ContainsAllRoles()
    {
        // Arrange & Act
        var allRoles = Roles.All;

        // Assert
        allRoles.Should().HaveCount(4, "there should be exactly 4 roles");
        allRoles.Should().Contain(Roles.SuperAdmin, "All should contain SuperAdmin");
        allRoles.Should().Contain(Roles.Admin, "All should contain Admin");
        allRoles.Should().Contain(Roles.Moderator, "All should contain Moderator");
        allRoles.Should().Contain(Roles.Viewer, "All should contain Viewer");
    }

    [Fact]
    public void All_ContainsOnlyDefinedRoles()
    {
        // Arrange & Act
        var allRoles = Roles.All;
        var expectedRoles = new[] { Roles.SuperAdmin, Roles.Admin, Roles.Moderator, Roles.Viewer };

        // Assert
        allRoles.Should().BeEquivalentTo(expectedRoles,
            "All should contain exactly the defined roles, no more, no less");
    }

    [Fact]
    public void All_RoleNamesAreUnique()
    {
        // Arrange & Act
        var allRoles = Roles.All;
        var distinctRoles = allRoles.Distinct();

        // Assert
        distinctRoles.Should().HaveCount(allRoles.Length,
            "all role names should be unique with no duplicates");
    }

    [Fact]
    public void All_NoRoleIsNullOrEmpty()
    {
        // Arrange & Act
        var allRoles = Roles.All;

        // Assert
        allRoles.Should().NotContainNulls("no role should be null");
        allRoles.Should().NotContain(string.Empty, "no role should be empty");
        allRoles.Should().AllSatisfy(role => role.Should().NotBeNullOrWhiteSpace(),
            "all roles should have non-empty values");
    }

    [Fact]
    public void All_RolesAreInExpectedOrder()
    {
        // Arrange & Act
        var allRoles = Roles.All;

        // Assert
        allRoles[0].Should().Be(Roles.SuperAdmin, "first role should be SuperAdmin");
        allRoles[1].Should().Be(Roles.Admin, "second role should be Admin");
        allRoles[2].Should().Be(Roles.Moderator, "third role should be Moderator");
        allRoles[3].Should().Be(Roles.Viewer, "fourth role should be Viewer");
    }
}
