using UnityEngine;

namespace Yuspec
{
    public static class YuspecDiagnosticReporter
    {
        [HideInCallstack]
        public static void Report(string assetPath, int line, int column, string message)
        {
            string formatted = $"{assetPath}({line},{column}): error YUSPEC: {message}";
            Debug.LogError(formatted);
        }
    }
}
