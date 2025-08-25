using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace dotnet;

internal static class Helpers
{
    public static void InitLogger()
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithThreadId()
            // Ref: https://github.com/serilog/serilog/wiki/Enrichment
            .Enrich.FromLogContext()
            .WriteTo.Async(configuration => configuration.Console(
                new ExpressionTemplate(
                    theme: TemplateTheme.Code,
                    // Ref: https://github.com/serilog/serilog-expressions
                    template: "[{@t:HH:mm:ss.fff}] {@l:u3}" +
                              "{#if EventName is not null} {EventName}{#end}" +
                              "{#if ThreadId is not null} TID:{ThreadId}{#end}" +
                              " {@m}\n"
                              // LogContext properties from LogEvent enrichment above,
                              // excluding those referenced in message
                              // "- OtherContext:{Rest(true)}\n\n"
                )));

        Log.Logger = loggerConfiguration.CreateLogger();
    }
}