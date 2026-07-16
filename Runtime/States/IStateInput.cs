using System;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public interface IStateInput
    {
        void BeginArrayProperty(string name);
        void BeginArrayElement();
        void EndArray();
        void BeginObjectProperty(string name);
        void BeginObjectElement();
        void EndObject();
        byte ReadByte(string name);
        int ReadInt(string name);
        long ReadLong(string name);
        float ReadFloat(string name);
        double ReadDouble(string name);
        bool ReadBool(string name);
        string ReadString(string name);
        Vector2 ReadVector2(string name);
        Vector3 ReadVector3(string name);
        Vector2Int ReadVector2Int(string name);
        Vector3Int ReadVector3Int(string name);
        Quaternion ReadQuaternion(string name);
        Color ReadColor(string name);
        Guid ReadGuid(string name);
    }
}
