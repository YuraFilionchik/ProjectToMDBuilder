# ProjectToMdBuilder 🚀

**ProjectToMdBuilder** is a powerful and convenient C# tool designed to automatically generate complete documentation of your project in Markdown format. It is ideal for preparing code for analysis by Large Language Models (LLMs), creating quick architecture overviews, or backing up source code into a single file.

---

## ✨ Key Features

*   **🌐 GitHub Support:** Simply paste a link to a public repository, and the tool will automatically clone it to a temporary folder for processing.
*   **📂 Structure Visualization:** Automatically builds a beautiful directory and file tree of the project.
*   **📝 Content Gathering:** Combines the source code of all files into a single MD file while preserving relative paths.
*   **🎨 Syntax Highlighting:** Automatically detects the language (C#, JS, JSON, HTML, CSS, SQL, XML) for correct code display in Markdown.
*   **🛡️ Smart Filtering:** Pre-defined exclusion lists to ignore service files, binary data, and libraries.
*   **⌨️ Interactive Menu:** User-friendly console interface with arrow key navigation and a history of recent paths.

---

## 🛠 Tech Stack

*   **Language:** C#
*   **Platform:** .NET 9.0
*   **Dependencies:** `System.Text.Encoding.CodePages`

---

## 🚀 Quick Start

### Requirements
*   [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) installed
*   [Git](https://git-scm.com/) installed (for working with GitHub repositories)

### Running
1.  Clone the repository with the tool or download the source code.
2.  Navigate to the project folder and execute the command:
    ```bash
    dotnet run
    ```
3.  Use the arrow keys on your keyboard to select an option:
    *   **Current Folder:** Process files in the launch directory.
    *   **Enter path manually:** Specify a local path or a GitHub repository URL.
    *   **History:** Choose from the 10 most recent paths.

---

## 🔍 Filtering Rules (Exclusions)

The tool automatically ignores "noise" to keep your documentation file clean and useful.

### Ignored Folders
`.git`, `.github`, `bin`, `obj`, `node_modules`, `packages`, `dist`, `wwwroot`, `.vs`, `.cr`, `.vscode`, `.idea`, `TestResults`, `coverage`, `artifacts`, `docs`, `images`, `resources`.

### Ignored Extensions
*   **Binary files:** `.exe`, `.dll`, `.pdb`, `.zip`, `.rar`, `.7z`, `.tar`, `.gz`, `.iso`, `.class`, `.jar`
*   **Media:** `.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp`, `.ico`, `.mp3`, `.mp4`, `.avi`, `.mov`, `.wmv`, `.flv`
*   **Service:** `.suo`, `.user`, `.cache`, `.pdf`, `.snk`, `.pfx`, `.cer`, `.sln`, `.userprefs`, `.lock.json`, `.tmp`, `.bak`, `.swp`, `.log`
*   **Other:** `.md`, `.txt`, `.sql`, `.dockerignore`, `.gitattributes`, `.gitignore`

### Ignored Files
`launchsettings.json`, `appsettings.json`, `appsettings.development.json`, `appsettings.production.json`, `appsettings.staging.json`, `web.config`.

---

## 📄 Output Example

The result is saved to a `{ProjectName}_listing.md` file in the project's root folder (for local paths) or in the `Outputs` folder (for GitHub clones).

Document Structure:
1.  **#======Project Structure:=====** (Visual tree)
2.  **# File Contents**
    *   `## File: Path/To/File.cs`
    *   Code block with appropriate language highlighting.

---

## 🤝 Contributing

If you have ideas for improvement or found a bug, please create an Issue or send a Pull Request. I would be happy for any help!

---
*Generated with love for clean code and high-quality documentation.*
