using TypedPaths.Generator.Utils;

using Xunit;

namespace TypedPaths.Generator.Tests;

public class UtilTests
{
    [Theory]
    [InlineData(".gitignore", "Gitignore")]
    [InlineData("a.config", "A")]
    [InlineData("123data.json", "Data")]
    [InlineData("my-file-name.txt", "MyFileName")]
    [InlineData("user_profile_icon.png", "UserProfileIcon")]
    [InlineData("user_profile_icon", "UserProfileIcon")]
    [InlineData("LICENCE", "LICENCE")]
    public void GetSafeClassName_ShouldReturnExpectedPascalCase(string input, string expected)
    {
        // Act
        string result = PascalConverter.GetSafeClassName(input);

        // Assert
        Assert.Equal(expected, result);
    }
}