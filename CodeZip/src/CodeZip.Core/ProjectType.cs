namespace CodeZip.Core;

/// <summary>
/// Represents the types of projects that can be detected and processed.
/// </summary>
[Flags]
public enum ProjectType
{
    None = 0,
    Delphi = 1 << 0,
    CSharp = 1 << 1,
    React = 1 << 2,
    Angular = 1 << 3,
    Node = 1 << 4,
    TypeScript = 1 << 5,
    Generic = 1 << 6
}
