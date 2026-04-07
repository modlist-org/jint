using System.Globalization;
using System.Reflection;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Function;
using Jint.Pooling;
using Jint.Runtime.Interop.Attributes;

namespace Jint.Runtime.Interop;

public sealed class MethodInfoWrapper : Function
{
    private static readonly JsString _name = new JsString("method");

    internal readonly MethodInfo _m;

    private readonly FastFunctionHandler _ffh;

    private readonly ParameterInfo[] _parameterInfos;

    private readonly bool _methodContainsParamsArgument;

    private readonly bool _wrapRawObject;

    public MethodInfoWrapper(Engine engine, MethodInfo m) : base(engine, engine.Realm, _name, FunctionThisMode.Global)
    {
        _m = m;
        _ffh = MethodWrapperPool.Get(m, engine.Options.ClrMethodWrapSlow);
        _prototype = engine.Realm.Intrinsics.Function.PrototypeObject;
        _parameterInfos = _m.GetParameters();
        _methodContainsParamsArgument = false;
        _wrapRawObject = m.IsDefined(typeof(RawReturnAttribute));
        ParameterInfo[] parameterInfos = _parameterInfos;
        for (int i = 0; i < parameterInfos.Length; i++)
        {
            if (Attribute.IsDefined(parameterInfos[i], typeof(ParamArrayAttribute)))
            {
                _methodContainsParamsArgument = true;
                break;
            }
        }
    }

    protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
    {
        var parameterInfos = _parameterInfos;
        var requiresEngineShift = DelegateWrapper.IsRequireEngineShift(ref parameterInfos);

        int paramCount = parameterInfos.Length;
        int nonParamsCount = _methodContainsParamsArgument ? paramCount - 1 : paramCount;
        int jsArgCount = arguments.Length;

        var typeConverter = Engine.TypeConverter;
        var valueCoercion = Engine.Options.Interop.ValueCoercion;

        var array = new object?[paramCount];

        for (int i = 0; i < Math.Min(jsArgCount, nonParamsCount); i++)
        {
            var paramType = parameterInfos[i].ParameterType;
            var arg = arguments[i];

            if (typeof(JsValue).IsAssignableFrom(paramType))
            {
                array[i] = arg;
            }
            else if (!ReflectionExtensions.TryConvertViaTypeCoercion(paramType, valueCoercion, arg, out array[i]))
            {
                array[i] = typeConverter.Convert(arg.ToObject(), paramType, CultureInfo.InvariantCulture);
            }
        }

        for (int i = jsArgCount; i < nonParamsCount; i++)
        {
            var param = parameterInfos[i];

            if (param.HasDefaultValue && param.DefaultValue != DBNull.Value && param.DefaultValue != System.Type.Missing)
            {
                array[i] = param.DefaultValue;
            }
            else if (param.ParameterType.IsValueType)
            {
                array[i] = Activator.CreateInstance(param.ParameterType);
            }
            else
            {
                array[i] = null;
            }
        }

        if (_methodContainsParamsArgument)
        {
            int paramsIndex = paramCount - 1;
            int paramsCount = Math.Max(0, jsArgCount - nonParamsCount);
            var elementType = parameterInfos[paramsIndex].ParameterType.GetElementType();
            var paramsArray = Array.CreateInstance(elementType!, paramsCount);

            for (int i = 0; i < paramsCount; i++)
            {
                var arg = arguments[nonParamsCount + i];
                object? converted;
                if (elementType == typeof(JsValue))
                {
                    converted = arg;
                }
                else if (!ReflectionExtensions.TryConvertViaTypeCoercion(elementType!, valueCoercion, arg, out converted))
                {
                    converted = typeConverter.Convert(arg.ToObject(), elementType!, CultureInfo.InvariantCulture);
                }
                paramsArray.SetValue(converted, i);
            }

            array[paramsIndex] = paramsArray;
        }

        if (requiresEngineShift)
        {
            DelegateWrapper.EngineShift(ref array, Engine);
        }

        try
        {
            var result = _ffh(null!, (object[]) array!);
            return _wrapRawObject ? new ObjectWrapper(Engine, result) : FromObject(Engine, result);
        }
        catch (TargetInvocationException tie)
        {
            Throw.MeaningfulException(_engine, tie);
            throw;
        }
    }
}
