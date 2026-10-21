using System.Text.Json;
using MdEditor.Models;

namespace MdEditor.Services;

/// <summary>
/// 会话记录的持久化（记住上次打开的文档与文件夹）。
/// </summary>
internal static class SessionService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>保存会话。</summary>
    public static void Save(SessionData data)
    {
        try
        {
            AppPaths.EnsureDataDirectory();
            File.WriteAllText(AppPaths.SessionFile, JsonSerializer.Serialize(data, Options));
        }
        catch
        {
            // 忽略持久化失败
        }
    }

    /// <summary>读取会话；不存在或损坏时返回 <c>null</c>。</summary>
    public static SessionData? Load()
    {
        try
        {
            var file = AppPaths.SessionFile;
            if (!File.Exists(file))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SessionData>(File.ReadAllText(file));
        }
        catch
        {
            return null;
        }
    }
}
