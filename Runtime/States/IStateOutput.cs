using System;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public interface IStateOutput
    {
        void BeginArrayProperty(string name);
        void BeginArrayElement();
        void EndArray();
        void BeginObjectProperty(string name);
        void BeginObjectElement();
        void EndObject();
        void WriteByte(string name, byte value);
        void WriteInt(string name, int value);
        void WriteLong(string name, long value);
        void WriteFloat(string name, float value);
        void WriteDouble(string name, double value);
        void WriteBool(string name, bool value);
        void WriteString(string name, string value);
        void WriteVector2(string name, Vector2 value);
        void WriteVector3(string name, Vector3 value);
        void WriteVector2Int(string name, Vector2Int value);
        void WriteVector3Int(string name, Vector3Int value);
        void WriteQuaternion(string name, Quaternion value);
        void WriteColor(string name, Color value);
        void WriteGuid(string name, Guid value);
    }
}
