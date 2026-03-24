using System.Text.Json.Nodes;
using FluentAssertions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.LLM.Serializers;

namespace GeneralAgent.Infrastructure.LLM.Tests.Serializers;

/// <summary>
/// OpenAI 工具序列化器测试
/// </summary>
public class OpenAIToolSerializerTests
{
    private readonly OpenAIToolSerializer _serializer = new();

    [Fact]
    public void SerializeToolDefinition_ShouldProduceOpenAIFormat()
    {
        // Arrange
        var toolDef = new ToolDefinition
        {
            Name = "get_weather",
            Description = "获取指定城市的天气信息",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["city"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "城市名称"
                    }
                },
                ["required"] = new JsonArray { "city" }
            }
        };

        // Act
        var result = _serializer.SerializeToolDefinition(toolDef);

        // Assert
        result.Should().NotBeNull();
        result["type"]?.ToString().Should().Be("function");

        var function = result["function"]?.AsObject();
        function.Should().NotBeNull();
        function!["name"]?.ToString().Should().Be("get_weather");
        function["description"]?.ToString().Should().Be("获取指定城市的天气信息");

        var parameters = function["parameters"]?.AsObject();
        parameters.Should().NotBeNull();
        parameters!["type"]?.ToString().Should().Be("object");

        var properties = parameters["properties"]?.AsObject();
        properties.Should().NotBeNull();
        properties!["city"].Should().NotBeNull();
    }

    [Fact]
    public void SerializeTools_ShouldProduceArray()
    {
        // Arrange
        var tools = new[]
        {
            new ToolDefinition
            {
                Name = "tool1",
                Description = "第一个工具",
                InputSchema = new JsonObject { ["type"] = "object" }
            },
            new ToolDefinition
            {
                Name = "tool2",
                Description = "第二个工具",
                InputSchema = new JsonObject { ["type"] = "object" }
            }
        };

        // Act
        var result = _serializer.SerializeTools(tools);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result[0]?["type"]?.ToString().Should().Be("function");
        result[1]?["type"]?.ToString().Should().Be("function");

        var function1 = result[0]?["function"]?.AsObject();
        function1?["name"]?.ToString().Should().Be("tool1");

        var function2 = result[1]?["function"]?.AsObject();
        function2?["name"]?.ToString().Should().Be("tool2");
    }

    [Fact]
    public void SerializeToolDefinition_ComplexSchema_ShouldPreserveStructure()
    {
        // Arrange - 复杂的 JSON Schema，包含嵌套属性、必填字段、默认值
        var toolDef = new ToolDefinition
        {
            Name = "complex_tool",
            Description = "复杂工具示例",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["user"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["name"] = new JsonObject { ["type"] = "string" },
                            ["age"] = new JsonObject { ["type"] = "number", ["default"] = 18 }
                        },
                        ["required"] = new JsonArray { "name" }
                    },
                    ["tags"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject { ["type"] = "string" }
                    }
                },
                ["required"] = new JsonArray { "user" }
            }
        };

        // Act
        var result = _serializer.SerializeToolDefinition(toolDef);

        // Assert
        var parameters = result["function"]?.AsObject()?["parameters"]?.AsObject();
        parameters.Should().NotBeNull();

        // 验证嵌套结构保持不变
        var userProp = parameters!["properties"]?.AsObject()?["user"]?.AsObject();
        userProp.Should().NotBeNull();
        userProp!["type"]?.ToString().Should().Be("object");

        var userProps = userProp["properties"]?.AsObject();
        userProps.Should().NotBeNull();
        userProps!["name"].Should().NotBeNull();
        userProps["age"]?.AsObject()?["default"]?.GetValue<int>().Should().Be(18);

        // 验证必填字段
        var required = parameters["required"]?.AsArray();
        required.Should().NotBeNull();
        required!.Count.Should().Be(1);
        required[0]?.ToString().Should().Be("user");
    }

    [Fact]
    public void SerializeTools_EmptyList_ShouldReturnEmptyArray()
    {
        // Arrange
        var tools = Array.Empty<ToolDefinition>();

        // Act
        var result = _serializer.SerializeTools(tools);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(0);
    }
}

/// <summary>
/// Anthropic 工具序列化器测试
/// </summary>
public class AnthropicToolSerializerTests
{
    private readonly AnthropicToolSerializer _serializer = new();

    [Fact]
    public void SerializeToolDefinition_ShouldProduceAnthropicFormat()
    {
        // Arrange
        var toolDef = new ToolDefinition
        {
            Name = "get_weather",
            Description = "获取指定城市的天气信息",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["city"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "城市名称"
                    }
                },
                ["required"] = new JsonArray { "city" }
            }
        };

        // Act
        var result = _serializer.SerializeToolDefinition(toolDef);

        // Assert
        result.Should().NotBeNull();
        result["name"]?.ToString().Should().Be("get_weather");
        result["description"]?.ToString().Should().Be("获取指定城市的天气信息");

        var inputSchema = result["input_schema"]?.AsObject();
        inputSchema.Should().NotBeNull();
        inputSchema!["type"]?.ToString().Should().Be("object");

        var properties = inputSchema["properties"]?.AsObject();
        properties.Should().NotBeNull();
        properties!["city"].Should().NotBeNull();
    }

    [Fact]
    public void SerializeTools_ShouldProduceArray()
    {
        // Arrange
        var tools = new[]
        {
            new ToolDefinition
            {
                Name = "tool1",
                Description = "第一个工具",
                InputSchema = new JsonObject { ["type"] = "object" }
            },
            new ToolDefinition
            {
                Name = "tool2",
                Description = "第二个工具",
                InputSchema = new JsonObject { ["type"] = "object" }
            }
        };

        // Act
        var result = _serializer.SerializeTools(tools);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result[0]?["name"]?.ToString().Should().Be("tool1");
        result[1]?["name"]?.ToString().Should().Be("tool2");
        result[0]?["input_schema"].Should().NotBeNull();
        result[1]?["input_schema"].Should().NotBeNull();
    }

    [Fact]
    public void SerializeToolDefinition_ComplexSchema_ShouldPreserveStructure()
    {
        // Arrange - 复杂的 JSON Schema，包含嵌套属性、必填字段、默认值
        var toolDef = new ToolDefinition
        {
            Name = "complex_tool",
            Description = "复杂工具示例",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["user"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["name"] = new JsonObject { ["type"] = "string" },
                            ["age"] = new JsonObject { ["type"] = "number", ["default"] = 18 }
                        },
                        ["required"] = new JsonArray { "name" }
                    },
                    ["tags"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject { ["type"] = "string" }
                    }
                },
                ["required"] = new JsonArray { "user" }
            }
        };

        // Act
        var result = _serializer.SerializeToolDefinition(toolDef);

        // Assert
        var inputSchema = result["input_schema"]?.AsObject();
        inputSchema.Should().NotBeNull();

        // 验证嵌套结构保持不变
        var userProp = inputSchema!["properties"]?.AsObject()?["user"]?.AsObject();
        userProp.Should().NotBeNull();
        userProp!["type"]?.ToString().Should().Be("object");

        var userProps = userProp["properties"]?.AsObject();
        userProps.Should().NotBeNull();
        userProps!["name"].Should().NotBeNull();
        userProps["age"]?.AsObject()?["default"]?.GetValue<int>().Should().Be(18);

        // 验证必填字段
        var required = inputSchema["required"]?.AsArray();
        required.Should().NotBeNull();
        required!.Count.Should().Be(1);
        required[0]?.ToString().Should().Be("user");
    }

    [Fact]
    public void SerializeTools_EmptyList_ShouldReturnEmptyArray()
    {
        // Arrange
        var tools = Array.Empty<ToolDefinition>();

        // Act
        var result = _serializer.SerializeTools(tools);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(0);
    }
}
