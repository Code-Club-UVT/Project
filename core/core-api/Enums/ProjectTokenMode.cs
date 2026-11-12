namespace CoreApi.Enums;

public enum ProjectTokenMode
{
    /// <summary>The gateway checks access and swaps the edge token for a project token.</summary>
    Exchange,

    /// <summary>The gateway forwards the edge token unchanged (core API only).</summary>
    PassThrough
}
