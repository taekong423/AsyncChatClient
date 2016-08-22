using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace AsyncClient
{
    enum ChatType
    {
        Send,
        Receive,
        System,
        Console,
        Debug
    }

    enum ServerType
    {
        Login,
        Chat
    }

    enum RunType
    {
        Start,
        Login,
        Lobby,
        Room
    }

    enum RunState
    {
        Waiting,
        Idle,
        Menu,
        Signup,
        Signin,
        Modify,
        Delete,
        List,
        Join,
        Leave,
        Create,
        Logout,
        Chat
    }

    public class CommonHelper
    {
        public static byte[] StructToBytes(object obj)
        {
            try
            {
                int datasize = Marshal.SizeOf(obj);//((PACKET_DATA)obj).TotalBytes; // 구조체에 할당된 메모리의 크기를 구한다.
                IntPtr buff = Marshal.AllocHGlobal(datasize); // 비관리 메모리 영역에 구조체 크기만큼의 메모리를 할당한다.
                Marshal.StructureToPtr(obj, buff, false); // 할당된 구조체 객체의 주소를 구한다.
                byte[] data = new byte[datasize]; // 구조체가 복사될 배열
                Marshal.Copy(buff, data, 0, datasize); // 구조체 객체를 배열에 복사
                Marshal.FreeHGlobal(buff); // 비관리 메모리 영역에 할당했던 메모리를 해제함

                return data; // 배열을 리턴
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.ReadKey();
                Environment.Exit(0);
                return null;
            }
        }

        public static T BytesToStruct<T>(byte[] b) where T : struct
        {
            try
            {
                IntPtr buff = Marshal.AllocHGlobal(b.Length);
                Marshal.Copy(b, 0, buff, b.Length);
                T obj = (T)Marshal.PtrToStructure(buff, typeof(T));
                Marshal.FreeHGlobal(buff);

                if (Marshal.SizeOf(obj) != b.Length)
                    return default(T);

                return obj;

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.ReadKey();
                Environment.Exit(0);
                return default(T);
            }
        }

        public static object[] ByteToStructureArray(byte[] data, Type type)
        {
            try
            {
                int objLength = data.Length / (Marshal.SizeOf(type));
                object[] objList = new object[objLength];

                for (int idx = 0; idx < objList.Length; idx++)
                {
                    byte[] tmp = new byte[Marshal.SizeOf(type)];
                    Array.Copy(data, Marshal.SizeOf(type) * idx, tmp, 0, tmp.Length);

                    IntPtr buff = Marshal.AllocHGlobal(Marshal.SizeOf(type)); // 배열의 크기만큼 비관리 메모리 영역에 메모리를 할당한다.
                    Marshal.Copy(tmp, 0, buff, tmp.Length); // 배열에 저장된 데이터를 위에서 할당한 메모리 영역에 복사한다.

                    object obj = Marshal.PtrToStructure(buff, type); // 복사된 데이터를 구조체 객체로 변환한다.
                    Marshal.FreeHGlobal(buff); // 비관리 메모리 영역에 할당했던 메모리를 해제함

                    if (Marshal.SizeOf(obj) != Marshal.SizeOf(type))// (((PACKET_DATA)obj).TotalBytes != data.Length) // 구조체와 원래의 데이터의 크기 비교
                    {
                        return null; // 크기가 다르면 null 리턴
                    }
                    objList[idx] = obj;
                }

                return objList; // 구조체 리턴
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.ReadKey();
                Environment.Exit(0);
                return null;
            }
            
        }
    }
}
