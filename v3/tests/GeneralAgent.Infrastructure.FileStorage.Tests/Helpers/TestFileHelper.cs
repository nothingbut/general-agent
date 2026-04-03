namespace GeneralAgent.Infrastructure.FileStorage.Tests.Helpers;

/// <summary>
/// 测试文件辅助类 - 用于创建和管理测试文件
/// </summary>
public static class TestFileHelper
{
    /// <summary>
    /// 创建临时测试文件
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <param name="content">文件内容</param>
    /// <returns>文件路径</returns>
    public static string CreateTempFile(string fileName, string content)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "general-agent-test-files");
        Directory.CreateDirectory(tempDir);

        var filePath = Path.Combine(tempDir, fileName);
        File.WriteAllText(filePath, content);

        return filePath;
    }

    /// <summary>
    /// 创建临时文本文件
    /// </summary>
    public static string CreateTempTextFile(string content = "测试文本内容")
    {
        return CreateTempFile($"test-{Guid.NewGuid()}.txt", content);
    }

    /// <summary>
    /// 创建临时 JSON 文件
    /// </summary>
    public static string CreateTempJsonFile(string? json = null)
    {
        json ??= """
            {
              "name": "test",
              "value": 123,
              "enabled": true
            }
            """;

        return CreateTempFile($"test-{Guid.NewGuid()}.json", json);
    }

    /// <summary>
    /// 创建临时代码文件
    /// </summary>
    public static string CreateTempCodeFile(string extension = ".cs", string? code = null)
    {
        code ??= """
            using System;

            public class TestClass
            {
                public void TestMethod()
                {
                    Console.WriteLine("Hello, World!");
                }
            }
            """;

        return CreateTempFile($"test-{Guid.NewGuid()}{extension}", code);
    }

    /// <summary>
    /// 创建临时 Markdown 文件
    /// </summary>
    public static string CreateTempMarkdownFile(string? markdown = null)
    {
        markdown ??= """
            # 测试标题

            这是一段测试内容。

            ## 子标题

            - 列表项 1
            - 列表项 2
            - 列表项 3
            """;

        return CreateTempFile($"test-{Guid.NewGuid()}.md", markdown);
    }

    /// <summary>
    /// 创建大文件（用于测试截断）
    /// </summary>
    public static string CreateLargeFile(int sizeInKB = 100)
    {
        var content = new string('A', sizeInKB * 1024);
        return CreateTempFile($"large-{Guid.NewGuid()}.txt", content);
    }

    /// <summary>
    /// 清理临时文件
    /// </summary>
    public static void CleanupTempFiles()
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "general-agent-test-files");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
        catch
        {
            // 忽略清理错误
        }
    }

    /// <summary>
    /// 获取测试数据目录
    /// </summary>
    public static string GetTestDataDirectory()
    {
        // 找到测试项目根目录
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "GeneralAgent.Infrastructure.FileStorage.Tests.csproj")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        if (currentDir == null)
        {
            throw new DirectoryNotFoundException("无法找到测试项目根目录");
        }

        var testDataDir = Path.Combine(currentDir, "TestData");
        Directory.CreateDirectory(testDataDir);

        return testDataDir;
    }

    /// <summary>
    /// 从测试数据目录复制文件到临时位置
    /// </summary>
    public static string CopyTestDataFile(string fileName)
    {
        var testDataDir = GetTestDataDirectory();
        var sourcePath = Path.Combine(testDataDir, fileName);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"测试数据文件不存在: {fileName}", sourcePath);
        }

        var tempPath = CreateTempFile(fileName, File.ReadAllText(sourcePath));
        return tempPath;
    }
}
