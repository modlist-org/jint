using System.Reflection;
using System.Reflection.Emit;

namespace Jint;

public static class FastInvoker
{
    public static FastDelegateHandler GetHandler(Delegate function, bool directBoxValueAccess = true)
    {
        FastFunctionHandler handler = GetHandler(function.Method, directBoxValueAccess);
        object target = function.Target;
        return (object[] args) => handler(target, args);
    }

    public static FastFunctionHandler GetHandler(MethodBase m, bool directBoxValueAccess = true)
    {
        string text = $"{m.DeclaringType}.{m}";
        DynamicMethod dynamicMethod = new DynamicMethod("FastInvoke_" + text + "_" + (directBoxValueAccess ? "direct" : "indirect"), typeof(object), new Type[2]
        {
            typeof(object),
            typeof(object[])
        }, typeof(FastInvoker), skipVisibility: true);
        ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
        if (!m.IsStatic)
        {
            iLGenerator.Emit(OpCodes.Ldarg_0);
        }
        bool flag = true;
        ParameterInfo[] parameters = m.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            Type type = parameters[i].ParameterType;
            bool isByRef = type.IsByRef;
            if (isByRef)
            {
                type = type.GetElementType();
            }
            bool isValueType = type.IsValueType;
            if (isByRef && isValueType && !directBoxValueAccess)
            {
                iLGenerator.Emit(OpCodes.Ldarg_1);
                EmitFastInt(iLGenerator, i);
            }
            iLGenerator.Emit(OpCodes.Ldarg_1);
            EmitFastInt(iLGenerator, i);
            if (isByRef && !isValueType)
            {
                iLGenerator.Emit(OpCodes.Ldelema, typeof(object));
                continue;
            }
            iLGenerator.Emit(OpCodes.Ldelem_Ref);
            if (!isValueType)
            {
                continue;
            }
            if (!isByRef || !directBoxValueAccess)
            {
                iLGenerator.Emit(OpCodes.Unbox_Any, type);
                if (isByRef)
                {
                    iLGenerator.Emit(OpCodes.Box, type);
                    iLGenerator.Emit(OpCodes.Dup);
                    if (flag)
                    {
                        flag = false;
                        iLGenerator.DeclareLocal(typeof(object), pinned: false);
                    }
                    iLGenerator.Emit(OpCodes.Stloc_0);
                    iLGenerator.Emit(OpCodes.Stelem_Ref);
                    iLGenerator.Emit(OpCodes.Ldloc_0);
                    iLGenerator.Emit(OpCodes.Unbox, type);
                }
            }
            else
            {
                iLGenerator.Emit(OpCodes.Unbox, type);
            }
        }
        if (m is ConstructorInfo con)
        {
            iLGenerator.Emit(OpCodes.Newobj, con);
        }
        else if (m is MethodInfo methodInfo)
        {
            if (methodInfo.IsStatic)
            {
                iLGenerator.EmitCall(OpCodes.Call, methodInfo, null);
            }
            else
            {
                iLGenerator.EmitCall(OpCodes.Callvirt, methodInfo, null);
            }
            if (methodInfo.ReturnType == typeof(void))
            {
                iLGenerator.Emit(OpCodes.Ldnull);
            }
            else
            {
                Type returnType = methodInfo.ReturnType;
                if (returnType.IsValueType)
                {
                    iLGenerator.Emit(OpCodes.Box, returnType);
                }
            }
        }
        iLGenerator.Emit(OpCodes.Ret);
        return (FastFunctionHandler) dynamicMethod.CreateDelegate(typeof(FastFunctionHandler));
    }

    internal static void EmitFastInt(ILGenerator il, int value)
    {
        switch (value)
        {
            case -1: il.Emit(OpCodes.Ldc_I4_M1); return;
            case 0: il.Emit(OpCodes.Ldc_I4_0); return;
            case 1: il.Emit(OpCodes.Ldc_I4_1); return;
            case 2: il.Emit(OpCodes.Ldc_I4_2); return;
            case 3: il.Emit(OpCodes.Ldc_I4_3); return;
            case 4: il.Emit(OpCodes.Ldc_I4_4); return;
            case 5: il.Emit(OpCodes.Ldc_I4_5); return;
            case 6: il.Emit(OpCodes.Ldc_I4_6); return;
            case 7: il.Emit(OpCodes.Ldc_I4_7); return;
            case 8: il.Emit(OpCodes.Ldc_I4_8); return;
        }

        if (value > -129 && value < 128)
        {
            il.Emit(OpCodes.Ldc_I4_S, (sbyte) value);
        }
        else
        {
            il.Emit(OpCodes.Ldc_I4, value);
        }
    }

    internal static void EmitTestWriteLine(ILGenerator il)
    {
        il.Emit(OpCodes.Dup);
        LocalBuilder localBuilder = il.DeclareLocal(typeof(object));
        il.Emit(OpCodes.Stloc, localBuilder);
        il.EmitWriteLine(localBuilder);
    }
}
