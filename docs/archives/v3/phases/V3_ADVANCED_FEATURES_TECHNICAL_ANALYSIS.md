# General Agent V3 - 高级功能技术分析

**创建日期**: 2026-03-26
**目标**: 深入分析技术方案、识别风险、准备原型验证

---

## 📊 功能复杂度矩阵

| 功能 | 技术复杂度 | 实施难度 | 用户价值 | 推荐优先级 | 预计时间 |
|------|----------|---------|---------|-----------|---------|
| 上下文压缩 | ⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ | P0 | 1-2 周 |
| 文件上传 | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | P0 | 2-3 周 |
| 长期记忆 | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | P1 | 3-4 周 |
| 计划任务 | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | P1 | 2-3 周 |
| Skill 抽取 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | P2 | 4-5 周 |

**说明**:
- ⭐ = 低，⭐⭐⭐⭐⭐ = 高
- P0 = 立即实施，P1 = 近期实施，P2 = 长期规划

---

## 1️⃣ 上下文压缩 - 深度技术分析

### 1.1 核心挑战

#### 挑战 1: 信息损失 vs Token 节省的平衡
**问题**: 压缩必然导致信息损失，如何保证对话质量？

**解决方案**:
```csharp
// 分层压缩策略
public class HierarchicalCompression
{
    // 第 1 层：完整保留（最近 N 条）
    private const int RecentMessagesCount = 10;
    
    // 第 2 层：保留关键点（N 到 M 条）
    private const int SummaryMessagesCount = 30;
    
    // 第 3 层：全局摘要（M 条以前）
    private const int ArchiveThreshold = 50;
    
    public CompressedContext Compress(List<Message> messages)
    {
        var result = new CompressedContext();
        
        // 最近 10 条：完整保留
        result.RecentMessages = messages.TakeLast(RecentMessagesCount).ToList();
        
        // 10-40 条：提取关键点
        var middleMessages = messages
            .Skip(Math.Max(0, messages.Count - 40))
            .Take(30)
            .ToList();
        result.KeyPoints = ExtractKeyPoints(middleMessages);
        
        // 40 条以前：生成摘要
        if (messages.Count > 40)
        {
            var archiveMessages = messages
                .Take(messages.Count - 40)
                .ToList();
            result.GlobalSummary = GenerateSummary(archiveMessages);
        }
        
        return result;
    }
}
```

#### 挑战 2: Token 计数准确性
**问题**: 不同模型的 tokenizer 不同，如何准确估算？

**解决方案**:
```csharp
public class TokenCounter
{
    // 使用 TikToken 库（OpenAI 官方）
    private readonly TikToken _encoder;
    
    public TokenCounter(string modelName)
    {
        _encoder = TikToken.EncodingForModel(modelName);
    }
    
    public int CountTokens(string text)
    {
        return _encoder.Encode(text).Count;
    }
    
    public int CountTokens(List<Message> messages)
    {
        // 考虑消息格式的开销
        var overhead = messages.Count * 4; // 每条消息约 4 个 token 的格式开销
        var contentTokens = messages.Sum(m => CountTokens(m.Content));
        return overhead + contentTokens;
    }
}
```

#### 挑战 3: 语义压缩的成本
**问题**: 使用 LLM 生成摘要会产生额外的 API 调用成本

**解决方案 - 混合策略**:
```csharp
public class AdaptiveCompressionService
{
    public async Task<CompressedContext> Compress(
        List<Message> messages,
        int targetTokens,
        CompressionBudget budget)
    {
        var currentTokens = _tokenCounter.CountTokens(messages);
        
        // 预算充足：使用语义压缩（质量最高）
        if (budget == CompressionBudget.High && currentTokens > targetTokens * 1.5)
        {
            return await SemanticCompress(messages, targetTokens);
        }
        
        // 预算中等：使用关键点提取（平衡）
        if (budget == CompressionBudget.Medium && currentTokens > targetTokens * 1.2)
        {
            return await KeyPointExtraction(messages, targetTokens);
        }
        
        // 预算紧张：使用滑动窗口（成本最低）
        return SlidingWindowCompress(messages, targetTokens);
    }
}
```

### 1.2 技术风险评估

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|---------|
| 信息丢失导致对话断裂 | 中 | 高 | 增量压缩，保留关键上下文 |
| Token 计数不准确 | 低 | 中 | 使用官方 tokenizer 库 |
| 压缩开销过大 | 中 | 中 | 异步压缩，批量处理 |
| 跨模型兼容性问题 | 高 | 低 | 支持多种 tokenizer |

### 1.3 原型验证方案

**阶段 1: 滑动窗口验证** (3 天)
```csharp
// 最简单的实现，验证基础可行性
public class SlidingWindowPrototype
{
    public List<Message> Compress(List<Message> messages, int windowSize = 10)
    {
        return messages.TakeLast(windowSize).ToList();
    }
}

// 验证指标
- Token 节省率 > 50%
- 对话连贯性保持
- 性能开销 < 10ms
```

**阶段 2: 分层压缩验证** (5 天)
```csharp
// 实现分层策略，评估信息保留效果
- 对比测试：完整历史 vs 压缩历史
- 质量评估：人工评分 + LLM 评分
- 成本分析：token 节省 vs API 调用增加
```

**阶段 3: 语义压缩验证** (5 天)
```csharp
// 使用 LLM 生成摘要
- A/B 测试：不同压缩策略对比
- 长期测试：100+ 轮对话的稳定性
- 成本评估：ROI 分析
```

---

## 2️⃣ 文件上传支持 - 深度技术分析

### 2.1 核心挑战

#### 挑战 1: 文件大小限制
**问题**: 大文件如何处理？存储在哪里？

**解决方案**:
```csharp
public class FileUploadConfig
{
    // 文件大小限制
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
    public long MaxTotalStorageBytes { get; set; } = 100 * 1024 * 1024; // 100MB
    
    // 存储策略
    public StorageStrategy Strategy { get; set; } = StorageStrategy.Local;
}

public enum StorageStrategy
{
    Local,      // 本地文件系统 (~/.agent/uploads/)
    S3,         // AWS S3 或兼容服务
    Database    // SQLite BLOB（仅小文件）
}

// 文件分块上传
public class ChunkedUploadService
{
    private const int ChunkSize = 1024 * 1024; // 1MB chunks
    
    public async Task<string> UploadLargeFile(Stream fileStream, string fileName)
    {
        var fileId = Guid.NewGuid().ToString();
        var chunks = new List<string>();
        
        var buffer = new byte[ChunkSize];
        int bytesRead;
        int chunkIndex = 0;
        
        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, ChunkSize)) > 0)
        {
            var chunkPath = await SaveChunk(fileId, chunkIndex, buffer, bytesRead);
            chunks.Add(chunkPath);
            chunkIndex++;
        }
        
        // 记录元数据
        await SaveFileMetadata(fileId, fileName, chunks);
        return fileId;
    }
}
```

#### 挑战 2: 不同文件格式的解析
**问题**: 如何统一处理各种文件格式？

**解决方案 - 插件架构**:
```csharp
public interface IFileParser
{
    bool CanParse(string mimeType);
    Task<ParsedContent> Parse(Stream fileStream);
}

// PDF 解析器
public class PdfParser : IFileParser
{
    public bool CanParse(string mimeType) 
        => mimeType == "application/pdf";
    
    public async Task<ParsedContent> Parse(Stream fileStream)
    {
        // 使用 PdfPig 或 iTextSharp
        using var document = PdfDocument.Open(fileStream);
        var text = string.Join("\n", 
            document.GetPages().Select(p => p.Text));
        
        return new ParsedContent
        {
            Text = text,
            PageCount = document.NumberOfPages,
            Metadata = ExtractMetadata(document)
        };
    }
}

// Markdown 解析器
public class MarkdownParser : IFileParser
{
    public bool CanParse(string mimeType) 
        => mimeType == "text/markdown";
    
    public async Task<ParsedContent> Parse(Stream fileStream)
    {
        using var reader = new StreamReader(fileStream);
        var markdown = await reader.ReadToEndAsync();
        
        // 可选：转换为 HTML
        var html = Markdig.Markdown.ToHtml(markdown);
        
        return new ParsedContent
        {
            Text = markdown,
            Html = html,
            Metadata = ExtractFrontmatter(markdown)
        };
    }
}

// 解析器管理
public class FileParserRegistry
{
    private readonly List<IFileParser> _parsers = new();
    
    public void Register(IFileParser parser)
    {
        _parsers.Add(parser);
    }
    
    public async Task<ParsedContent> Parse(string mimeType, Stream fileStream)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(mimeType));
        if (parser == null)
            throw new UnsupportedFileTypeException(mimeType);
        
        return await parser.Parse(fileStream);
    }
}
```

#### 挑战 3: 图片处理（多模态）
**问题**: 图片需要多模态模型支持，如何集成？

**解决方案 - 分阶段实现**:
```csharp
// Phase 1: 仅提取元数据
public class ImageMetadataExtractor
{
    public ImageMetadata Extract(Stream imageStream)
    {
        using var image = Image.Load(imageStream);
        
        return new ImageMetadata
        {
            Width = image.Width,
            Height = image.Height,
            Format = image.Metadata.DecodedImageFormat?.Name,
            FileSize = imageStream.Length,
            // EXIF 数据
            CameraMake = image.Metadata.ExifProfile?.GetValue(ExifTag.Make)?.Value,
            DateTime = image.Metadata.ExifProfile?.GetValue(ExifTag.DateTime)?.Value
        };
    }
}

// Phase 2: OCR 文字提取
public class OcrService
{
    private readonly Tesseract _engine;
    
    public async Task<string> ExtractText(Stream imageStream)
    {
        using var image = Pix.LoadFromMemory(imageStream);
        using var page = _engine.Process(image);
        return page.GetText();
    }
}

// Phase 3: 多模态模型集成（未来）
public class VisionLLMService
{
    public async Task<ImageAnalysis> AnalyzeImage(
        Stream imageStream,
        string prompt = "Describe this image in detail")
    {
        // 调用 GPT-4V, Claude 3 等多模态模型
        var base64Image = ConvertToBase64(imageStream);
        
        var response = await _llmClient.CompleteAsync(new[]
        {
            new Message { Role = "user", Content = prompt },
            new Message 
            { 
                Role = "user", 
                Content = new MultiModalContent 
                { 
                    Type = "image", 
                    Data = base64Image 
                } 
            }
        });
        
        return new ImageAnalysis { Description = response };
    }
}
```

### 2.2 技术风险评估

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|---------|
| 文件存储空间耗尽 | 高 | 中 | 配额管理，自动清理 |
| 恶意文件上传 | 中 | 高 | 文件类型白名单，病毒扫描 |
| 解析性能问题 | 中 | 中 | 异步处理，后台队列 |
| 多模态 API 成本高 | 高 | 中 | 可选功能，预算控制 |

### 2.3 原型验证方案

**阶段 1: 基础文件上传** (3 天)
```bash
# 验证目标
- 支持文本和代码文件上传
- 文件存储和检索
- 基础元数据提取

# 测试场景
/upload test.py              # 上传 Python 文件
/files list                  # 列出文件
/files show <id>             # 查看内容
```

**阶段 2: PDF 和 Markdown 支持** (4 天)
```bash
# 验证目标
- PDF 文本提取
- Markdown 解析和渲染
- 大文件处理（分块）

# 测试场景
/upload document.pdf --extract
/upload README.md
/analyze <file-id>           # 分析文件内容
```

**阶段 3: 高级功能** (可选)
```bash
# OCR 文字识别
/upload image.png --ocr

# 多模态分析（需要支持的模型）
/upload image.png --analyze
```

---

## 3️⃣ 长期记忆系统 - 深度技术分析

### 3.1 核心挑战

#### 挑战 1: 向量嵌入和相似度搜索
**问题**: 如何高效地存储和检索记忆？

**解决方案 - 嵌入向量数据库**:
```csharp
// 选项 1: 使用 SQLite 的 vector0 扩展
public class SqliteVectorStore
{
    public async Task StoreMemory(string content, float[] embedding)
    {
        await _db.ExecuteAsync(@"
            INSERT INTO memories (id, content, embedding)
            VALUES (@id, @content, vector(@embedding))
        ", new { id = Guid.NewGuid(), content, embedding });
    }
    
    public async Task<List<Memory>> SearchSimilar(float[] queryEmbedding, int limit = 10)
    {
        return await _db.QueryAsync<Memory>(@"
            SELECT id, content, 
                   vector_distance_cosine(embedding, vector(@query)) as similarity
            FROM memories
            ORDER BY similarity ASC
            LIMIT @limit
        ", new { query = queryEmbedding, limit });
    }
}

// 选项 2: 使用专用向量数据库（Qdrant, Milvus）
public class QdrantVectorStore
{
    private readonly QdrantClient _client;
    
    public async Task StoreMemory(string id, string content, float[] embedding)
    {
        await _client.UpsertAsync("memories", new[]
        {
            new PointStruct
            {
                Id = id,
                Vector = embedding,
                Payload = new Dictionary<string, object>
                {
                    ["content"] = content,
                    ["timestamp"] = DateTime.UtcNow
                }
            }
        });
    }
    
    public async Task<List<Memory>> SearchSimilar(
        float[] queryEmbedding, 
        int limit = 10,
        float minScore = 0.7f)
    {
        var results = await _client.SearchAsync("memories", 
            queryEmbedding, 
            limit: (ulong)limit,
            scoreThreshold: minScore);
        
        return results.Select(r => new Memory
        {
            Id = r.Id.ToString(),
            Content = r.Payload["content"].ToString(),
            Similarity = r.Score
        }).ToList();
    }
}
```

#### 挑战 2: 嵌入模型选择
**问题**: 使用哪个嵌入模型？成本如何？

**对比分析**:
```
| 模型 | 维度 | 质量 | 速度 | 成本 | 推荐场景 |
|------|------|------|------|------|---------|
| OpenAI text-embedding-3-small | 1536 | ⭐⭐⭐⭐ | 快 | $$ | 通用 |
| OpenAI text-embedding-3-large | 3072 | ⭐⭐⭐⭐⭐ | 中 | $$$ | 高质量 |
| Ollama nomic-embed-text | 768 | ⭐⭐⭐ | 快 | 免费 | 本地部署 |
| Sentence-Transformers | 384 | ⭐⭐⭐ | 快 | 免费 | 轻量级 |
```

**推荐方案**:
```csharp
public class EmbeddingService
{
    private readonly EmbeddingProvider _provider;
    
    public async Task<float[]> GetEmbedding(string text)
    {
        return _provider switch
        {
            EmbeddingProvider.OpenAI => await GetOpenAIEmbedding(text),
            EmbeddingProvider.Ollama => await GetOllamaEmbedding(text),
            EmbeddingProvider.Local => GetLocalEmbedding(text),
            _ => throw new NotSupportedException()
        };
    }
    
    // 本地模型（免费，隐私）
    private float[] GetLocalEmbedding(string text)
    {
        // 使用 ONNX Runtime + Sentence-Transformers 模型
        var tokens = _tokenizer.Encode(text);
        var inputTensor = CreateTensor(tokens);
        var results = _session.Run(inputTensor);
        return results[0].AsSpan<float>().ToArray();
    }
}
```

#### 挑战 3: 记忆重要性评估
**问题**: 如何自动评估记忆的重要性？

**解决方案 - 多因素评分**:
```csharp
public class MemoryImportanceCalculator
{
    public float CalculateImportance(Memory memory)
    {
        var scores = new[]
        {
            // 因素 1: 访问频率（0-1）
            NormalizeAccessCount(memory.AccessCount),
            
            // 因素 2: 时间衰减（0-1）
            CalculateTimeDecay(memory.CreatedAt),
            
            // 因素 3: 内容长度（0-1）
            NormalizeContentLength(memory.Content.Length),
            
            // 因素 4: 用户显式标记（0-1）
            memory.UserImportance ?? 0.5f,
            
            // 因素 5: 关联强度（0-1）
            CalculateRelationStrength(memory)
        };
        
        // 加权平均
        var weights = new[] { 0.3f, 0.2f, 0.1f, 0.3f, 0.1f };
        return scores.Zip(weights, (s, w) => s * w).Sum();
    }
    
    private float CalculateTimeDecay(DateTime createdAt)
    {
        var age = DateTime.UtcNow - createdAt;
        var daysSinceCreation = age.TotalDays;
        
        // 使用指数衰减
        // 30 天后降到 0.5，90 天后降到 0.1
        return (float)Math.Exp(-daysSinceCreation / 50);
    }
}
```

### 3.2 技术风险评估

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|---------|
| 向量搜索性能差 | 中 | 高 | 使用专用向量数据库，建立索引 |
| 嵌入 API 成本高 | 高 | 中 | 使用本地模型，批量处理 |
| 记忆冲突和矛盾 | 高 | 中 | 冲突检测，用户确认 |
| 隐私和数据安全 | 中 | 高 | 本地存储，加密敏感数据 |

### 3.3 原型验证方案

**阶段 1: 基础存储和检索** (5 天)
```csharp
// 验证目标
- 记忆存储到 SQLite
- 基于关键词的简单检索
- 记忆 CRUD 操作

// CLI 命令
/memory add "我喜欢用 Python 写代码"
/memory list
/memory search Python
```

**阶段 2: 向量嵌入集成** (7 天)
```csharp
// 验证目标
- 集成本地嵌入模型（Sentence-Transformers）
- 实现相似度搜索
- 性能测试（1000+ 记忆）

// CLI 命令
/memory add "我在开发一个 AI 助手项目"
/memory search "AI 项目"  # 应该找到上面的记忆
```

**阶段 3: 智能记忆管理** (5 天)
```csharp
// 验证目标
- 记忆重要性自动评分
- 记忆衰减和遗忘
- 记忆冲突检测

// 测试场景
- 添加 100 条记忆
- 模拟 30 天后的记忆衰减
- 检测矛盾信息
```

---

## 4️⃣ 计划任务 - 深度技术分析

### 4.1 核心挑战

#### 挑战 1: 跨平台后台服务
**问题**: CLI 工具退出后如何继续执行任务？

**解决方案 - 多种架构**:

**方案 A: 独立守护进程**
```csharp
// 优点：独立运行，可靠
// 缺点：需要额外的进程管理

// agent-scheduler 守护进程
public class SchedulerDaemon
{
    public async Task StartAsync()
    {
        _logger.LogInformation("Scheduler daemon starting...");
        
        // 加载任务
        var tasks = await _taskRepository.GetEnabledTasksAsync();
        
        // 启动调度器
        foreach (var task in tasks)
        {
            ScheduleTask(task);
        }
        
        // 保持运行
        await Task.Delay(Timeout.Infinite);
    }
    
    private void ScheduleTask(ScheduledTask task)
    {
        var schedule = CrontabSchedule.Parse(task.CronExpression);
        var nextOccurrence = schedule.GetNextOccurrence(DateTime.Now);
        
        var timer = new Timer(
            callback: async _ => await ExecuteTask(task),
            state: null,
            dueTime: nextOccurrence - DateTime.Now,
            period: Timeout.InfiniteTimeSpan
        );
        
        _timers[task.Id] = timer;
    }
}

// systemd 服务配置（Linux）
[Unit]
Description=General Agent Task Scheduler
After=network.target

[Service]
Type=simple
User=<username>
ExecStart=/usr/local/bin/agent-scheduler
Restart=always

[Install]
WantedBy=multi-user.target

// launchd 配置（macOS）
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" ...>
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.agent.scheduler</string>
    <key>ProgramArguments</key>
    <array>
        <string>/usr/local/bin/agent-scheduler</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
</dict>
</plist>
```

**方案 B: 系统任务调度器集成**
```csharp
// 优点：利用系统服务，简单
// 缺点：跨平台兼容性差

// Windows Task Scheduler
public class WindowsTaskScheduler
{
    public void CreateTask(ScheduledTask task)
    {
        using var ts = new TaskService();
        
        var td = ts.NewTask();
        td.RegistrationInfo.Description = task.Description;
        
        // 触发器
        var trigger = new TimeTrigger();
        trigger.StartBoundary = task.NextRun;
        td.Triggers.Add(trigger);
        
        // 操作
        td.Actions.Add(new ExecAction(
            "agent",
            $"task run {task.Id}",
            null));
        
        ts.RootFolder.RegisterTaskDefinition(
            $"AgentTask_{task.Id}",
            td);
    }
}

// Linux cron
public class CronTaskScheduler
{
    public void CreateTask(ScheduledTask task)
    {
        var cronLine = $"{task.CronExpression} agent task run {task.Id}";
        
        // 添加到 crontab
        var crontab = File.ReadAllText("/var/spool/cron/crontabs/<user>");
        crontab += $"\n{cronLine}";
        File.WriteAllText("/var/spool/cron/crontabs/<user>", crontab);
    }
}
```

**方案 C: 混合方案（推荐）**
```csharp
// 默认使用轻量级内置调度器
// 可选升级到守护进程

public class HybridScheduler
{
    public SchedulerMode Mode { get; set; } = SchedulerMode.Embedded;
    
    public async Task ScheduleTask(ScheduledTask task)
    {
        switch (Mode)
        {
            case SchedulerMode.Embedded:
                // 使用 Hangfire 或 Quartz.NET
                BackgroundJob.Schedule(
                    () => ExecuteTask(task.Id),
                    task.NextRun);
                break;
                
            case SchedulerMode.Daemon:
                // 注册到守护进程
                await _daemonClient.RegisterTaskAsync(task);
                break;
                
            case SchedulerMode.System:
                // 使用系统调度器
                _systemScheduler.CreateTask(task);
                break;
        }
    }
}
```

#### 挑战 2: 通知系统
**问题**: 如何跨平台地发送通知？

**解决方案**:
```csharp
public interface INotificationService
{
    Task SendNotification(string title, string message);
}

// Windows 通知
public class WindowsNotificationService : INotificationService
{
    public async Task SendNotification(string title, string message)
    {
        var toastContent = new ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .GetToastContent();
        
        var toast = new ToastNotification(toastContent.GetXml());
        ToastNotificationManager.CreateToastNotifier().Show(toast);
    }
}

// macOS 通知
public class MacOSNotificationService : INotificationService
{
    public async Task SendNotification(string title, string message)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e 'display notification \"{message}\" with title \"{title}\"'",
                UseShellExecute = false
            }
        };
        process.Start();
        await process.WaitForExitAsync();
    }
}

// Linux 通知（libnotify）
public class LinuxNotificationService : INotificationService
{
    public async Task SendNotification(string title, string message)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "notify-send",
                Arguments = $"\"{title}\" \"{message}\"",
                UseShellExecute = false
            }
        };
        process.Start();
        await process.WaitForExitAsync();
    }
}
```

### 4.2 技术风险评估

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|---------|
| 守护进程稳定性 | 中 | 高 | 自动重启，错误恢复 |
| 跨平台兼容性 | 高 | 中 | 多种调度方案，降级策略 |
| 任务执行失败 | 中 | 中 | 重试机制，错误通知 |
| 时区处理错误 | 低 | 中 | 统一使用 UTC，显示转换 |

### 4.3 原型验证方案

**阶段 1: 基础任务调度** (4 天)
```bash
# 验证目标
- 创建和管理任务
- 基于时间戳的一次性任务
- 任务执行和状态追踪

# CLI 命令
/schedule add "测试提醒" --at "2026-03-27 14:00"
/schedule list
/schedule run <id>  # 手动执行
```

**阶段 2: Cron 表达式支持** (3 天)
```bash
# 验证目标
- 解析 Cron 表达式
- 计算下次执行时间
- 循环任务执行

# CLI 命令
/schedule add "每日备份" --cron "0 9 * * *"
/schedule next <id>  # 查看下次执行时间
```

**阶段 3: 通知系统** (3 天)
```bash
# 验证目标
- 跨平台通知
- 通知历史
- 通知设置

# 测试场景
- 到期自动通知
- 任务执行结果通知
- 错误通知
```

---

## 5️⃣ Skill 抽取 - 深度技术分析

### 4.1 核心挑战

#### 挑战 1: 模式识别准确性
**问题**: 如何准确识别对话中的可复用模式？

**解决方案 - 多层检测**:
```csharp
public class PatternDetector
{
    // 第 1 层：关键词匹配
    public List<Pattern> DetectByKeywords(List<Message> messages)
    {
        var patterns = new List<Pattern>();
        
        // 检测常见模式
        if (ContainsPattern(messages, ["翻译", "translate"]))
        {
            patterns.Add(new Pattern
            {
                Type = PatternType.Translation,
                Confidence = 0.8f,
                Examples = ExtractExamples(messages, ["翻译"])
            });
        }
        
        return patterns;
    }
    
    // 第 2 层：结构化模板匹配
    public List<Pattern> DetectByTemplate(List<Message> messages)
    {
        // 识别 "请帮我 X" 这样的模板
        var regex = new Regex(@"请帮我\s+(.+?)(?:[。，！]|$)");
        
        var matches = messages
            .Select(m => regex.Match(m.Content))
            .Where(m => m.Success)
            .ToList();
        
        if (matches.Count >= 3) // 出现 3 次以上
        {
            return new List<Pattern>
            {
                new Pattern
                {
                    Type = PatternType.Request,
                    Template = "请帮我 {action}",
                    Confidence = 0.7f,
                    Frequency = matches.Count
                }
            };
        }
        
        return new List<Pattern>();
    }
    
    // 第 3 层：LLM 语义分析
    public async Task<List<Pattern>> DetectBySemantic(List<Message> messages)
    {
        // 使用 LLM 分析对话模式
        var prompt = $@"
分析以下对话，识别可以提取为技能的重复模式：

{string.Join("\n", messages.Select(m => $"{m.Role}: {m.Content}"))}

请列出：
1. 模式描述
2. 参数列表
3. 置信度 (0-1)
4. 示例对话
";
        
        var response = await _llmClient.CompleteAsync(prompt);
        return ParsePatterns(response);
    }
}
```

#### 挑战 2: 参数自动提取
**问题**: 如何从示例中提取参数定义？

**解决方案**:
```csharp
public class ParameterExtractor
{
    public async Task<List<SkillParameter>> ExtractParameters(
        List<Message> examples)
    {
        var parameters = new List<SkillParameter>();
        
        // 方法 1: 变化点检测
        var variations = FindVariations(examples);
        foreach (var (placeholder, examples) in variations)
        {
            parameters.Add(new SkillParameter
            {
                Name = InferParameterName(placeholder),
                Type = InferParameterType(examples),
                Required = true,
                Description = $"从示例中提取：{string.Join(", ", examples)}"
            });
        }
        
        // 方法 2: LLM 分析
        var llmParams = await AnalyzeWithLLM(examples);
        parameters.AddRange(llmParams);
        
        // 去重和合并
        return MergeParameters(parameters);
    }
    
    private Dictionary<string, List<string>> FindVariations(
        List<Message> examples)
    {
        // 使用最长公共子序列找到变化部分
        var commonParts = FindCommonSubsequence(
            examples.Select(m => m.Content).ToList());
        
        var variations = new Dictionary<string, List<string>>();
        
        foreach (var example in examples)
        {
            var variable = example.Content.Replace(commonParts, "");
            if (!string.IsNullOrWhiteSpace(variable))
            {
                if (!variations.ContainsKey("var1"))
                    variations["var1"] = new List<string>();
                variations["var1"].Add(variable.Trim());
            }
        }
        
        return variations;
    }
    
    private string InferParameterType(List<string> examples)
    {
        // 类型推断
        if (examples.All(e => int.TryParse(e, out _)))
            return "integer";
        if (examples.All(e => float.TryParse(e, out _)))
            return "number";
        if (examples.All(e => DateTime.TryParse(e, out _)))
            return "datetime";
        
        return "string";
    }
}
```

#### 挑战 3: 技能质量保证
**问题**: 生成的技能可能不准确或不完整

**解决方案 - 迭代优化流程**:
```csharp
public class SkillGenerationWorkflow
{
    public async Task<Skill> GenerateSkill(Pattern pattern)
    {
        // 步骤 1: 生成初始版本
        var draft = await GenerateDraft(pattern);
        
        // 步骤 2: 用户预览和反馈
        var feedback = await RequestUserFeedback(draft);
        
        // 步骤 3: 根据反馈优化
        var improved = await ImproveSkill(draft, feedback);
        
        // 步骤 4: 测试验证
        var testResults = await TestSkill(improved, pattern.Examples);
        
        // 步骤 5: 用户确认
        if (await RequestUserApproval(improved, testResults))
        {
            return await SaveSkill(improved);
        }
        
        // 步骤 6: 继续迭代
        return await GenerateSkill(pattern);
    }
    
    private async Task<SkillTestResults> TestSkill(
        Skill skill,
        List<Message> examples)
    {
        var results = new SkillTestResults();
        
        foreach (var example in examples)
        {
            // 提取参数值
            var parameters = ExtractParameterValues(example, skill);
            
            // 执行技能
            var output = await ExecuteSkill(skill, parameters);
            
            // 与原始输出对比
            var similarity = CalculateSimilarity(output, example.Content);
            
            results.Add(new TestResult
            {
                Input = example,
                Expected = example.Content,
                Actual = output,
                Similarity = similarity,
                Passed = similarity > 0.8f
            });
        }
        
        return results;
    }
}
```

### 5.2 技术风险评估

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|---------|
| 模式识别误报率高 | 高 | 中 | 用户确认，置信度阈值 |
| 参数提取不准确 | 高 | 高 | 人工审核，迭代优化 |
| 生成技能质量差 | 中 | 中 | 测试验证，用户反馈 |
| LLM 调用成本高 | 高 | 中 | 批量处理，缓存结果 |

### 5.3 原型验证方案

**阶段 1: 基础模式检测** (5 天)
```bash
# 验证目标
- 识别简单重复模式（关键词）
- 频率统计
- 用户确认流程

# 测试场景
You> 请帮我翻译 "Hello"
You> 请帮我翻译 "World"
You> 请帮我翻译 "Python"

/skill patterns
# 输出：检测到 "翻译" 模式，出现 3 次
```

**阶段 2: 参数提取和技能生成** (7 天)
```bash
# 验证目标
- 自动提取参数
- 生成技能定义
- 保存到文件系统

# 工作流
/skill create <pattern-id>
# 1. 显示提取的参数
# 2. 请求用户确认
# 3. 生成 YAML + MD 文件
# 4. 测试技能
```

**阶段 3: 质量优化** (5 天)
```bash
# 验证目标
- 技能测试和验证
- 用户反馈收集
- 迭代改进

# 测试场景
- 生成 10 个不同类型的技能
- 评估准确率和可用性
- 优化算法
```

---

## 📊 综合评估和建议

### 推荐实施顺序

**立即开始 (Phase 6)**:
1. **上下文压缩** 
   - 风险：⭐⭐（低）
   - 价值：⭐⭐⭐⭐⭐（高）
   - 时间：1-2 周
   - **建议**: 从滑动窗口开始，逐步增强

2. **文件上传支持**
   - 风险：⭐⭐⭐（中）
   - 价值：⭐⭐⭐⭐⭐（高）
   - 时间：2-3 周
   - **建议**: 先支持文本/代码，再扩展到 PDF

**近期实施 (Phase 7)**:
3. **计划任务**
   - 风险：⭐⭐⭐（中）
   - 价值：⭐⭐⭐（中）
   - 时间：2-3 周
   - **建议**: 使用混合调度方案

4. **长期记忆系统**
   - 风险：⭐⭐⭐⭐（高）
   - 价值：⭐⭐⭐⭐（高）
   - 时间：3-4 周
   - **建议**: 优先本地方案，降低成本

**长期规划 (Phase 8+)**:
5. **Skill 抽取**
   - 风险：⭐⭐⭐⭐⭐（高）
   - 价值：⭐⭐⭐（中）
   - 时间：4-5 周
   - **建议**: 等其他功能稳定后再实施

### 关键决策点

#### 决策 1: 向量数据库选择
**选项**:
- A. SQLite + vector0 扩展（简单，但功能有限）
- B. Qdrant（功能强大，但需要额外服务）
- C. 本地嵌入 + 简单余弦相似度（最简单）

**建议**: 先用 C 验证，再根据需求升级到 B

#### 决策 2: 文件存储位置
**选项**:
- A. 本地文件系统（~/.agent/uploads/）
- B. SQLite BLOB（简单但有大小限制）
- C. 云存储（S3 等，成本高）

**建议**: 先用 A，提供 B 作为备选

#### 决策 3: 任务调度方式
**选项**:
- A. 独立守护进程（复杂但可靠）
- B. 系统任务调度器（简单但跨平台差）
- C. Hangfire/Quartz.NET（中等复杂度）

**建议**: 先用 C，提供 A 作为高级选项

---

## 🎯 下一步行动建议

### 立即可做:
1. ✅ 创建 Phase 6 详细实施计划
2. ✅ 搭建上下文压缩原型
3. ✅ 评估依赖库和工具

### 本周内:
1. ⏸ 完成上下文压缩 MVP
2. ⏸ 开始文件上传设计
3. ⏸ 准备技术选型文档

### 下周:
1. ⏸ 完成文件上传 MVP
2. ⏸ 集成测试
3. ⏸ 用户测试和反馈

---

**文档创建**: 2026-03-26
**创建者**: Claude Sonnet 4.5
**状态**: ✅ 技术分析完成，等待实施决策
