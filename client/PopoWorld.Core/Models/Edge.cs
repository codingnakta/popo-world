namespace PopoWorld.Core.Models;

/// <summary>캐릭터 띠가 붙는 모니터 변.</summary>
public enum Edge
{
    Bottom,
    Top,
    Left,
    Right,
}

public static class EdgeExtensions
{
    public static Edge ParseEdge(string? value) => value?.ToLowerInvariant() switch
    {
        "top" => Edge.Top,
        "left" => Edge.Left,
        "right" => Edge.Right,
        _ => Edge.Bottom,
    };

    public static string ToKey(this Edge edge) => edge switch
    {
        Edge.Top => "top",
        Edge.Left => "left",
        Edge.Right => "right",
        _ => "bottom",
    };

    /// <summary>좌·우 변이면 띠가 세로로 선다.</summary>
    public static bool IsVertical(this Edge edge) => edge is Edge.Left or Edge.Right;
}
