using System;
using System.IO;
using System.Text;

namespace CvrProxyChainPatcher
{
    public static class ConsoleHelper
    {
        public static void WriteColor(string text, ConsoleColor color)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = old;
        }

        public static void WriteLineColor(string text, ConsoleColor color)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = old;
        }

        public static void LogInfo(string msg)
        {
            WriteColor("[INFO] ", ConsoleColor.Cyan);
            Console.WriteLine(msg);
        }

        public static void LogSuccess(string msg)
        {
            WriteColor("[SUCCESS] ", ConsoleColor.Green);
            Console.WriteLine(msg);
        }

        public static void LogWarn(string msg)
        {
            WriteColor("[WARN] ", ConsoleColor.Yellow);
            Console.WriteLine(msg);
        }

        public static void LogError(string msg)
        {
            WriteColor("[ERROR] ", ConsoleColor.Red);
            Console.WriteLine(msg);
        }

        public static void WriteHeader(string title)
        {
            WriteLineColor("==================================================", ConsoleColor.DarkGray);
            WriteColor("  " + title + "\n", ConsoleColor.Cyan);
            WriteLineColor("==================================================", ConsoleColor.DarkGray);
        }

        public static void WriteDivider()
        {
            WriteLineColor("--------------------------------------------------", ConsoleColor.DarkGray);
        }

        public static void WriteMenuItem(string key, string title, string hint = null, bool isSecondary = false)
        {
            if (isSecondary)
            {
                WriteColor("  [" + key + "] ", ConsoleColor.DarkGray);
                WriteLineColor(title + (!string.IsNullOrEmpty(hint) ? " " + hint : ""), ConsoleColor.DarkGray);
            }
            else
            {
                WriteColor("  [" + key + "] ", ConsoleColor.Cyan);
                WriteColor(title, ConsoleColor.White);
                if (!string.IsNullOrEmpty(hint))
                {
                    WriteColor(" " + hint, ConsoleColor.DarkGray);
                }
                Console.WriteLine();
            }
        }

        public static void LogHeader(string msg)
        {
            Console.WriteLine();
            WriteHeader(msg);
            Console.WriteLine();
        }

        public static string NormalizeLineEndings(string text)
        {
            return (text ?? "").Replace("\r\n", "\n");
        }

        public static string DetectLineEnding(string text)
        {
            return (text != null && text.Contains("\r\n")) ? "\r\n" : "\n";
        }

        public static string ApplyLineEnding(string text, string eol)
        {
            return NormalizeLineEndings(text).Replace("\n", eol);
        }

        public static bool PromptYesNo(string question, bool defaultYes)
        {
            string suffix = defaultYes ? " [Y/n]: " : " [y/N]: ";
            WriteColor(question + suffix, ConsoleColor.Cyan);
            string input = Console.ReadLine();
            if (string.IsNullOrEmpty(input)) return defaultYes;
            input = input.Trim().ToLowerInvariant();
            if (input == "y" || input == "yes") return true;
            if (input == "n" || input == "no") return false;
            return defaultYes;
        }

        public static string PromptInput(string prompt, string defaultValue)
        {
            if (!string.IsNullOrEmpty(defaultValue))
                WriteColor(prompt + " [" + defaultValue + "]: ", ConsoleColor.Cyan);
            else
                WriteColor(prompt + ": ", ConsoleColor.Cyan);

            string input = Console.ReadLine();
            if (string.IsNullOrEmpty(input)) return defaultValue;
            return input.Trim('"', ' ', '\'');
        }

        public static string FormatFileSize(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1024)
                return string.Format("{0:F2} GB", (double)bytes / (1024 * 1024 * 1024));
            if (bytes >= 1024 * 1024)
                return string.Format("{0:F2} MB", (double)bytes / (1024 * 1024));
            if (bytes >= 1024)
                return string.Format("{0:F2} KB", (double)bytes / 1024);
            return bytes + " B";
        }
    }
}
