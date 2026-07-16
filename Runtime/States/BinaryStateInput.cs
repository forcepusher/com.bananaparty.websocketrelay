using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public class BinaryStateInput : IStateInput
    {
        private readonly ReadOnlyMemory<byte> _rootData;
        private readonly Stack<IBinaryReadLayer> _layers = new();
        private BinaryFieldReader _reader;

        public BinaryStateInput(ReadOnlyMemory<byte> data)
        {
            _rootData = data;
            _reader = new BinaryFieldReader(data);
        }

        public void BeginArrayProperty(string name)
        {
            if (name != "NetworkStates")
                throw new NotSupportedException($"Binary array property '{name}' is not supported.");

            _layers.Push(NetworkStatesReadLayer.Read(_reader, name));
        }

        public void BeginArrayElement() { }

        public void EndArray()
        {
            if (_layers.Count == 0 || _layers.Peek() is not NetworkStatesReadLayer)
                throw new InvalidOperationException("EndArray called without matching BeginArrayProperty.");

            _layers.Pop();
        }

        public void BeginObjectProperty(string name)
        {
            if (!Guid.TryParse(name, out Guid identityId))
                throw new KeyNotFoundException($"Network identity '{name}' was not found in binary state.");

            if (_layers.Count == 0 || _layers.Peek() is not IdentityMapReadLayer identityMapLayer)
                throw new InvalidOperationException("Cannot read keyed object property outside of a network states object.");

            if (!identityMapLayer.TryGetPayload(identityId, out ReadOnlyMemory<byte> payload))
                throw new KeyNotFoundException($"Network identity '{name}' was not found in binary state.");

            _layers.Push(new IdentityPayloadReadLayer());
            _reader = new BinaryFieldReader(payload);
        }

        public void BeginObjectElement()
        {
            if (_layers.Count == 0)
            {
                _layers.Push(IdentityMapReadLayer.Read(_rootData));
                return;
            }

            if (_layers.Peek() is NetworkStatesReadLayer networkStatesLayer)
                _reader = networkStatesLayer.BeginNextState();
        }

        public void EndObject()
        {
            if (_layers.Count == 0)
                return;

            if (_layers.Peek() is NetworkStatesReadLayer)
                return;

            if (_layers.Peek() is IdentityPayloadReadLayer)
            {
                _layers.Pop();
                return;
            }

            if (_layers.Peek() is IdentityMapReadLayer)
            {
                _layers.Pop();
                _reader = new BinaryFieldReader(_rootData);
            }
        }

        internal static IReadOnlyList<Guid> GetRootIdentityIds(ReadOnlyMemory<byte> data)
        {
            BinaryFieldReader reader = new(data);
            int identityCount = reader.ReadInt32();
            List<Guid> identityIds = new(identityCount);

            for (int identityIndex = 0; identityIndex < identityCount; identityIndex++)
            {
                identityIds.Add(reader.ReadGuidValue());
                int payloadLength = reader.ReadInt32();
                reader.ReadBytes(payloadLength);
            }

            return identityIds;
        }

        public string ReadString(string name)
        {
            _reader.VerifyEntryName(name);
            return _reader.ReadStringValue();
        }

        public byte ReadByte(string name)
        {
            _reader.VerifyEntryName(name);
            return _reader.ReadByteValue();
        }

        public int ReadInt(string name)
        {
            _reader.VerifyEntryName(name);
            return _reader.ReadInt32();
        }

        public long ReadLong(string name)
        {
            _reader.VerifyEntryName(name);
            return _reader.ReadInt64();
        }

        public float ReadFloat(string name)
        {
            _reader.VerifyEntryName(name);
            return _reader.ReadFloat32();
        }

        public double ReadDouble(string name)
        {
            _reader.VerifyEntryName(name);
            return _reader.ReadDouble64();
        }

        public bool ReadBool(string name)
        {
            _reader.VerifyEntryName(name);
            return _reader.ReadBoolValue();
        }

        public Vector2 ReadVector2(string name)
        {
            _reader.VerifyEntryName(name);
            return new Vector2(_reader.ReadFloat32(), _reader.ReadFloat32());
        }

        public Vector3 ReadVector3(string name)
        {
            _reader.VerifyEntryName(name);
            return new Vector3(_reader.ReadFloat32(), _reader.ReadFloat32(), _reader.ReadFloat32());
        }

        public Vector2Int ReadVector2Int(string name)
        {
            _reader.VerifyEntryName(name);
            return new Vector2Int(_reader.ReadInt32(), _reader.ReadInt32());
        }

        public Vector3Int ReadVector3Int(string name)
        {
            _reader.VerifyEntryName(name);
            return new Vector3Int(_reader.ReadInt32(), _reader.ReadInt32(), _reader.ReadInt32());
        }

        public Quaternion ReadQuaternion(string name)
        {
            _reader.VerifyEntryName(name);
            return new Quaternion(
                _reader.ReadFloat32(),
                _reader.ReadFloat32(),
                _reader.ReadFloat32(),
                _reader.ReadFloat32());
        }

        public Color ReadColor(string name)
        {
            _reader.VerifyEntryName(name);
            return new Color(
                _reader.ReadFloat32(),
                _reader.ReadFloat32(),
                _reader.ReadFloat32(),
                _reader.ReadFloat32());
        }

        public Guid ReadGuid(string name)
        {
            _reader.VerifyEntryName(name);
            return _reader.ReadGuidValue();
        }

        private interface IBinaryReadLayer { }

        private sealed class IdentityPayloadReadLayer : IBinaryReadLayer { }

        private sealed class BinaryFieldReader
        {
            private readonly ReadOnlyMemory<byte> _data;
            private int _position;

            public BinaryFieldReader(ReadOnlyMemory<byte> data)
            {
                _data = data;
            }

            public void VerifyEntryName(string expectedName)
            {
                int nameHash = ReadInt32();
                int expectedHash = Hash.StringToInt(expectedName);

                if (nameHash != expectedHash)
                {
                    throw new InvalidDataException(
                        $"Name hash mismatch. Expected '{expectedName ?? string.Empty}' ({expectedHash}), got {nameHash}.");
                }
            }

            public byte ReadByteValue()
            {
                if (_position >= _data.Length)
                    throw new EndOfStreamException("Unexpected end of binary stream while reading byte value.");

                return _data.Span[_position++];
            }

            public int ReadInt32()
            {
                if (_position + 4 > _data.Length)
                    throw new EndOfStreamException("Unexpected end of binary stream while reading Int32.");

                int value = BitConverter.ToInt32(_data.Span.Slice(_position, 4));
                _position += 4;
                return value;
            }

            public long ReadInt64()
            {
                if (_position + 8 > _data.Length)
                    throw new EndOfStreamException("Unexpected end of binary stream while reading Int64.");

                long value = BitConverter.ToInt64(_data.Span.Slice(_position, 8));
                _position += 8;
                return value;
            }

            public float ReadFloat32()
            {
                if (_position + 4 > _data.Length)
                    throw new EndOfStreamException("Unexpected end of binary stream while reading Float32.");

                float value = BitConverter.ToSingle(_data.Span.Slice(_position, 4));
                _position += 4;
                return value;
            }

            public double ReadDouble64()
            {
                if (_position + 8 > _data.Length)
                    throw new EndOfStreamException("Unexpected end of binary stream while reading Float64.");

                double value = BitConverter.ToDouble(_data.Span.Slice(_position, 8));
                _position += 8;
                return value;
            }

            public bool ReadBoolValue()
            {
                if (_position >= _data.Length)
                    throw new EndOfStreamException("Unexpected end of binary stream while reading boolean.");

                return _data.Span[_position++] != 0;
            }

            public string ReadStringValue()
            {
                if (_position + 2 > _data.Length)
                    throw new EndOfStreamException("Unexpected end of binary stream while reading string length.");

                ushort length = BitConverter.ToUInt16(_data.Span.Slice(_position, 2));
                _position += 2;

                if (length == 0)
                    return string.Empty;

                if (_position + length > _data.Length)
                    throw new EndOfStreamException("Unexpected end of binary stream while reading string content.");

                string value = Encoding.UTF8.GetString(_data.Span.Slice(_position, length));
                _position += length;
                return value;
            }

            public Guid ReadGuidValue()
            {
                if (_position + 16 > _data.Length)
                    throw new EndOfStreamException("Unexpected end of binary stream while reading Guid.");

                ReadOnlySpan<byte> guidBytes = _data.Span.Slice(_position, 16);
                _position += 16;
                return new Guid(guidBytes);
            }

            public ReadOnlyMemory<byte> ReadBytes(int length)
            {
                if (length < 0)
                    throw new InvalidDataException("Binary payload length cannot be negative.");

                if (_position + length > _data.Length)
                    throw new EndOfStreamException("Unexpected end of binary stream while reading bytes.");

                ReadOnlyMemory<byte> bytes = _data.Slice(_position, length);
                _position += length;
                return bytes;
            }
        }

        private sealed class IdentityMapReadLayer : IBinaryReadLayer
        {
            private readonly Dictionary<Guid, ReadOnlyMemory<byte>> _payloadsByIdentity = new();

            public static IdentityMapReadLayer Read(ReadOnlyMemory<byte> rootData)
            {
                IdentityMapReadLayer layer = new();
                BinaryFieldReader reader = new(rootData);
                int identityCount = reader.ReadInt32();

                for (int identityIndex = 0; identityIndex < identityCount; identityIndex++)
                {
                    Guid identityId = reader.ReadGuidValue();
                    int payloadLength = reader.ReadInt32();
                    ReadOnlyMemory<byte> payload = reader.ReadBytes(payloadLength);
                    layer._payloadsByIdentity[identityId] = payload;
                }

                return layer;
            }

            public bool TryGetPayload(Guid identityId, out ReadOnlyMemory<byte> payload)
            {
                return _payloadsByIdentity.TryGetValue(identityId, out payload);
            }
        }

        private sealed class NetworkStatesReadLayer : IBinaryReadLayer
        {
            private readonly ReadOnlyMemory<byte>[] _statePayloads;
            private int _nextStateIndex;

            private NetworkStatesReadLayer(ReadOnlyMemory<byte>[] statePayloads)
            {
                _statePayloads = statePayloads;
            }

            public static NetworkStatesReadLayer Read(BinaryFieldReader reader, string propertyName)
            {
                reader.VerifyEntryName(propertyName);
                int stateCount = reader.ReadInt32();
                ReadOnlyMemory<byte>[] statePayloads = new ReadOnlyMemory<byte>[stateCount];

                for (int stateIndex = 0; stateIndex < stateCount; stateIndex++)
                {
                    int payloadLength = reader.ReadInt32();
                    statePayloads[stateIndex] = reader.ReadBytes(payloadLength);
                }

                return new NetworkStatesReadLayer(statePayloads);
            }

            public BinaryFieldReader BeginNextState()
            {
                if (_nextStateIndex >= _statePayloads.Length)
                    throw new InvalidOperationException("No more network states in binary array.");

                return new BinaryFieldReader(_statePayloads[_nextStateIndex++]);
            }
        }
    }
}
