namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 技能写入器接口 - 将技能定义保存到文件系统
/// </summary>
public interface ISkillWriter
{
    /// <summary>
    /// 保存技能文件
    /// </summary>
    /// <param name="namespace">命名空间</param>
    /// <param name="name">技能名称</param>
    /// <param name="content">文件内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存后的文件路径</returns>
    Task<string> SaveSkillAsync(
        string @namespace,
        string name,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新现有技能文件
    /// </summary>
    /// <param name="skillPath">技能文件路径</param>
    /// <param name="content">新的文件内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateSkillAsync(
        string skillPath,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除技能文件
    /// </summary>
    /// <param name="skillPath">技能文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteSkillAsync(
        string skillPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查技能是否已存在
    /// </summary>
    /// <param name="namespace">命名空间</param>
    /// <param name="name">技能名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistsAsync(
        string @namespace,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取技能文件路径
    /// </summary>
    /// <param name="namespace">命名空间</param>
    /// <param name="name">技能名称</param>
    /// <returns>完整文件路径</returns>
    string GetSkillPath(string @namespace, string name);
}
