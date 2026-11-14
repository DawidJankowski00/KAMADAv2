using Kamada.Modules.Workforce.Application.Auth;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Workforce.UnitTests;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("password1234")]
    [InlineData("  QWERTYUIOP123  ")]
    [InlineData("Kamada123456")]
    [InlineData("############")]
    public void IsCommon_BuiltInListAndRepeatedCharacters_True(string password)
    {
        new PasswordPolicy(Options.Create(new PasswordPolicyOptions())).IsCommon(password).ShouldBeTrue();
    }

    [Fact]
    public void IsCommon_LongUnusualPassphrase_False()
    {
        new PasswordPolicy(Options.Create(new PasswordPolicyOptions())).IsCommon("zielony rower na dachu").ShouldBeFalse();
    }

    [Fact]
    public void IsCommon_ExtraListFromConfiguration_IsAdded()
    {
        var file = Path.GetTempFileName();
        File.WriteAllLines(file, ["# extra", "correct horse battery staple"]);
        try
        {
            var policy = new PasswordPolicy(Options.Create(new PasswordPolicyOptions { BlocklistFile = file }));

            policy.IsCommon("Correct Horse Battery Staple").ShouldBeTrue();
            policy.IsCommon("password1234").ShouldBeTrue();
        }
        finally
        {
            File.Delete(file);
        }
    }
}
