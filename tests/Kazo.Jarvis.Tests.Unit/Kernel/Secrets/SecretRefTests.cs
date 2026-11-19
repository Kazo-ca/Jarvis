using Kazo.Jarvis.Kernel.Secrets;

namespace Kazo.Jarvis.Tests.Unit.Kernel.Secrets;

/// <summary>Secret references and values (ADR 0004, section 4).</summary>
[TestFixture]
public sealed class SecretRefTests
{
    [TestCase("a")]
    [TestCase("llm.anthropic.apikey")]
    [TestCase("voice-azure_key.2")]
    public void A_valid_reference_is_accepted(string value) =>
        Assert.That(new SecretRef(value).ToString(), Is.EqualTo(value));

    [TestCase("")]
    [TestCase("with space")]
    [TestCase("slash/inside")]
    [TestCase("accentué")]
    [TestCase("star*")]
    public void An_invalid_reference_is_refused(string value)
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new SecretRef(value), Throws.ArgumentException);
            Assert.That(SecretRef.TryParse(value, out _), Is.False);
        });
    }

    [Test]
    public void A_reference_has_at_most_100_characters()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SecretRef.IsValid(new string('a', SecretRef.MaximumLength)), Is.True);
            Assert.That(SecretRef.IsValid(new string('a', SecretRef.MaximumLength + 1)), Is.False);
            Assert.That(SecretRef.IsValid(null), Is.False);
        });
    }

    [Test]
    public void A_secret_value_never_shows_in_text()
    {
        var value = new SecretValue("sk-very-secret");

        Assert.Multiple(() =>
        {
            Assert.That(value.ToString(), Does.Not.Contain("sk-very-secret"));
            Assert.That($"key: {value}", Does.Not.Contain("sk-very-secret"));
            Assert.That(value.Reveal(), Is.EqualTo("sk-very-secret"));
        });
    }

    [Test]
    public void A_secret_value_cannot_be_empty() =>
        Assert.That(() => new SecretValue(string.Empty), Throws.ArgumentException);

    [Test]
    public void Two_values_with_the_same_secret_are_equal()
    {
        var value = new SecretValue("abc");
        var same = new SecretValue("abc");

        Assert.That(value, Is.EqualTo(same).And.Not.EqualTo(new SecretValue("abd")));
    }

    [Test]
    public void The_vault_section_defaults_to_the_credential_manager() =>
        Assert.That(VaultSettings.Section.CreateDefault().Kind, Is.EqualTo(VaultKind.CredentialManager));

    [Test]
    public void The_vault_section_refuses_an_unknown_vault() =>
        Assert.That(VaultSettings.Section.Validate(new VaultSettings { Kind = (VaultKind)42 }), Is.Not.Empty);
}
