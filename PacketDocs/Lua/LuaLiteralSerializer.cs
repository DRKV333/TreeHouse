using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Common;
using YamlDotNet.Serialization;

namespace PacketDocs.Lua;

internal class LuaLiteralSerializer
{
    private sealed class LuaPropertyInfo
    {
        public string Name { get; }

        public Func<object, object?> Get { get; }

        public LuaPropertyInfo(PropertyInfo info)
        {
            YamlMemberAttribute? attr = info.GetCustomAttribute<YamlMemberAttribute>();
            if (attr?.Alias != null)
                Name = attr.Alias;
            else
                Name = info.Name;

            MethodInfo? getterMethod = info.GetGetMethod();
            if (getterMethod == null)
                throw new InvalidOperationException("Property is not gettable");

            ParameterExpression getterParam = Expression.Parameter(typeof(object));
            Get = (Func<object, object>)Expression.Lambda(
                Expression.Convert(
                    Expression.Call(
                        Expression.Convert(
                            getterParam,
                            getterMethod.DeclaringType!
                        ),
                        getterMethod
                    ),
                    typeof(object)
                ),
                getterParam
            ).Compile();
        }
    }

    private readonly HashSet<Assembly> assemblyWhitelist = new() { Assembly.GetExecutingAssembly() };

    private readonly Dictionary<Type, Func<object, object>> transformers = new();

    private readonly Dictionary<Type, List<LuaPropertyInfo>> objectProps = new();

    public void AddWhitelistAssembly(Assembly assembly) => assemblyWhitelist.Add(assembly);

    public void AddObjectTransformer<T>(Func<T, object> func) => transformers.Add(typeof(T), x => func((T)x));

    public Task Serialize(object obj, TextWriter writer)
    {
        if (obj is string s)
            return SerializeString(s, writer);
        else if (IsNumber(obj))
            return SerializePrimitive(obj, writer);
        else if (obj is IDictionary dict)
            return SerializeDictionary(dict, writer);
        else if (obj is IEnumerable enumerable)
            return SerializeEnumerable(enumerable, writer);
        else
            return SerializeObject(obj, writer);
    }

    private static async Task SerializeString(string s, TextWriter writer)
    {
        await writer.WriteAsync('"');
        await writer.WriteAsync(s);
        await writer.WriteAsync('"');
    }

    private static Task SerializePrimitive(object obj, TextWriter writer)
    {
        if (obj is IFormattable formattable)
            return writer.WriteAsync(formattable.ToString(null, CultureInfo.InvariantCulture));
        else
            return writer.WriteAsync(obj.ToString());
    }

    private async Task SerializeDictionary(IDictionary dict, TextWriter writer)
    {
        await writer.WriteAsync('{');
        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Value != null)
            {
                bool numberKey = IsNumber(entry.Key);

                if (numberKey)
                    await writer.WriteAsync('[');
                await SerializePrimitive(entry.Key, writer);
                if (numberKey)
                    await writer.WriteAsync(']');

                await writer.WriteAsync('=');
                await Serialize(entry.Value, writer);
                await writer.WriteAsync(',');
            }
        }
        await writer.WriteAsync('}');
    }

    private async Task SerializeEnumerable(IEnumerable enumerable, TextWriter writer)
    {
        await writer.WriteAsync('{');
        foreach (object item in enumerable)
        {
            await Serialize(item, writer);
            await writer.WriteAsync(',');
        }
        await writer.WriteAsync('}');
    }

    private async Task SerializeObject(object obj, TextWriter writer)
    {
        Type type = obj.GetType();
        if (transformers.TryGetValue(type, out Func<object, object>? transform))
        {
            await Serialize(transform(obj), writer);
            return;
        }

        List<LuaPropertyInfo> props = GetProps(type);

        await writer.WriteAsync('{');
        foreach (LuaPropertyInfo prop in props)
        {
            object? value = prop.Get(obj);
            if (value != null)
            {
                await writer.WriteAsync(prop.Name);
                await writer.WriteAsync('=');
                await Serialize(value, writer);
                await writer.WriteAsync(',');
            }
        }
        await writer.WriteAsync('}');
    }

    private List<LuaPropertyInfo> GetProps(Type type) => objectProps.TryGetOrAdd(type, t =>
    {
        if (!assemblyWhitelist.Contains(t.Assembly))
            throw new ArgumentException($"Type {t.AssemblyQualifiedName} is not from a whitelisted assembly.");

        return t.GetProperties().Select(x => new LuaPropertyInfo(x)).ToList();
    });

    private static bool IsNumber(object obj) =>
        obj is byte or sbyte or short or ushort or int or uint or long or ulong or float or double;
}
