using System;
using System.Collections.Generic;

namespace GeneralAgent.Tests;

/// <summary>
/// 示例类 - 用于测试代码文件处理
/// </summary>
public class SampleClass
{
    private readonly string _name;

    public SampleClass(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string GetGreeting()
    {
        return $"Hello, {_name}!";
    }

    public List<int> GenerateNumbers(int count)
    {
        var numbers = new List<int>();
        for (int i = 0; i < count; i++)
        {
            numbers.Add(i);
        }
        return numbers;
    }
}
