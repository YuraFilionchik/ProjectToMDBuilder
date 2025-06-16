using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var builder = new ProjectToMdBuilder();
        builder.Run();
    }
}

class ProjectToMdBuilder
{
    private readonly List<string> _excludedDirs = new List<string>
    {
        ".git", ".github", "bin", "obj", "node_modules", "packages", "dist", "wwwroot",
        ".vs", ".cr", ".vscode", ".idea", "TestResults", "coverage", "artifacts",
        "docs", "images", "resources"
    };

    private readonly List<string> _excludedExtensions = new List<string>
    {
        ".exe", ".dll", ".pdb", ".suo", ".user", ".cache", ".zip", ".pdf",
        ".snk", ".pfx", ".cer", ".csproj", ".sln", ".userprefs", ".lock.json", ".dockerignore",
        ".gitattributes", ".gitignore", "sql", ".md", ".txt", ".log", ".tmp", ".bak", ".swp",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".rar", ".7z", ".tar", ".gz",
        ".mp3", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".iso", ".class", ".jar"
    };

    private readonly List<string> _excludedFiles = new List<string>
    {
        "launchsettings.json", "appsettings.json", "appsettings.development.json",
        "appsettings.production.json", "appsettings.staging.json", "web.config"
    };

    public void Run()
    {
        try
        {
            Console.WriteLine("=== Генератор документации проекта ===");

            var defaultPath = AppDomain.CurrentDomain.BaseDirectory;
            Console.WriteLine($"\nТекущая папка приложения: {defaultPath}");
            Console.Write("Использовать текущую папку? (Y/N Д/Н): ");
            var readed = Console.ReadLine().Trim().ToUpper();
            var rootPath = readed == "Y" || readed == "Д"
                ? defaultPath
                : GetCustomPath();
            var root = new DirectoryInfo(rootPath).Name;
            var outputPath = Path.Combine(rootPath, root + "_project_documentation.md");

            Console.WriteLine("\nНачинаем обработку...");
            Build(rootPath, outputPath);

            Console.WriteLine($"\nГотово! MD сохранен: {outputPath}");
           // Process.Start(outputPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nОшибка: {ex.Message}");
            Console.WriteLine("Подробности:");
            Console.WriteLine(ex);
        }
    }

    private string GetCustomPath()
    {
        Console.Write("Введите полный путь к папке проекта: ");
        var path = Console.ReadLine().Trim();

        while (!Directory.Exists(path))
        {
            Console.Write("Папка не найдена! Повторите ввод: ");
            path = Console.ReadLine().Trim();
        }

        return path;
    }

    private void Build(string rootPath, string outputMd)
    {
        var mdContent = new StringBuilder();
        var fileCounter = 0;
        var dirCounter = 0;

        Console.WriteLine("\nГенерация структуры проекта...");
        mdContent.AppendLine("#======Структура проекта:=====\n ");
        GenerateStructure(new DirectoryInfo(rootPath), "", mdContent, ref dirCounter, ref fileCounter);
        mdContent.AppendLine("===============================");
        Console.WriteLine($"Обработано папок: {dirCounter}, файлов: {fileCounter}");

        Console.WriteLine("\nСбор содержимого файлов...");
        mdContent.AppendLine("\n# Содержимое файлов");
        ProcessDirectory(new DirectoryInfo(rootPath), mdContent, ref fileCounter);

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

    private void GenerateStructure(DirectoryInfo dir, string indent, StringBuilder sb, ref int dirCounter, ref int fileCounter)
    {
        dirCounter++;
        if (dirCounter == 1) sb.AppendLine($"[ROOT] {dir.Name}");
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
                GenerateStructure(subDir, newIndent, sb, ref dirCounter, ref fileCounter);
            }
        }
    }

    private void ProcessDirectory(DirectoryInfo dir, StringBuilder sb, ref int counter)
    {
        foreach (var file in dir.GetFiles())
        {
            if (ShouldExclude(file)) continue;

            try
            {
                Console.WriteLine($"Обработка: {file.FullName}");
                sb.AppendLine($"\n## Файл: {GetRelativePath(file.FullName)}");
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
            ProcessDirectory(subDir, sb, ref counter);
        }
    }

    private bool ShouldExclude(FileSystemInfo item)
    {
        if (item is DirectoryInfo dir)
            return _excludedDirs.Contains(dir.Name);

        if (item is FileInfo file)
            return _excludedExtensions.Contains(file.Extension.ToLower()) || _excludedFiles.Contains(file.Name.ToLower());

        return false;
    }

    private string GetRelativePath(string fullPath) =>
        Path.GetRelativePath(Directory.GetCurrentDirectory(), fullPath);

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