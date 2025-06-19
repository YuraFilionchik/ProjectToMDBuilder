using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var builder = new ProjectToMdBuilder();
        builder.Run();
    }
}

/// <summary>
/// Generates a Markdown documentation file for a given C# project.
/// Supports processing local project directories and cloning public GitHub repositories.
/// Features include project structure generation, file content inclusion,
/// configurable exclusions, and history of processed paths.
/// </summary>
class ProjectToMdBuilder
{
    // Lists of directories, file extensions, and specific file names to exclude from processing.
    private readonly List<string> _excludedDirs = new List<string>
    {
        ".git", ".github", "bin", "obj", "node_modules", "packages", "dist", "wwwroot",
        ".vs", ".cr", ".vscode", ".idea", "TestResults", "coverage", "artifacts",
        "docs", "images", "resources"
    };

    private readonly List<string> _excludedExtensions = new List<string>
    {
        ".exe", ".dll", ".pdb", ".suo", ".user", ".cache", ".zip", ".pdf",
        ".snk", ".pfx", ".cer", ".sln", ".userprefs", ".lock.json", ".dockerignore",
        ".gitattributes", ".gitignore", "sql", ".md", ".txt", ".log", ".tmp", ".bak", ".swp",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".rar", ".7z", ".tar", ".gz",
        ".mp3", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".iso", ".class", ".jar"
    };

    private readonly List<string> _excludedFiles = new List<string>
    {
        "launchsettings.json", "appsettings.json", "appsettings.development.json",
        "appsettings.production.json", "appsettings.staging.json", "web.config"
    };

    private const string HistoryFile = "Path_history.txt";
    private string _tempClonedRepoPath = null; // Stores the path of a temporarily cloned repository for cleanup.

    /// <summary>
    /// Main method to run the documentation generation process.
    /// Handles user input, GitHub cloning (if applicable), project building, and cleanup.
    /// </summary>
    public void Run()
    {
        string originalUserInputPath = null;
        try
        {
            Console.WriteLine("=== Генератор документации проекта ===");
            originalUserInputPath = ChooseProjectPath(); // Get the raw user input
            string projectPath = originalUserInputPath; // This path might be updated if it's a URL
            var isGit = IsGitHubUrl(projectPath);
            if (isGit)
            {
                Console.WriteLine($"Обнаружен URL GitHub репозитория: {projectPath}");
                if (CloneGitHubRepository(projectPath, out string clonedPath))
                {
                    projectPath = clonedPath; // projectPath now points to the local clone
                    _tempClonedRepoPath = clonedPath; // Store for cleanup
                    Console.WriteLine($"Репозиторий успешно клонирован в: {projectPath}");
                }
                else
                {
                    Console.WriteLine("\n--- Ошибка клонирования GitHub репозитория ---");
                    Console.WriteLine("Пожалуйста, проверьте следующее:");
                    Console.WriteLine("  1. URL репозитория корректен.");
                    Console.WriteLine("  2. Репозиторий является публичным. (Для приватных репозиториев требуется локальная настройка Git для доступа через командную строку).");
                    Console.WriteLine("  3. У вас стабильное интернет-соединение.");
                    Console.WriteLine("  4. Git установлен и корректно настроен в вашей системе (включен в PATH).");
                    Console.WriteLine("--- Дополнительные детали от Git (если доступны) указаны выше ---");
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(projectPath) ||
                (!IsGitHubUrl(originalUserInputPath) && !Directory.Exists(projectPath))) // Check original if it wasn't a URL that got cloned
            {
                 Console.WriteLine("Не удалось определить действительный путь к проекту или папка не существует.");
                 return;
            }

            var rootName = new DirectoryInfo(projectPath).Name; // Use actual processed path for root name
            string outputPath;
            string? gitName = null; 
            if (isGit)
            { 
                gitName = originalUserInputPath.Split('/').Last().Replace(".git", "");
                outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Outputs", gitName + "_listing.md");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)); // Ensure Outputs directory exists
            }
            else
            {
                // For local paths, save in the same directory as the project
                outputPath = Path.Combine(projectPath, rootName + "_listing.md");
            }
            Console.WriteLine("\nНачинаем обработку...");
            Build(projectPath, outputPath, gitName);

            // If Build was successful, update history with the original path
            string historyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HistoryFile);
            UpdateHistory(originalUserInputPath, historyFilePath);

            Console.WriteLine($"\nГотово! MD сохранен: {outputPath}");
            // Process.Start(outputPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nОшибка: {ex.Message}");
            Console.WriteLine($"Подробности: {ex.StackTrace}");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(_tempClonedRepoPath) && Directory.Exists(_tempClonedRepoPath))
            {
                try
                {
                    Console.WriteLine($"\nОчистка временного клонированного репозитория: {_tempClonedRepoPath}");
                    Directory.Delete(_tempClonedRepoPath, true);
                    Console.WriteLine("Временная папка успешно удалена.");
                }
                catch (IOException ioEx)
                {
                    Console.WriteLine($"Ошибка при удалении временной папки '{_tempClonedRepoPath}': {ioEx.Message}. Пробую еще раз.");
                    Directory.Delete(_tempClonedRepoPath, true);
                    Console.WriteLine($"Ошибка при удалении временной папки '{_tempClonedRepoPath}': {ioEx.Message}. Возможно, потребуется удалить ее вручную.");

                }
                catch (UnauthorizedAccessException authEx)
                {
                    Console.WriteLine($"Ошибка доступа при удалении временной папки '{_tempClonedRepoPath}': {authEx.Message}. Возможно, потребуется удалить ее вручную.");
                }
            }
        }
    }

    /// <summary>
    /// Clones a public GitHub repository to a temporary local directory.
    /// </summary>
    /// <param name="repoUrl">The URL of the GitHub repository.</param>
    /// <param name="clonedPath">The local path where the repository was cloned.</param>
    /// <returns>True if cloning was successful, false otherwise.</returns>
    private bool CloneGitHubRepository(string repoUrl, out string clonedPath)
    {
        clonedPath = Path.Combine(Directory.GetCurrentDirectory(), "GitHub_Clones", Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(clonedPath);
            Console.WriteLine($"\nПопытка клонировать {repoUrl} в {clonedPath}...");

            ProcessStartInfo gitInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone --depth 1 \"{repoUrl}\" \"{clonedPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string stdout;
            string stderr;

            using (Process gitProcess = new Process { StartInfo = gitInfo })
            {
                gitProcess.Start();
                stdout = gitProcess.StandardOutput.ReadToEnd();
                stderr = gitProcess.StandardError.ReadToEnd();
                gitProcess.WaitForExit();

                if (gitProcess.ExitCode == 0)
                {
                    Console.WriteLine("Git: Клонирование успешно завершено.");
                    if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine($"Git output:\n{stdout}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Git: Ошибка при клонировании (Код выхода: {gitProcess.ExitCode}).");
                    if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine($"Git stdout:\n{stdout}");
                    if (!string.IsNullOrWhiteSpace(stderr)) Console.WriteLine($"Git stderr:\n{stderr}");

                    if (Directory.Exists(clonedPath)) { try { Directory.Delete(clonedPath, true); } catch {} }
                    return false;
                }
            }
        }
        catch (System.ComponentModel.Win32Exception winEx) // Catches errors like "git" command not found
        {
            Console.WriteLine("Ошибка: Git команда не найдена. Пожалуйста, убедитесь, что Git установлен и находится в системном PATH.");
            Console.WriteLine($"Детали ошибки: {winEx.Message}");
            if (Directory.Exists(clonedPath)) { try { Directory.Delete(clonedPath, true); } catch {} }
            clonedPath = null;
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Исключение при попытке клонировать репозиторий: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"Внутреннее исключение: {ex.InnerException.Message}");
            if (Directory.Exists(clonedPath)) { try { Directory.Delete(clonedPath, true); } catch {} }
            clonedPath = null;
            return false;
        }
    }

    /// <summary>
    /// Checks if the given path string looks like a GitHub repository URL.
    /// </summary>
    /// <param name="path">The path string to check.</param>
    /// <returns>True if the path is a GitHub URL, false otherwise.</returns>
    private bool IsGitHubUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        bool isUrl = (path.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ||
                      path.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase));
        // Optionally, make it more robust by checking for .git suffix, though clone usually handles it.
        // if (isUrl && !path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        // {
        //     path += ".git"; // This modification should be done carefully, git clone handles it mostly.
        // }
        return isUrl;
    }

    /// <summary>
    /// Manages the user input process for selecting a project path (local or GitHub URL).
    /// It handles empty input and basic validation for local paths.
    /// </summary>
    /// <returns>The raw project path or URL string entered by the user.</returns>
    private string ChooseProjectPath()
    {
        // This method now solely focuses on getting user input. History is handled in UpdateHistory.
        string path = GetCustomPath(); // GetCustomPath already prompts the user

        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("Введен пустой путь. Пожалуйста, укажите корректный путь.");
            return ChooseProjectPath(); // Re-prompt for empty input
        }

        if (IsGitHubUrl(path))
        {
            // No validation needed here for URL, it will be handled during cloning attempt
            Console.WriteLine("Обнаружен URL GitHub репозитория (будет проверен при клонировании).");
        }
        else
        {
            // It's not a URL, so assume it's a local path and validate existence
            Console.WriteLine("Предполагается локальный путь. Проверка...");
            if (!Directory.Exists(path))
            {
                Console.Write($"Локальная папка '{path}' не найдена. Повторите ввод: ");
                // Allow user to enter a new path or URL
                // This recursive call might be better handled with a loop in a real app,
                // but for this structure, it's consistent.
                return ChooseProjectPath();
            }
            Console.WriteLine($"Локальная папка '{path}' найдена.");
        }

        return path; // Return the raw user input
    }



    private string GetCustomPath()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var historyFile = Path.Combine(appDir, HistoryFile);

        var history = new List<string>();
        if (File.Exists(historyFile))
        {
            history = File.ReadAllLines(historyFile)
                .Distinct()
                .ToList();
        }

        var options = new List<string> { $"Текущая папка: {appDir}", "Ввести путь вручную" };
        options.AddRange(history);

        var selected = ShowMenu(options);

        string path = selected switch
        {
            var s when s.StartsWith("Текущая папка") => appDir,
            "Ввести путь вручную" => GetCustomPath(),
            _ => selected
        };

        if (!history.Contains(path))
        {
            history.Insert(0, path);
            File.WriteAllLines(historyFile, history);
        }

        return path;
    }

    private string ShowMenu(List<string> options)
    {
        int index = 0;
        ConsoleKeyInfo key;
        Console.CursorVisible = false;

        do
        {
            Console.Clear();
            Console.WriteLine("=== Генератор документации проекта ===\n");
            Console.WriteLine("Выберите папку проекта (стрелки, Enter):\n");

            for (int i = 0; i < options.Count; i++)
            {
                if (i == index)
                {
                    Console.BackgroundColor = ConsoleColor.Gray;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.WriteLine($"> {options[i]}");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine($"  {options[i]}");
                }
            }

            key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.UpArrow)
                index = (index - 1 + options.Count) % options.Count;
            else if (key.Key == ConsoleKey.DownArrow)
                index = (index + 1) % options.Count;

        } while (key.Key != ConsoleKey.Enter);

        Console.CursorVisible = true;
        Console.Clear();
        return options[index];
    }
    /// <summary>
    /// Updates the history file with the successfully processed path.
    /// The path is added to the top, and the history is limited to 10 entries.
    /// Duplicate entries are handled by moving the existing one to the top.
    /// </summary>
    /// <param name="successfullyProcessedPath">The path (local or URL) that was successfully processed.</param>
    /// <param name="historyFilePath">The path to the history file.</param>
    private void UpdateHistory(string successfullyProcessedPath, string historyFilePath)
    {
        if (string.IsNullOrWhiteSpace(successfullyProcessedPath)) return;

        List<string> history = new List<string>();
        if (File.Exists(historyFilePath))
        {
            history = File.ReadAllLines(historyFilePath)
                          .Where(line => !string.IsNullOrWhiteSpace(line))
                          .Distinct(StringComparer.OrdinalIgnoreCase) // Case-insensitive distinct paths
                          .ToList();
        }

        // Remove the path if it already exists, to move it to the top
        history.RemoveAll(p => p.Equals(successfullyProcessedPath, StringComparison.OrdinalIgnoreCase));

        // Add the successfully processed path to the beginning of the list
        history.Insert(0, successfullyProcessedPath);

        // Limit history to the most recent N entries (e.g., 10)
        var limitedHistory = history.Take(10).ToList();

        try
        {
            File.WriteAllLines(historyFilePath, limitedHistory);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не удалось обновить файл истории ({historyFilePath}): {ex.Message}");
        }
    }


    // ShowMenu method was removed.

    /// <summary>
    /// Builds the Markdown documentation for the project at the given root path.
    /// This involves generating the project structure and processing individual file contents.
    /// </summary>
    /// <param name="rootPath">The root path of the project to document (can be a local path or a path to a cloned repository).</param>
    /// <param name="outputMd">The path where the generated Markdown file will be saved.</param>
    private void Build(string rootPath, string outputMd, string? gitName)
    {
        // Before building, ensure the path is a valid local directory.
        // The checks for IsGitHubUrl and Directory.Exists(rootPath) are now effectively handled
        // at the beginning of the Run method, especially after cloning.
        // If projectPath in Run method is a URL, it's cloned. If it's a local path, it's used directly.
        // Build() is now called with a path that is confirmed to be a local directory.

        if (!Directory.Exists(rootPath))
        {
            Console.WriteLine($"Критическая Ошибка: Директория для сборки {rootPath} не существует. Это не должно было произойти.");
            throw new DirectoryNotFoundException($"Директория для сборки не найдена: {rootPath}");
        }

        var mdContent = new StringBuilder();
        var fileCounter = 0;
        var dirCounter = 0;

        Console.WriteLine("\nГенерация структуры проекта...");
        mdContent.AppendLine("#======Структура проекта:=====\n ");
        GenerateStructure(new DirectoryInfo(rootPath), "", mdContent, gitName, ref dirCounter, ref fileCounter);

        mdContent.AppendLine("==============================");
        Console.WriteLine($"Обработано папок: {dirCounter}, файлов: {fileCounter}");

        Console.WriteLine("\nСбор содержимого файлов...");
        mdContent.AppendLine("\n# Содержимое файлов");
        // Pass rootPath as the basePath for relative path calculations
        ProcessDirectory(new DirectoryInfo(rootPath), rootPath, mdContent, ref fileCounter);

        Console.WriteLine("\nСохранение MD...");
        File.WriteAllText(outputMd, mdContent.ToString(), Encoding.UTF8);
    }

    //private void GenerateStructure(DirectoryInfo dir, string indent, StringBuilder sb, ref int dirCounter, ref int fileCounter)
    //{
    //    dirCounter++;
    //    sb.AppendLine($"{indent} {dir.Name}");

    //    foreach (var file in dir.GetFiles())
    //    {
    //        if (ShouldExclude(file)) continue;
    //        fileCounter++;
    //        sb.AppendLine($"{indent}└──[FILE] {file.Name}");
    //    }

    //    foreach (var subDir in dir.GetDirectories())
    //    {
    //        if (ShouldExclude(subDir)) continue;
    //        if (indent.Contains("DIR"))
    //        {
    //            GenerateStructure(subDir, indent + "──]", sb, ref dirCounter, ref fileCounter);
    //        }
    //        else
    //        {
    //            GenerateStructure(subDir, indent + "└─DIR", sb, ref dirCounter, ref fileCounter);
    //        }
    //    }
    //}

    /// <summary>
    /// Recursively generates the project's directory and file structure in Markdown format.
    /// </summary>
    /// <param name="dir">The current directory being processed.</param>
    /// <param name="indent">The current indentation string for formatting the structure.</param>
    /// <param name="sb">The StringBuilder to append the structure to.</param>
    /// <param name="dirCounter">Reference to a counter for processed directories.</param>
    /// <param name="fileCounter">Reference to a counter for processed files (for structure view).</param>
    private void GenerateStructure(DirectoryInfo dir, string indent, StringBuilder sb, string? projName, ref int dirCounter, ref int fileCounter)
    {
        dirCounter++;
        var rootName = projName ?? dir.Name; // Use provided project name or directory name
        if (dirCounter == 1) sb.AppendLine($"[ROOT] {rootName}");
        var files = dir.GetFiles().Where(f => !ShouldExclude(f)).ToList();
        var subDirs = dir.GetDirectories().Where(d => !ShouldExclude(d)).ToList();

        // Обработка файлов и подпапок
        for (int i = 0; i < files.Count + subDirs.Count; i++)
        {
            var isLast = i == files.Count + subDirs.Count - 1;
            var connector = isLast ? "└── " : "├── ";

            if (i < files.Count)
            {
                // Обработка файлов
                fileCounter++;
                sb.AppendLine($"{indent}{connector} {files[i].Name}");
            }
            else
            {
                // Обработка подпапок
                var subDir = subDirs[i - files.Count];
                sb.AppendLine($"{indent}{connector} {subDir.Name}");
                //sb.AppendLine($"<div class='folder'>{indent}📁 {dir.Name}</div>");
                // Рекурсивный вызов с правильным отступом
                var newIndent = indent + (isLast ? "        " : "│        ");
                GenerateStructure(subDir, newIndent, sb, projName, ref dirCounter, ref fileCounter);
            }
        }
    }

    /// <summary>
    /// Recursively processes a directory, reading content from allowed files and appending it to the StringBuilder.
    /// </summary>
    /// <param name="dir">The current directory being processed.</param>
    /// <param name="basePath">The root path of the project, used for calculating relative file paths.</param>
    /// <param name="sb">The StringBuilder to append file contents to.</param>
    /// <param name="counter">Reference to a counter for processed files (for content view).</param>
    private void ProcessDirectory(DirectoryInfo dir, string basePath, StringBuilder sb, ref int counter)
    {
        foreach (var file in dir.GetFiles())
        {
            if (ShouldExclude(file)) continue;

            try
            {
                Console.WriteLine($"Обработка: {file.FullName}");
                // Use the basePath for GetRelativePath
                sb.AppendLine($"\n## Файл: {GetRelativePath(file.FullName, basePath)}");
                sb.AppendLine($"```{GetLanguageTag(file)}");
                sb.AppendLine(File.ReadAllText(file.FullName, Encoding.UTF8));
                sb.AppendLine("```");
                counter++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка чтения файла: {file.Name} - {ex.Message}");
            }
        }

        foreach (var subDir in dir.GetDirectories())
        {
            if (ShouldExclude(subDir)) continue;
            // Pass the basePath along in recursive calls
            ProcessDirectory(subDir, basePath, sb, ref counter);
        }
    }

    /// <summary>
    /// Determines if a file or directory should be excluded based on predefined lists.
    /// </summary>
    /// <param name="item">The FileSystemInfo item (file or directory) to check.</param>
    /// <returns>True if the item should be excluded, false otherwise.</returns>
    private bool ShouldExclude(FileSystemInfo item)
    {
        if (item is DirectoryInfo dir)
            return _excludedDirs.Contains(dir.Name);

        if (item is FileInfo file)
            return _excludedExtensions.Contains(file.Extension.ToLower()) || _excludedFiles.Contains(file.Name.ToLower());

        return false;
    }

    /// <summary>
    /// Calculates the relative path of a file or directory from a given base path.
    /// </summary>
    /// <param name="fullPath">The full path of the item.</param>
    /// <param name="basePath">The base path to make the fullPath relative to.</param>
    /// <returns>A path string relative to the basePath.</returns>
    private string GetRelativePath(string fullPath, string basePath) =>
        Path.GetRelativePath(basePath, fullPath);

    /// <summary>
    /// Gets the language tag for Markdown code blocks based on file extension.
    /// </summary>
    /// <param name="file">The FileInfo object for the file.</param>
    /// <returns>A string representing the language (e.g., "csharp", "javascript") or an empty string if not recognized.</returns>
    private string GetLanguageTag(FileInfo file) =>
        file.Extension.ToLower() switch
        {
            ".cs" => "csharp",
            ".js" => "javascript",
            ".json" => "json",
            ".html" => "html",
            ".css" => "css",
            ".md" => "markdown",
            ".xml" => "xml",
            ".sql" => "sql",
            _ => ""
        };


}
