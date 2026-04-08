using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Processors;
using GeneralAgent.Infrastructure.FileStorage.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Infrastructure.FileStorage.Services;

/// <summary>
/// 文件存储服务（文件 I/O 操作）
/// </summary>
public class FileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly FileRepository _repository;
    private readonly FileProcessorService _processorService;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        IOptions<FileStorageOptions> options,
        FileRepository repository,
        FileProcessorService processorService,
        ILogger<FileStorageService> logger)
    {
        _options = options.Value;
        _repository = repository;
        _processorService = processorService;
        _logger = logger;
    }

    /// <summary>
    /// 上传文件
    /// </summary>
    /// <param name="sourceFilePath">源文件路径</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="ownerId">文件所有者 ID</param>
    /// <param name="accessLevel">访问级别（默认私有）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上传后的文件记录</returns>
    /// <exception cref="ArgumentException">文件不存在或不支持</exception>
    /// <exception cref="InvalidOperationException">文件过大</exception>
    public async Task<UploadedFile> UploadFileAsync(
        string sourceFilePath,
        string sessionId,
        string ownerId,
        FileAccessLevel accessLevel = FileAccessLevel.Private,
        CancellationToken cancellationToken = default)
    {
        // 1. 验证文件存在
        if (!File.Exists(sourceFilePath))
        {
            throw new ArgumentException($"文件不存在: {sourceFilePath}", nameof(sourceFilePath));
        }

        // 2. 获取文件信息
        var fileInfo = new FileInfo(sourceFilePath);
        var fileName = fileInfo.Name;
        var fileType = fileInfo.Extension.ToLowerInvariant();
        var fileSize = fileInfo.Length;

        // 3. 验证文件类型
        if (!_options.AllowedExtensions.Contains(fileType))
        {
            throw new ArgumentException(
                $"不支持的文件类型: {fileType}。支持的类型: {string.Join(", ", _options.AllowedExtensions)}",
                nameof(sourceFilePath));
        }

        // 4. 验证文件大小
        if (fileSize > _options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"文件过大: {fileSize / 1024.0 / 1024.0:F2} MB，最大允许 {_options.MaxFileSizeBytes / 1024.0 / 1024.0:F2} MB");
        }

        // 5. 生成存储路径
        var relativePath = GenerateStoragePath(sessionId, fileName);
        var absolutePath = Path.Combine(_options.RootDirectory, relativePath);

        // 6. 确保目录存在
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogDebug("创建文件存储目录: {Directory}", directory);
        }

        // 7. 复制文件到存储位置
        await using (var sourceStream = File.OpenRead(sourceFilePath))
        await using (var destStream = File.Create(absolutePath))
        {
            await sourceStream.CopyToAsync(destStream, cancellationToken);
        }

        _logger.LogInformation("文件已存储: {FileName} -> {Path}", fileName, relativePath);

        // 8. 创建文件记录
        var uploadedFile = UploadedFile.Create(
            sessionId: sessionId,
            fileName: fileName,
            filePath: relativePath,
            fileType: fileType,
            fileSize: fileSize,
            ownerId: ownerId,
            mimeType: GetMimeType(fileType),
            accessLevel: accessLevel);

        // 9. 保存元数据
        await _repository.SaveAsync(uploadedFile, cancellationToken);

        return uploadedFile;
    }

    /// <summary>
    /// 读取文件内容
    /// </summary>
    public async Task<ProcessedFileContent> ReadFileContentAsync(
        UploadedFile file,
        CancellationToken cancellationToken = default)
    {
        var absolutePath = Path.Combine(_options.RootDirectory, file.FilePath);

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException($"文件不存在: {file.FileName}", absolutePath);
        }

        // 使用 FileProcessorService 处理文件
        var processedContent = await _processorService.ProcessFileAsync(
            absolutePath,
            _options.MaxContentLength,
            cancellationToken);

        if (processedContent.IsTruncated)
        {
            _logger.LogWarning(
                "文件内容已截断: {FileName}, 原始长度: {Original}, 处理后: {Processed}",
                file.FileName,
                processedContent.OriginalLength,
                processedContent.ProcessedLength);
        }

        return processedContent;
    }

    /// <summary>
    /// 读取文件内容（返回字符串）
    /// </summary>
    public async Task<string> ReadFileContentAsStringAsync(
        UploadedFile file,
        CancellationToken cancellationToken = default)
    {
        var processedContent = await ReadFileContentAsync(file, cancellationToken);
        return processedContent.Content;
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    public async Task<bool> DeleteFileAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        // 1. 获取文件元数据
        var file = await _repository.GetByIdAsync(fileId, cancellationToken);
        if (file == null)
        {
            _logger.LogWarning("文件不存在: ID {FileId}", fileId);
            return false;
        }

        // 2. 删除物理文件
        var absolutePath = Path.Combine(_options.RootDirectory, file.FilePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
            _logger.LogInformation("文件已删除: {FileName}", file.FileName);
        }

        // 3. 删除元数据
        await _repository.DeleteAsync(fileId, cancellationToken);

        return true;
    }

    /// <summary>
    /// 列出会话的所有文件
    /// </summary>
    public async Task<List<UploadedFile>> ListFilesAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.ListBySessionAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// 根据 ID 获取文件
    /// </summary>
    public async Task<UploadedFile?> GetFileAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(fileId, cancellationToken);
    }

    /// <summary>
    /// 根据文件名获取文件（当前会话）
    /// </summary>
    public async Task<List<UploadedFile>> GetFilesByNameAsync(
        string fileName,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetByFileNameAsync(fileName, sessionId, cancellationToken);
    }

    /// <summary>
    /// 生成存储路径
    /// 格式: sessions/<session-id>/files/<filename>
    /// </summary>
    private static string GenerateStoragePath(string sessionId, string fileName)
    {
        // 为避免文件名冲突，添加时间戳前缀
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var safeFileName = $"{timestamp}_{fileName}";

        return Path.Combine("sessions", sessionId, "files", safeFileName);
    }

    /// <summary>
    /// 根据文件扩展名获取 MIME 类型
    /// </summary>
    private static string? GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".txt" or ".md" or ".markdown" => "text/plain",
            ".json" => "application/json",
            ".yaml" or ".yml" => "application/x-yaml",
            ".xml" => "application/xml",
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".ts" => "application/typescript",
            ".cs" => "text/x-csharp",
            ".py" => "text/x-python",
            ".java" => "text/x-java",
            ".cpp" or ".c" or ".h" => "text/x-c",
            ".rs" => "text/x-rust",
            ".go" => "text/x-go",
            _ => null
        };
    }
}
