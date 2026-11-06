namespace TeamPortal.Application.Dtos;

/// <summary>Who said it. Persisted nowhere - a conversation lives for a visit.</summary>
public enum ChatRole
{
    Person = 1,
    Portal = 2
}

public sealed record ChatMessageDto(ChatRole Role, string Text, DateTime AtUtc)
{
    public IReadOnlyList<ChatSourceDto> Sources { get; init; } = [];

    /// <summary>Screenshots shown with the answer. Usually none.</summary>
    public IReadOnlyList<ChatImageDto> Images { get; init; } = [];
}

/// <summary>
/// A screenshot worth showing beside an answer, and the step it belongs to.
///
/// The URL is always resolved from the portal's own record, never taken from
/// anything a model wrote. A model asked to produce a link will happily produce
/// a plausible one that points nowhere; it may only NAME a step, and the name is
/// looked up here.
/// </summary>
public sealed record ChatImageDto(
    string ProcessName,
    string StepName,
    string FileName,
    string Url);

/// <summary>
/// A process the answer was drawn from, and where to find it. <c>FolderId</c>
/// and <c>ProcessId</c> together make the route the UI links to.
/// </summary>
public sealed record ChatSourceDto(
    int FolderId,
    string FolderName,
    int ProcessId,
    string ProcessName);

public sealed record ChatReplyDto(string Text, IReadOnlyList<ChatSourceDto> Sources)
{
    public IReadOnlyList<ChatImageDto> Images { get; init; } = [];
}
