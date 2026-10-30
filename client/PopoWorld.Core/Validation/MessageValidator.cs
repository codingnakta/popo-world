using PopoWorld.Core.Models;

namespace PopoWorld.Core.Validation;

public enum MessageError
{
    None,
    Empty,
    TooLong,
    TooManyLines,
}

/// <summary>메시지 본문 검사: 1~200자, 최대 3줄.</summary>
public static class MessageValidator
{
    public static MessageError Validate(string? body)
    {
        if (body is null) return MessageError.Empty;
        var normalized = Normalize(body);
        if (normalized.Length == 0) return MessageError.Empty;
        if (CountChars(normalized) > PopoRules.MessageMaxLength) return MessageError.TooLong;
        if (CountLines(normalized) > PopoRules.MessageMaxLines) return MessageError.TooManyLines;
        return MessageError.None;
    }

    public static bool IsValid(string? body) => Validate(body) == MessageError.None;

    /// <summary>줄 끝을 \n으로 통일하고 앞뒤 공백을 없앤다. 서버에는 이 형태로 보낸다.</summary>
    public static string Normalize(string body) => body.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    /// <summary>서버의 char_length 와 같은 기준 (코드 포인트 수).</summary>
    public static int CountChars(string s)
    {
        var n = 0;
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(s);
        while (e.MoveNext()) n++;
        return n;
    }

    public static int CountLines(string s) => s.Length == 0 ? 0 : s.Count(c => c == '\n') + 1;

    public static string Describe(MessageError e) => e switch
    {
        MessageError.None => "",
        MessageError.Empty => "내용을 입력하세요",
        MessageError.TooLong => $"{PopoRules.MessageMaxLength}자까지 보낼 수 있어요",
        MessageError.TooManyLines => $"{PopoRules.MessageMaxLines}줄까지 보낼 수 있어요",
        _ => "보낼 수 없어요",
    };
}
