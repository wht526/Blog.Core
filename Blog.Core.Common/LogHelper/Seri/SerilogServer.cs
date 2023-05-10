using Blog.Core.Common.Helper;
using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace Blog.Core.Common.LogHelper
{
    public class SerilogServer
    {
        /// <summary>
        /// 记录日常日志
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="message"></param>
        /// <param name="info"></param>
        public static void WriteLog(string filename, string[] dataParas, bool IsHeader = true, string defaultFolder = "", bool isJudgeJsonFormat = false)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                //.WriteTo.File(Path.Combine($"log/Serilog/{filename}/", ".log"), rollingInterval: RollingInterval.Day, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}")
                .WriteTo.File(Path.Combine("Log", defaultFolder, $"{filename}.log"),
                    rollingInterval: RollingInterval.Infinite,
                    outputTemplate: "{Message}{NewLine}{Exception}")
                .CreateLogger();

            var now = DateTime.Now;
            string logContent = String.Join("\r\n", dataParas);
            var isJsonFormat = true;
            if (isJudgeJsonFormat)
            {
                var judCont = logContent.Substring(0, logContent.LastIndexOf(","));
                isJsonFormat = JsonHelper.IsJson(judCont);
            }

            if (isJsonFormat)
            {
                if (IsHeader)
                {
                    logContent = (
                       "--------------------------------\r\n" +
                       DateTime.Now + "|\r\n" +
                       String.Join("\r\n", dataParas) + "\r\n"
                       );
                }
                // 展示elk支持输出4种日志级别
                Log.Information(logContent);
                //Log.Warning(logContent);
                //Log.Error(logContent);
                //Log.Debug(logContent);
            }
            else
            {
                Console.WriteLine("【JSON格式异常：】"+logContent + now.ObjToString());
            }
            Log.CloseAndFlush();
        }
        /// <summary>
        /// 记录异常日志
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        public static void WriteErrorLog(string filename, string message, Exception ex)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                .WriteTo.File(Path.Combine($"log/Error/{filename}/", ".txt"), rollingInterval: RollingInterval.Day)
                .CreateLogger();
            Log.Error(ex, message);
            Log.CloseAndFlush();
        }
    }
}
