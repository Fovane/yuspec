using System;

namespace Yuspec.Unity
{
    public enum YuspecDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    [Serializable]
    public sealed class YuspecDiagnostic
    {
        public YuspecDiagnosticSeverity severity;
        public string code;
        public string message;
        public string source;
        public int line;
        public int column;

        public YuspecDiagnostic(
            YuspecDiagnosticSeverity severity,
            string code,
            string message,
            string source = "",
            int line = 0,
            int column = 0)
        {
            this.severity = severity;
            this.code = code;
            this.message = message;
            this.source = source;
            this.line = line;
            this.column = column;
        }

        public override string ToString()
        {
            var location = string.IsNullOrEmpty(source) ? string.Empty : $"{source}:{line}:{column}: ";
            return $"{location}{severity} {code}: {message}";
        }
    }
}
