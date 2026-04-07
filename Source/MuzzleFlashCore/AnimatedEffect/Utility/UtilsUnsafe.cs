using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MuzzleFlash
{
    public static unsafe class UtilsUnsafe
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* Alloc(int size)
        {

            return Marshal.AllocHGlobal(size).ToPointer();

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free(void* ptr)
        {

            Marshal.FreeHGlobal((IntPtr)ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free(IntPtr ptr)
        {

            Marshal.FreeHGlobal(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr ToRef<T>(T value) where T : unmanaged
        {
            IntPtr ptr = Marshal.AllocHGlobal(sizeof(T));

            Marshal.StructureToPtr(value, ptr, false);
            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf(Type type)
        {
            return Marshal.SizeOf(type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>() where T : unmanaged
        {
            return sizeof(T);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemCopy(void* src, void* dest, long size)
        {
            Buffer.MemoryCopy(src, dest, size, size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadStruct<T>(IntPtr ptr) where T : unmanaged
        {
            return Marshal.PtrToStructure<T>(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get<T>(IntPtr ptr)
        {
            return (T)Marshal.PtrToStructure(ptr, typeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get<T>(void* ptr)
        {
            return (T)Marshal.PtrToStructure((IntPtr)ptr, typeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void WriteArrayElement<T>(void* destination, int index, T value) where T : unmanaged
        {
            *(T*)((byte*)destination + (long)index * (long)sizeof(T)) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static T ReadArrayElement<T>(void* source, int index) where T : unmanaged
        {
            return *(T*)((byte*)source + (long)index * (long)sizeof(T));
        }
    }
}

