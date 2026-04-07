using System.Reflection;

namespace Jint.Pooling;

public sealed class MethodWrapperPool
{
    private static readonly Dictionary<MethodBase, FastFunctionHandler> FunctionPool = new Dictionary<MethodBase, FastFunctionHandler>();

    private static readonly Dictionary<Delegate, FastDelegateHandler> DelegatePool = new Dictionary<Delegate, FastDelegateHandler>();

    public static FastFunctionHandler Get(MethodBase method, bool invoke = false)
    {
        if (FunctionPool.TryGetValue(method, out var value))
        {
            return value;
        }
        if (invoke)
        {
            return FunctionPool[method] = method.Invoke;
        }
        return FunctionPool[method] = FastInvoker.GetHandler(method);
    }

    public static FastDelegateHandler Get(Delegate function, bool invoke = false)
    {
        if (DelegatePool.TryGetValue(function, out var value))
        {
            return value;
        }
        if (invoke)
        {
            MethodInfo method = function.Method;
            object target = function.Target;
            return DelegatePool[function] = (object[] args) => method.Invoke(target, args);
        }
        return DelegatePool[function] = FastInvoker.GetHandler(function);
    }
}
