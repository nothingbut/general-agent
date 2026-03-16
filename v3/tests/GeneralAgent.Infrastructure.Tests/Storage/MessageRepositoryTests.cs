using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Storage;
using GeneralAgent.Infrastructure.Storage.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Tests.Storage;

public class MessageRepositoryTests : IDisposable
{
    private readonly AgentDbContext _context;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly Guid _testSessionId;

    public MessageRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new AgentDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _sessionRepository = new SessionRepository(_context);
        _messageRepository = new MessageRepository(_context);

        var session = Session.Create(title: "Test Session");
        _sessionRepository.CreateAsync(session).Wait();
        _testSessionId = session.Id;
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistMessage()
    {
        var message = Message.CreateUser(_testSessionId, "Hello");
        var created = await _messageRepository.CreateAsync(message);
        created.Should().NotBeNull();
        created.Content.Should().Be("Hello");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
    {
        var result = await _messageRepository.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySessionAsync_ShouldReturnAllMessages()
    {
        await _messageRepository.CreateAsync(Message.CreateUser(_testSessionId, "Message 1"));
        await _messageRepository.CreateAsync(Message.CreateAssistant(_testSessionId, "Response 1"));
        var messages = await _messageRepository.GetBySessionAsync(_testSessionId);
        messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRecentAsync_ShouldReturnLimitedMessages()
    {
        for (int i = 0; i < 10; i++)
        {
            await _messageRepository.CreateAsync(Message.CreateUser(_testSessionId, $"Message {i}"));
            await Task.Delay(10);
        }
        var recent = await _messageRepository.GetRecentAsync(_testSessionId, limit: 5);
        recent.Should().HaveCount(5);
    }

    [Fact]
    public async Task CountAsync_ShouldReturnCorrectCount()
    {
        await _messageRepository.CreateAsync(Message.CreateUser(_testSessionId, "Message 1"));
        await _messageRepository.CreateAsync(Message.CreateAssistant(_testSessionId, "Response 1"));
        var count = await _messageRepository.CountAsync(_testSessionId);
        count.Should().Be(2);
    }

    [Fact]
    public async Task DeleteBySessionAsync_ShouldRemoveAllMessages()
    {
        await _messageRepository.CreateAsync(Message.CreateUser(_testSessionId, "Message 1"));
        await _messageRepository.DeleteBySessionAsync(_testSessionId);
        var messages = await _messageRepository.GetBySessionAsync(_testSessionId);
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteBySessionAsync_WhenSessionDeleted_ShouldCascadeDelete()
    {
        await _messageRepository.CreateAsync(Message.CreateUser(_testSessionId, "Message 1"));
        await _sessionRepository.DeleteAsync(_testSessionId);
        var messages = await _messageRepository.GetBySessionAsync(_testSessionId);
        messages.Should().BeEmpty();
    }
}
