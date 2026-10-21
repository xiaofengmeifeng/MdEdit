using MdEditor.Forms;
using MdEditor.Services;

namespace MdEditor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);

        var startupFile = args.Length > 0 ? args[0] : null;

        // 单实例：若已有实例在运行，把文件路径转发过去后退出。
        using var mutex = new Mutex(true, AppConstants.SingleInstanceMutexName, out var isNewInstance);
        if (!isNewInstance)
        {
            IpcClient.Send(startupFile);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(startupFile));
    }

    private static void LogCrash(Exception? exception)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "mdedit-crash.txt"),
                DateTime.Now.ToString("s") + Environment.NewLine + exception);
        }
        catch
        {
            // 忽略日志写入失败
        }
    }
}
