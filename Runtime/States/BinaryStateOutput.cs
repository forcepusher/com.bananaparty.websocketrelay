using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public class BinaryStateOutput : IStateOutput, IDisposable
    {
        private readonly MemoryStream _stream = new();
        private readonly BinaryWriter _rootWriter;
        private readonly Stack<IBinaryWriteScope> _scopes = new();
        private BinaryWriter _activeWriter;

        public BinaryStateOutput()
        {
            _rootWriter = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
            _activeWriter = _rootWriter;
        }

        public ReadOnlyMemory<byte> GetBuffer() => _stream.ToArray().AsMemory();

        public void BeginArrayProperty(string name)
        {
            if (name != "NetworkStates")
                throw new NotSupportedException($"Binary array property '{name}' is not supported.");

            NetworkStatesWriteScope networkStatesScope = new(Hash.StringToInt(name));
            _scopes.Push(networkStatesScope);
        }

        public void BeginArrayElement() { }

        public void EndArray()
        {
            if (_scopes.Count == 0)
                throw new InvalidOperationException("EndArray called without matching BeginArrayProperty.");

            _scopes.Peek().EndArray(this);
            _scopes.Pop();
        }

        public void BeginObjectProperty(string name)
        {
            if (!Guid.TryParse(name, out Guid identityId))
                throw new NotSupportedException($"Binary object property '{name}' is not supported.");

            _scopes.Peek().BeginIdentityPayload(this, identityId);
        }

        public void BeginObjectElement()
        {
            if (_scopes.Count == 0)
            {
                _scopes.Push(new IdentityMapWriteScope());
                return;
            }

            _scopes.Peek().BeginObjectElement(this);
        }

        public void EndObject()
        {
            if (_scopes.Count == 0)
                throw new InvalidOperationException("EndObject called without matching BeginObjectElement.");

            _scopes.Peek().EndObject(this);
        }

        public void WriteByte(string name, byte value) => WriteEntry(name, value);

        public void WriteInt(string name, int value) => WriteEntry(name, value);

        public void WriteLong(string name, long value) => WriteEntry(name, value);

        public void WriteFloat(string name, float value) => WriteEntry(name, value);

        public void WriteDouble(string name, double value) => WriteEntry(name, value);

        public void WriteBool(string name, bool value) => WriteEntry(name, value);

        public void WriteString(string name, string value) => WriteEntry(name, value);

        public void WriteVector2(string name, Vector2 value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value.x);
            _activeWriter.Write(value.y);
        }

        public void WriteVector3(string name, Vector3 value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value.x);
            _activeWriter.Write(value.y);
            _activeWriter.Write(value.z);
        }

        public void WriteVector2Int(string name, Vector2Int value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value.x);
            _activeWriter.Write(value.y);
        }

        public void WriteVector3Int(string name, Vector3Int value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value.x);
            _activeWriter.Write(value.y);
            _activeWriter.Write(value.z);
        }

        public void WriteQuaternion(string name, Quaternion value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value.x);
            _activeWriter.Write(value.y);
            _activeWriter.Write(value.z);
            _activeWriter.Write(value.w);
        }

        public void WriteColor(string name, Color value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value.r);
            _activeWriter.Write(value.g);
            _activeWriter.Write(value.b);
            _activeWriter.Write(value.a);
        }

        public void WriteGuid(string name, Guid value) => WriteEntry(name, value);

        public byte[] ToArray() => _stream.ToArray();

        public void Dispose()
        {
            _rootWriter.Dispose();
            _stream.Dispose();
        }

        private BinaryWriter GetIdentityWriter()
        {
            foreach (IBinaryWriteScope scope in _scopes)
            {
                BinaryWriter identityWriter = scope.IdentityWriter;
                if (identityWriter != null)
                    return identityWriter;
            }

            return _rootWriter;
        }

        private void SetActiveWriter(BinaryWriter writer) => _activeWriter = writer;

        private void PopScope() => _scopes.Pop();

        private void PushScope(IBinaryWriteScope scope) => _scopes.Push(scope);

        private interface IBinaryWriteScope
        {
            BinaryWriter IdentityWriter { get; }

            void BeginIdentityPayload(BinaryStateOutput output, Guid identityId);

            void BeginObjectElement(BinaryStateOutput output);

            void EndObject(BinaryStateOutput output);

            void EndArray(BinaryStateOutput output);
        }

        private sealed class IdentityMapWriteScope : IBinaryWriteScope
        {
            private readonly List<IdentityEntry> _entries = new();

            public BinaryWriter IdentityWriter => null;

            public void BeginIdentityPayload(BinaryStateOutput output, Guid identityId)
            {
                IdentityPayloadWriteScope identityScope = new(identityId, this);
                output.PushScope(identityScope);
                output.SetActiveWriter(identityScope.Writer);
            }

            public void BeginObjectElement(BinaryStateOutput output)
            {
                throw new InvalidOperationException("BeginObjectElement called outside of a network states array.");
            }

            public void EndObject(BinaryStateOutput output)
            {
                WriteTo(output._rootWriter);
                output.PopScope();
                output.SetActiveWriter(output._rootWriter);
            }

            public void EndArray(BinaryStateOutput output)
            {
                throw new InvalidOperationException("EndArray called without matching BeginArrayProperty.");
            }

            public void AddIdentity(Guid identityId, byte[] payload)
            {
                _entries.Add(new IdentityEntry(identityId, payload));
            }

            private void WriteTo(BinaryWriter writer)
            {
                writer.Write(_entries.Count);
                foreach (IdentityEntry entry in _entries)
                {
                    writer.Write(entry.IdentityId.ToByteArray());
                    writer.Write(entry.Payload.Length);
                    writer.Write(entry.Payload);
                }
            }

            private readonly struct IdentityEntry
            {
                public IdentityEntry(Guid identityId, byte[] payload)
                {
                    IdentityId = identityId;
                    Payload = payload;
                }

                public Guid IdentityId { get; }
                public byte[] Payload { get; }
            }
        }

        private sealed class IdentityPayloadWriteScope : IBinaryWriteScope
        {
            private readonly MemoryStream _stream = new();
            private readonly BinaryWriter _writer;

            public IdentityPayloadWriteScope(Guid identityId, IdentityMapWriteScope parentMap)
            {
                IdentityId = identityId;
                ParentMap = parentMap;
                _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
            }

            public Guid IdentityId { get; }
            public IdentityMapWriteScope ParentMap { get; }
            public BinaryWriter Writer => _writer;
            public BinaryWriter IdentityWriter => Writer;

            public void BeginIdentityPayload(BinaryStateOutput output, Guid identityId)
            {
                throw new InvalidOperationException("Identity payload must be written inside an identity map.");
            }

            public void BeginObjectElement(BinaryStateOutput output)
            {
                throw new InvalidOperationException("BeginObjectElement called outside of a network states array.");
            }

            public void EndObject(BinaryStateOutput output)
            {
                ParentMap.AddIdentity(IdentityId, ToArray());
                output.PopScope();
                output.SetActiveWriter(output._rootWriter);
            }

            public void EndArray(BinaryStateOutput output)
            {
                throw new InvalidOperationException("EndArray called without matching BeginArrayProperty.");
            }

            private byte[] ToArray() => _stream.ToArray();
        }

        private sealed class NetworkStatesWriteScope : IBinaryWriteScope
        {
            private readonly int _propertyHash;
            private readonly List<byte[]> _statePayloads = new();
            private MemoryStream _activeStateStream;
            private BinaryWriter _activeStateWriter;

            public NetworkStatesWriteScope(int propertyHash)
            {
                _propertyHash = propertyHash;
            }

            public BinaryWriter IdentityWriter => null;

            public void BeginIdentityPayload(BinaryStateOutput output, Guid identityId)
            {
                throw new InvalidOperationException("Identity payload must be written inside an identity map.");
            }

            public void BeginObjectElement(BinaryStateOutput output)
            {
                BeginState();
                output.SetActiveWriter(_activeStateWriter);
            }

            public void EndObject(BinaryStateOutput output)
            {
                if (_activeStateStream == null)
                    throw new InvalidOperationException("EndObject called in an invalid binary write scope.");

                EndState();
                output.SetActiveWriter(output.GetIdentityWriter());
            }

            public void EndArray(BinaryStateOutput output)
            {
                WriteTo(output._activeWriter);
            }

            private void BeginState()
            {
                _activeStateStream = new MemoryStream();
                _activeStateWriter = new BinaryWriter(_activeStateStream, Encoding.UTF8, leaveOpen: true);
            }

            private void EndState()
            {
                _activeStateWriter.Dispose();
                _statePayloads.Add(_activeStateStream.ToArray());
                _activeStateStream.Dispose();
                _activeStateStream = null;
                _activeStateWriter = null;
            }

            private void WriteTo(BinaryWriter writer)
            {
                writer.Write(_propertyHash);
                writer.Write(_statePayloads.Count);
                foreach (byte[] statePayload in _statePayloads)
                {
                    writer.Write(statePayload.Length);
                    writer.Write(statePayload);
                }
            }
        }

        private void WriteEntry(string name, byte value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value);
        }

        private void WriteEntry(string name, int value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value);
        }

        private void WriteEntry(string name, long value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value);
        }

        private void WriteEntry(string name, float value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value);
        }

        private void WriteEntry(string name, double value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value);
        }

        private void WriteEntry(string name, bool value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value);
        }

        private void WriteEntry(string name, string value)
        {
            WriteNameHash(name);
            byte[] stringBytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            _activeWriter.Write((ushort)stringBytes.Length);
            _activeWriter.Write(stringBytes);
        }

        private void WriteEntry(string name, Guid value)
        {
            WriteNameHash(name);
            _activeWriter.Write(value.ToByteArray());
        }

        private void WriteNameHash(string name)
        {
            _activeWriter.Write(Hash.StringToInt(name));
        }
    }
}
