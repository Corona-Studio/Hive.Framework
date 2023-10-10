using System.Reflection;
using System.Text;
using Hive.Server.Abstractions;

namespace Hive.Server.Cluster.RSC;

public class ReflectionExecutor: IRemoteServiceCallExecutor
{
    private IServiceProvider _serviceProvider;

    public ReflectionExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private Type GetServiceType(ServiceAddress serviceAddress)
    {
        return typeof(object);
    }
    
    [ThreadStatic] private static byte[]? _buffer;

    private static byte[] Buffer => _buffer ??= new byte[1024];

    public void OnCall(ServiceAddress serviceAddress, string methodName, Stream serializedArguments, Stream serializedResult)
    {
        var targetServiceType = GetServiceType(serviceAddress);
        var service = _serviceProvider.GetService(targetServiceType);
        
        var method = targetServiceType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        
        var arguments = new List<object>();
        
        foreach (var parameter in method.GetParameters())
        {
            arguments.Add(ReadParameter(parameter.ParameterType, serializedArguments));
        }
        
        var result = method.Invoke(service, arguments.ToArray());
        WriteParameter(method.ReturnType, result, serializedResult);
    }

    private void WriteParameter(Type parameterType, object obj, Stream stream)
    {
        switch (parameterType)
        {
            case var _ when parameterType == typeof(bool):
                WriteParameter(obj, stream, (o, buffer) =>
                {
                    BitConverter.TryWriteBytes(buffer.AsSpan(), (bool) o);
                    return 1;
                });
                break;
            case var _ when parameterType == typeof(byte):
                WriteParameter(obj, stream, (o, buffer) =>
                {
                    buffer[0] = (byte) o;
                    return 1;
                });
                break;
            case var _ when parameterType == typeof(sbyte):
                WriteParameter(obj, stream, (o, buffer) =>
                {
                    buffer[0] = (byte) (sbyte) o;
                    return 1;
                });
                break;
            case var _ when parameterType == typeof(short):
                WriteParameter(obj, stream, (o, buffer) =>
                {
                    BitConverter.TryWriteBytes(buffer.AsSpan(), (short) o);
                    return 2;
                });
                break;
            case var _ when parameterType == typeof(ushort):
                WriteParameter(obj, stream, (o, buffer) =>
                {
                    BitConverter.TryWriteBytes(buffer.AsSpan(), (ushort) o);
                    return 2;
                });
                break;
            case var _ when parameterType == typeof(int):
                WriteParameter(obj, stream, (o, buffer) =>
                {
                    BitConverter.TryWriteBytes(buffer.AsSpan(), (int) o);
                    return 4;
                });
                break;
            case var _ when parameterType == typeof(uint):
                WriteParameter(obj, stream, (o, buffer) =>
                {
                    BitConverter.TryWriteBytes(buffer.AsSpan(), (uint) o);
                    return 4;
                });
                break;
            case var _ when parameterType == typeof(long):
                WriteParameter(obj, stream, (o, buffer) =>
                {
                    BitConverter.TryWriteBytes(buffer.AsSpan(), (long) o);
                    return 8;
                });
                break;
            case var _ when parameterType == typeof(ulong):
                WriteParameter(obj, stream, (o, buffer) =>
                {
                    BitConverter.TryWriteBytes(buffer.AsSpan(), (ulong) o);
                    return 8;
                });
                break;
            case var _ when parameterType == typeof(float):
                WriteParameter(obj, stream, (o, buffer) =>
                {
                    BitConverter.TryWriteBytes(buffer.AsSpan(), (float) o);
                    return 4;
                });
                break;
            case var _ when parameterType == typeof(double):
                WriteParameter(obj, stream, (o, buffer) =>
                {
                    BitConverter.TryWriteBytes(buffer.AsSpan(), (double) o);
                    return 8;
                });
                break;
            case var _ when parameterType == typeof(decimal):
                WriteVarLenParameter(obj, stream, (o, buffer) =>
                {
                    var bits = decimal.GetBits((decimal) o);
                    BitConverter.TryWriteBytes(buffer.AsSpan(), bits.Length);
                    for (var i = 0; i < bits.Length; i++)
                    {
                        BitConverter.TryWriteBytes(buffer.AsSpan(4 + i * 4), bits[i]);
                    }

                    return 4 + bits.Length * 4;
                });
                break;
            case var _ when parameterType == typeof(string):
                WriteVarLenParameter(obj, stream, (o, buffer) =>
                {
                    var bytes = Encoding.UTF8.GetBytes((string) o);
                    BitConverter.TryWriteBytes(buffer.AsSpan(), bytes.Length);
                    bytes.CopyTo(buffer.AsSpan(4));
                    return 4 + bytes.Length;
                });
                break;
            default:
                    throw new NotSupportedException();
        }
    }
    
    private object ReadParameter(Type parameterType, Stream stream)
    {
        switch (parameterType)
        {
            case var _ when parameterType == typeof(bool):
                return ReadParameter(stream, 1, buffer => BitConverter.ToBoolean(buffer.Span));
            case var _ when parameterType == typeof(byte):
                return ReadParameter(stream, 1, buffer => buffer.Span[0]);
            case var _ when parameterType == typeof(sbyte):
                return ReadParameter(stream, 1, buffer => (sbyte) buffer.Span[0]);
            case var _ when parameterType == typeof(short):
                return ReadParameter(stream, 2, buffer => BitConverter.ToInt16(buffer.Span));
            case var _ when parameterType == typeof(ushort):
                return ReadParameter(stream, 2, buffer => BitConverter.ToUInt16(buffer.Span));
            case var _ when parameterType == typeof(int):
                return ReadParameter(stream, 4, buffer => BitConverter.ToInt32(buffer.Span));
            case var _ when parameterType == typeof(uint):
                return ReadParameter(stream, 4, buffer => BitConverter.ToUInt32(buffer.Span));
            case var _ when parameterType == typeof(long):
                return ReadParameter(stream, 8, buffer => BitConverter.ToInt64(buffer.Span));
            case var _ when parameterType == typeof(ulong):
                return ReadParameter(stream, 8, buffer => BitConverter.ToUInt64(buffer.Span));
            case var _ when parameterType == typeof(float):
                return ReadParameter(stream, 4, buffer => BitConverter.ToSingle(buffer.Span));
            case var _ when parameterType == typeof(double):
                return ReadParameter(stream, 8, buffer => BitConverter.ToDouble(buffer.Span));
            case var _ when parameterType == typeof(decimal):
                return ReadVarLenParameter(stream, buffer =>
                {
                    var length = BitConverter.ToInt32(buffer.Span);
                    Span<int> bits = stackalloc int[length];
                    for (var i = 0; i < length; i++)
                    {
                        bits[i] = BitConverter.ToInt32(buffer.Span.Slice(4 + i * 4));
                    }

                    return new decimal(bits);
                });
            
            case var _ when parameterType == typeof(string):
                return ReadVarLenParameter(stream, buffer =>
                {
                    var length = BitConverter.ToInt32(buffer.Span);
                    return Encoding.UTF8.GetString(buffer.Span.Slice(4, length));
                });
            default:
                throw new NotSupportedException();
        }
    }

    private static object ReadParameter(Stream stream,int size,Func<Memory<byte>,object> converter)
    {
        var read = stream.Read(Buffer, 0, size);
        if (read != size)
        {
            throw new InvalidDataException();
        }

        return converter(Buffer.AsMemory(0,size));
    }
    
    private static object ReadVarLenParameter(Stream stream,Func<Memory<byte>,object> converter)
    {
        var read = stream.Read(Buffer, 0, 4);
        if (read != 4)
        {
            throw new InvalidDataException();
        }
        
        var size = BitConverter.ToInt32(Buffer);
        read = stream.Read(Buffer, 0, size);
        if (read != size)
        {
            throw new InvalidDataException();
        }

        return converter(Buffer.AsMemory(0,size));
    }
    
    private static void WriteParameter(object parameter,Stream stream,Func<object,byte[],int> converter)
    {
        var len = converter(parameter,Buffer);
        stream.Write(Buffer,0,len);
    }
    
    private static void WriteVarLenParameter(object parameter,Stream stream,Func<object,byte[],int> converter)
    {
        var len = converter(parameter,Buffer);
        Span<byte> lenBuffer = stackalloc byte[4];
        BitConverter.TryWriteBytes(lenBuffer,len);
        stream.Write(lenBuffer);
        stream.Write(Buffer,0,len);
    }
}