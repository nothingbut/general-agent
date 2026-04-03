using GeneralAgent.Infrastructure.FileStorage.Processors;
using GeneralAgent.Infrastructure.FileStorage.Repositories;
using GeneralAgent.Infrastructure.FileStorage.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Infrastructure.FileStorage.Tests.Fixtures;

/// <summary>
/// 文件存储测试固件 - 提供共享的测试环境
/// </summary>
public class FileStorageFixture : IDisposable
{
    /// <summary>
    /// 测试根目录
    /// </summary>
    public string TestRootDirectory { get; }

    /// <summary>
    /// 测试数据库路径
    /// </summary>
    public string TestDatabasePath { get; }

    /// <summary>
    /// 文件存储选项
    /// </summary>
    public FileStorageOptions Options { get; }

    /// <summary>
    /// 文件仓储
    /// </summary>
    public FileRepository Repository { get; }

    /// <summary>
    /// 文件存储服务
    /// </summary>
    public FileStorageService StorageService { get; }

    public FileStorageFixture()
    {
        // 创建临时测试目录
        TestRootDirectory = Path.Combine(Path.GetTempPath(), "general-agent-tests", Guid.NewGuid().ToString());
        TestDatabasePath = Path.Combine(TestRootDirectory, "test-files.db");

        Directory.CreateDirectory(TestRootDirectory);

        // 配置选项
        Options = new FileStorageOptions
        {
            RootDirectory = TestRootDirectory,
            DatabasePath = TestDatabasePath,
            MaxFileSizeBytes = 5 * 1024 * 1024, // 5 MB
            MaxContentLength = 10000
        };

        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(Options);

        // 创建仓储
        var repoLogger = Microsoft.Extensions.Logging.LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger<FileRepository>();
        Repository = new FileRepository(optionsWrapper, repoLogger);

        // 创建处理器服务
        var processorLogger = Microsoft.Extensions.Logging.LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger<FileStorageService>();

        // 注册文件处理器
        var processors = new List<IFileProcessor>
        {
            new TextFileProcessor(
                Microsoft.Extensions.Logging.LoggerFactory
                    .Create(builder => builder.AddConsole())
                    .CreateLogger<TextFileProcessor>()),
            new CodeFileProcessor(
                Microsoft.Extensions.Logging.LoggerFactory
                    .Create(builder => builder.AddConsole())
                    .CreateLogger<CodeFileProcessor>()),
            new JsonFileProcessor(
                Microsoft.Extensions.Logging.LoggerFactory
                    .Create(builder => builder.AddConsole())
                    .CreateLogger<JsonFileProcessor>())
        };

        var processorService = new FileProcessorService(
            processors,
            Microsoft.Extensions.Logging.LoggerFactory
                .Create(builder => builder.AddConsole())
                .CreateLogger<FileProcessorService>());

        // 创建存储服务
        StorageService = new FileStorageService(
            optionsWrapper,
            Repository,
            processorService,
            processorLogger);
    }

    /// <summary>
    /// 创建测试会话目录
    /// </summary>
    public string CreateTestSessionDirectory(string sessionId)
    {
        var sessionDir = Path.Combine(TestRootDirectory, "sessions", sessionId, "files");
        Directory.CreateDirectory(sessionDir);
        return sessionDir;
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(TestRootDirectory))
            {
                Directory.Delete(TestRootDirectory, recursive: true);
            }
        }
        catch
        {
            // 忽略清理错误
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// xUnit 集合固件 - 允许在多个测试类之间共享固件
/// </summary>
[CollectionDefinition("FileStorage Collection")]
public class FileStorageCollection : ICollectionFixture<FileStorageFixture>
{
    // 此类仅用于定义集合，不需要实现
}
