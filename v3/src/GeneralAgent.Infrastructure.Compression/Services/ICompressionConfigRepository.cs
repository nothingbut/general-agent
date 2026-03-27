using GeneralAgent.Infrastructure.Compression.Models;

namespace GeneralAgent.Infrastructure.Compression.Services;

/// <summary>
/// 压缩配置仓储接口
/// </summary>
public interface ICompressionConfigRepository
{
    /// <summary>
    /// 获取会话的压缩配置
    /// </summary>
    Task<CompressionConfig?> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// 保存或更新会话的压缩配置
    /// </summary>
    Task<CompressionConfig> SaveOrUpdateAsync(CompressionConfig config, CancellationToken ct = default);

    /// <summary>
    /// 删除会话的压缩配置
    /// </summary>
    Task DeleteBySessionIdAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// 获取所有启用自动压缩的会话 ID
    /// </summary>
    Task<List<Guid>> GetAutoCompressionEnabledSessionsAsync(CancellationToken ct = default);
}
