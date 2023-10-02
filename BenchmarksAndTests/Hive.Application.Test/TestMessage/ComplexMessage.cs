using Hive.Codec.Shared;
using MemoryPack;

namespace Hive.Application.Test.TestMessage;

[MemoryPackable]
[MessageDefine]
public partial class ComplexMessage : IEquatable<ComplexMessage>, IEqualityComparer<ComplexMessage>
{
    public ComplexMessage()
    {
        Random rnd = new Random();
        Id = 0;
        Name = "ComplexMessage";
        Numbers = new int[10];
        Names = new string[10];
        Messages = new ComplexMessage[10];
        NullNumbers = new int[5];

        for (int i = 0; i < 10; i++)
        {
            Numbers[i] = rnd.Next();
        }
    }
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int[] Numbers { get; set; } = new int[0];
    public string[] Names { get; set; } = new string[0];
    public ComplexMessage[] Messages { get; set; } = new ComplexMessage[0];
    public ComplexMessage? Message { get; set; }
    public int[]? NullNumbers { get; set; }
    public string[]? NullNames { get; set; }
    public ComplexMessage[]? NullMessages { get; set; }
    public ComplexMessage? NullMessage { get; set; }
    public int? NullableId { get; set; }
    public string? NullableName { get; set; }

    public bool Equals(ComplexMessage? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id && Name == other.Name && Numbers.Equals(other.Numbers) && Names.Equals(other.Names) && Messages.Equals(other.Messages) && Equals(Message, other.Message) && Equals(NullNumbers, other.NullNumbers) && Equals(NullNames, other.NullNames) && Equals(NullMessages, other.NullMessages) && Equals(NullMessage, other.NullMessage) && NullableId == other.NullableId && NullableName == other.NullableName;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ComplexMessage)obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Id);
        hashCode.Add(Name);
        hashCode.Add(Numbers);
        hashCode.Add(Names);
        hashCode.Add(Messages);
        hashCode.Add(Message);
        hashCode.Add(NullNumbers);
        hashCode.Add(NullNames);
        hashCode.Add(NullMessages);
        hashCode.Add(NullMessage);
        hashCode.Add(NullableId);
        hashCode.Add(NullableName);
        return hashCode.ToHashCode();
    }

    public bool Equals(ComplexMessage? x, ComplexMessage? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.Id == y.Id && x.Name == y.Name && x.Numbers.Equals(y.Numbers) && x.Names.Equals(y.Names) && x.Messages.Equals(y.Messages) && Equals(x.Message, y.Message) && Equals(x.NullNumbers, y.NullNumbers) && Equals(x.NullNames, y.NullNames) && Equals(x.NullMessages, y.NullMessages) && Equals(x.NullMessage, y.NullMessage) && x.NullableId == y.NullableId && x.NullableName == y.NullableName;
    }

    public int GetHashCode(ComplexMessage obj)
    {
        var hashCode = new HashCode();
        hashCode.Add(obj.Id);
        hashCode.Add(obj.Name);
        hashCode.Add(obj.Numbers);
        hashCode.Add(obj.Names);
        hashCode.Add(obj.Messages);
        hashCode.Add(obj.Message);
        hashCode.Add(obj.NullNumbers);
        hashCode.Add(obj.NullNames);
        hashCode.Add(obj.NullMessages);
        hashCode.Add(obj.NullMessage);
        hashCode.Add(obj.NullableId);
        hashCode.Add(obj.NullableName);
        return hashCode.ToHashCode();
    }
}