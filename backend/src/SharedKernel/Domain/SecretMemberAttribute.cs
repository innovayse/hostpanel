namespace Innovayse.Hostpanel.SharedKernel.Domain;

/// <summary>
/// Marks a property as holding a secret whose name the naming rule in <see cref="SecretMembers"/>
/// cannot catch (a one-time code, a link that carries a token, a key called only <c>Key</c>), so a
/// record's printed form masks it. On a positional record put it on the parameter as
/// <c>[property: SecretMember]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class SecretMemberAttribute : Attribute
{
}
