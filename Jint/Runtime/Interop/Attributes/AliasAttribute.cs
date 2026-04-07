namespace Jint.Runtime.Interop.Attributes;

[AttributeUsage(AttributeTargets.All)]
public class AliasAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
