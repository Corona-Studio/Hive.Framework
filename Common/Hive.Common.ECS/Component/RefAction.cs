namespace Hive.Framework.ECS.Component
{
    public delegate void RefAction<T>(ref T component);

    public delegate void RefAction<T1, T2>(ref T1 arg1, ref T2 arg2);

    public delegate void RefAction<T1, T2, T3>(ref T1 arg1, ref T2 arg2, ref T3 arg3);

    public delegate void RefAction<T1, T2, T3, T4>(ref T1 arg1, ref T2 arg2, ref T3 arg3, ref T4 arg4);

    public delegate void RefAction<T1, T2, T3, T4, T5>(ref T1 arg1, ref T2 arg2, ref T3 arg3, ref T4 arg4, ref T5 arg5);
}