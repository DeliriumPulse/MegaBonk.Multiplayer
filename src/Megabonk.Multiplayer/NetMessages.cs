// File: NetMessages.cs
using System;
using System.IO;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    internal enum NetMsgType : byte
    {
        HELLO = 1,
        HELLO_ACK = 2,
        START_RUN = 3,
        PAWN_SPAWN = 4,
        PAWN_DESPAWN = 5,
        PAWN_TRANSFORM = 6
    }

    internal static class NetMsgUtil
    {
        public static byte[] Build(Action<BinaryWriter> writerAction, NetMsgType type, bool reliable, out bool isReliable)
        {
            using (var ms = new MemoryStream(64))
            using (var bw = new BinaryWriter(ms))
            {
                // Header: message type
                bw.Write((byte)type);
                writerAction?.Invoke(bw);
                bw.Flush();
                isReliable = reliable;
                return ms.ToArray();
            }
        }

        public static NetMsgType PeekType(ArraySegment<byte> data)
        {
            if (data.Count <= 0) return 0;
            return (NetMsgType)data.Array[data.Offset];
        }

        public static BinaryReader Reader(ArraySegment<byte> data, out int startOffset)
        {
            // Skip 1 byte (type)
            startOffset = data.Offset + 1;
            var ms = new MemoryStream(data.Array, startOffset, data.Count - 1, false);
            return new BinaryReader(ms);
        }

        public static void WriteVector3(BinaryWriter bw, Vector3 v)
        {
            bw.Write(v.x); bw.Write(v.y); bw.Write(v.z);
        }

        public static Vector3 ReadVector3(BinaryReader br)
        {
            return new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        }

        public static void WriteQuaternion(BinaryWriter bw, Quaternion q)
        {
            bw.Write(q.x); bw.Write(q.y); bw.Write(q.z); bw.Write(q.w);
        }

        public static Quaternion ReadQuaternion(BinaryReader br)
        {
            return new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        }
    }
}
