using GymBackend.Application.Auth;

namespace GymBackend.UnitTests.Auth;

public sealed class InputRulesTests
{
    [Theory]
    [InlineData("José Núñez", true)]
    [InlineData("María-José O'Higgins", true)]
    [InlineData("Jose\u0301 O’Brien.", true)]
    [InlineData("Test 12345678", true)]
    [InlineData("𐐀a", true)]
    [InlineData("", false)]
    [InlineData("A", false)]
    [InlineData("<script>alert(1)</script>", false)]
    [InlineData("Juan\" onmouseover=\"x", false)]
    [InlineData("A&B", false)]
    [InlineData("A/B", false)]
    [InlineData("A\\B", false)]
    [InlineData("A=B", false)]
    [InlineData("A`B", false)]
    [InlineData("A{B}", false)]
    [InlineData("A😀B", false)]
    [InlineData("A\nB", false)]
    [InlineData("A\tB", false)]
    [InlineData("A\0B", false)]
    [InlineData("A\u007fB", false)]
    [InlineData("A\u200bB", false)]
    public void IsSafeName_AllowedCharacters(string name, bool expected) =>
        Assert.Equal(expected, InputRules.IsSafeName(name));

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void IsSafeName_LengthLimits(int length, bool expected) =>
        Assert.Equal(expected, InputRules.IsSafeName(new string('a', length)));

    [Theory]
    [InlineData("user@example.test", true)]
    [InlineData("maria.jose+gym@x.cl", true)]
    [InlineData("", false)]
    [InlineData("a<b>@x.cl", false)]
    [InlineData("no-es-correo", false)]
    [InlineData("User <user@example.test>", false)]
    [InlineData("User user@example.test", false)]
    [InlineData(" user@example.test ", false)]
    [InlineData("a\r\nb@x.cl", false)]
    [InlineData("a\tb@x.cl", false)]
    [InlineData("a\0b@x.cl", false)]
    [InlineData("a\u007fb@x.cl", false)]
    public void IsValidEmail_Format(string email, bool expected) =>
        Assert.Equal(expected, InputRules.IsValidEmail(email));

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void IsValidEmail_LengthLimits(int length, bool expected)
    {
        var email = new string('a', 64) + "@" + new string('b', 63) + "." +
            new string('c', 63) + "." + new string('d', length - 196) + ".cl";
        Assert.Equal(length, email.Length);
        Assert.Equal(expected, InputRules.IsValidEmail(email));
    }
}
