using System.IO.Pipes;

namespace MdEditor.Services;

/// <summary>
/// 单实例 IPC：第一个进程作为服务端监听命名管道，接收后续进程转发的文件路径。
/// </summary>
internal sealed class IpcServer
{
    private readonly Action<string> _onPathReceived;

    public IpcServer(Action<string> onPathReceived) => _onPathReceived = onPathReceived;

    /// <summary>在后台线程启动监听。</summary>
    public void Start()
    {
        var thread = new Thread(Loop) { IsBackground = true, Name = "MdEdit.Ipc" };
        thread.Start();
    }

    private void Loop()
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    AppConstants.IpcPipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                server.WaitForConnection();

                using var reader = new StreamReader(server);
                var line = reader.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _onPathReceived(line);
                }
            }
            catch
            {
                Thread.Sleep(300);
            }
        }
    }
}

/// <summary>
/// 单实例 IPC 客户端：把启动参数转发给已运行的实例。
/// </summary>
internal static class IpcClient
{
    public static void Send(string? path)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", AppConstants.IpcPipeName, PipeDirection.Out);
                client.Connect(1000);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine(path ?? string.Empty);
                return;
            }
            catch
            {
                Thread.Sleep(300);
            }
        }
    }
}
