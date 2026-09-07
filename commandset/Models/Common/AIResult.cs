namespace RevitMCPCommandSet.Models.Common;

public class AIResult<T>
{
    /// <summary>
    ///     是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     消息
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    ///     返回数据
    /// </summary>
    public T Response { get; set; }

    /// <summary>
    ///     非致命提示：元素已创建，但发生了调用方看不见的替换、就近匹配或重叠。
    ///     Non-fatal notes — the element WAS created, but something the caller
    ///     could not otherwise see happened to it (a defaulted type, an inexact
    ///     level match, an ignored argument, an overlap with existing geometry).
    ///     These used to exist only as prose inside <see cref="Message"/>, so a
    ///     caller had to parse text to find them and, in practice, did not.
    /// </summary>
    public List<string> Warnings { get; set; }

    /// <summary>
    ///     致命项：该条目未创建任何元素。
    ///     Entries that produced NO element. Kept apart from
    ///     <see cref="Warnings"/> because "built, but check it" and "not built at
    ///     all" call for different reactions and used to be indistinguishable.
    /// </summary>
    public List<string> Failures { get; set; }
}